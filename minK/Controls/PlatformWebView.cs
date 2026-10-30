using Avalonia;
using Avalonia.Controls;
using Avalonia.Platform;
using Avalonia.Threading;
using Avalonia.VisualTree;
using Microsoft.Web.WebView2.Core;
using System;
using System.Collections.Generic;
using System.IO;
using System.Runtime.InteropServices;
using System.Threading.Tasks;

namespace mink.Controls
{
    public class PlatformWebView : NativeControlHost, IWebSurface
    {
        private static Task<CoreWebView2Environment>? _sharedEnvTask;
        private static readonly object _envLock = new();

        private IntPtr _childHwnd;
        private CoreWebView2Controller? _controller;
        private CoreWebView2? _webView;
        private bool _isDestroyed;
        private bool _isReady;

        private readonly Dictionary<string, string> _virtualHosts = new(StringComparer.OrdinalIgnoreCase);
        private readonly Queue<Func<Task>> _pendingOperations = new();

        public bool AllowDefaultContextMenu { get; set; } = true;
        public bool AllowBrowserZoom { get; set; } = true;

        public bool IsReady => _isReady;
        public event EventHandler? SurfaceReady;
        public event EventHandler<string>? WebMessageReceived;

        public PlatformWebView()
        {
            LayoutUpdated += OnLayoutUpdated;
            PropertyChanged += OnControlPropertyChanged;
        }

        private static Task<CoreWebView2Environment> GetSharedEnvironmentAsync()
        {
            lock (_envLock)
            {
                if (_sharedEnvTask == null || _sharedEnvTask.IsFaulted)
                {
                    string userDataDir = Path.Combine(
                        Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData),
                        "minK",
                        "ChromiumData");
                    Directory.CreateDirectory(userDataDir);

                    var options = new CoreWebView2EnvironmentOptions(
                        additionalBrowserArguments: "--disable-features=msSmartScreenProtection --no-first-run --disable-background-networking");

                    _sharedEnvTask = CoreWebView2Environment.CreateAsync(
                        browserExecutableFolder: null,
                        userDataFolder: userDataDir,
                        options: options);
                }
                return _sharedEnvTask;
            }
        }

        protected override IPlatformHandle CreateNativeControlCore(IPlatformHandle parent)
        {
            _isDestroyed = false;

            if (OperatingSystem.IsWindows())
            {
                int w = Math.Max(1, (int)Bounds.Width);
                int h = Math.Max(1, (int)Bounds.Height);

                _childHwnd = Win32Interop.CreateWindowExW(
                    0,
                    "Static",
                    "",
                    Win32Interop.WS_CHILD | Win32Interop.WS_VISIBLE | Win32Interop.WS_CLIPCHILDREN,
                    0, 0, w, h,
                    parent.Handle,
                    IntPtr.Zero,
                    IntPtr.Zero,
                    IntPtr.Zero);

                Dispatcher.UIThread.Post(() => _ = InitializeWindowsWebViewAsync(_childHwnd));
                return new PlatformHandle(_childHwnd, "HWND");
            }

            return base.CreateNativeControlCore(parent);
        }

        protected override void DestroyNativeControlCore(IPlatformHandle control)
        {
            _isDestroyed = true;
            _isReady = false;

            try
            {
                if (_webView != null)
                {
                    _webView.WebMessageReceived -= OnCoreWebMessageReceived;
                    _webView = null;
                }

                if (_controller != null)
                {
                    _controller.Close();
                    _controller = null;
                }
            }
            catch
            {
                // Ignore teardown exceptions
            }

            if (OperatingSystem.IsWindows() && _childHwnd != IntPtr.Zero)
            {
                Win32Interop.DestroyWindow(_childHwnd);
                _childHwnd = IntPtr.Zero;
            }
            else
            {
                base.DestroyNativeControlCore(control);
            }
        }

        private async Task InitializeWindowsWebViewAsync(IntPtr hwnd)
        {
            try
            {
                var env = await GetSharedEnvironmentAsync();
                if (_isDestroyed || hwnd == IntPtr.Zero) return;

                var controller = await env.CreateCoreWebView2ControllerAsync(hwnd);
                if (_isDestroyed)
                {
                    controller.Close();
                    return;
                }

                _controller = controller;
                _controller.DefaultBackgroundColor = System.Drawing.Color.FromArgb(255, 30, 30, 30);

                _webView = _controller.CoreWebView2;
                _webView.Settings.AreDefaultContextMenusEnabled = AllowDefaultContextMenu;
                _webView.Settings.IsStatusBarEnabled = false;
                _webView.Settings.AreDevToolsEnabled = true;
                _webView.Settings.IsZoomControlEnabled = AllowBrowserZoom;
                _webView.WebMessageReceived += OnCoreWebMessageReceived;

                foreach (var kvp in _virtualHosts)
                {
                    if (Directory.Exists(kvp.Value))
                    {
                        _webView.SetVirtualHostNameToFolderMapping(
                            kvp.Key,
                            kvp.Value,
                            CoreWebView2HostResourceAccessKind.Allow);
                    }
                }

                SyncBoundsAndVisibility();
                _isReady = true;

                while (_pendingOperations.Count > 0)
                {
                    var op = _pendingOperations.Dequeue();
                    await op();
                }

                SurfaceReady?.Invoke(this, EventArgs.Empty);
            }
            catch (Exception ex)
            {
                System.Diagnostics.Debug.WriteLine($"[PlatformWebView] Initialization error: {ex.Message}");
            }
        }

        private void OnCoreWebMessageReceived(object? sender, CoreWebView2WebMessageReceivedEventArgs e)
        {
            try
            {
                string payload;
                try
                {
                    payload = e.TryGetWebMessageAsString();
                }
                catch (ArgumentException)
                {
                    payload = e.WebMessageAsJson;
                }

                if (!string.IsNullOrEmpty(payload))
                {
                    WebMessageReceived?.Invoke(this, payload);
                }
            }
            catch
            {
                // Ignore malformed message
            }
        }

        private void OnControlPropertyChanged(object? sender, AvaloniaPropertyChangedEventArgs e)
        {
            if (e.Property == BoundsProperty || e.Property == IsVisibleProperty)
            {
                SyncBoundsAndVisibility();
            }
        }

        private void OnLayoutUpdated(object? sender, EventArgs e)
        {
            SyncBoundsAndVisibility();
        }

        private void SyncBoundsAndVisibility()
        {
            if (_controller == null || _isDestroyed) return;

            bool effectivelyVisible = this.IsEffectivelyVisible && Bounds.Width > 1 && Bounds.Height > 1;
            if (_controller.IsVisible != effectivelyVisible)
            {
                _controller.IsVisible = effectivelyVisible;
            }

            if (!effectivelyVisible) return;

            double scale = this.GetVisualRoot()?.RenderScaling ?? 1.0;
            int pxWidth = Math.Max(1, (int)Math.Round(Bounds.Width * scale));
            int pxHeight = Math.Max(1, (int)Math.Round(Bounds.Height * scale));

            if (OperatingSystem.IsWindows() && _childHwnd != IntPtr.Zero)
            {
                Win32Interop.MoveWindow(_childHwnd, 0, 0, pxWidth, pxHeight, true);
            }

            var current = _controller.Bounds;
            if (current.Width != pxWidth || current.Height != pxHeight || current.X != 0 || current.Y != 0)
            {
                _controller.Bounds = new System.Drawing.Rectangle(0, 0, pxWidth, pxHeight);
            }
        }

        public void MapLocalFolder(string virtualHostName, string folderPath)
        {
            _virtualHosts[virtualHostName] = folderPath;
            if (_webView != null && Directory.Exists(folderPath))
            {
                _webView.SetVirtualHostNameToFolderMapping(
                    virtualHostName,
                    folderPath,
                    CoreWebView2HostResourceAccessKind.Allow);
            }
        }

        public Task NavigateAsync(string url)
        {
            if (_isReady && _webView != null)
            {
                _webView.Navigate(url);
                return Task.CompletedTask;
            }

            _pendingOperations.Enqueue(() =>
            {
                _webView?.Navigate(url);
                return Task.CompletedTask;
            });
            return Task.CompletedTask;
        }

        public Task NavigateToStringAsync(string htmlContent)
        {
            if (_isReady && _webView != null)
            {
                _webView.NavigateToString(htmlContent);
                return Task.CompletedTask;
            }

            _pendingOperations.Enqueue(() =>
            {
                _webView?.NavigateToString(htmlContent);
                return Task.CompletedTask;
            });
            return Task.CompletedTask;
        }

        public Task PostJsonMessageAsync(string jsonPayload)
        {
            if (_isReady && _webView != null)
            {
                _webView.PostWebMessageAsJson(jsonPayload);
                return Task.CompletedTask;
            }

            _pendingOperations.Enqueue(() =>
            {
                _webView?.PostWebMessageAsJson(jsonPayload);
                return Task.CompletedTask;
            });
            return Task.CompletedTask;
        }

        public async Task<string?> ExecuteScriptAsync(string javascript)
        {
            if (_isReady && _webView != null)
            {
                return await _webView.ExecuteScriptAsync(javascript);
            }

            var tcs = new TaskCompletionSource<string?>();
            _pendingOperations.Enqueue(async () =>
            {
                if (_webView != null)
                {
                    string res = await _webView.ExecuteScriptAsync(javascript);
                    tcs.TrySetResult(res);
                }
                else
                {
                    tcs.TrySetResult(null);
                }
            });
            return await tcs.Task;
        }

        public Task ReloadAsync()
        {
            if (_isReady && _webView != null)
            {
                _webView.Reload();
            }
            return Task.CompletedTask;
        }

        private static class Win32Interop
        {
            public const int WS_CHILD = 0x40000000;
            public const int WS_VISIBLE = 0x10000000;
            public const int WS_CLIPCHILDREN = 0x02000000;

            [DllImport("user32.dll", CharSet = CharSet.Unicode, SetLastError = true)]
            public static extern IntPtr CreateWindowExW(
                int dwExStyle,
                string lpClassName,
                string lpWindowName,
                int dwStyle,
                int x,
                int y,
                int nWidth,
                int nHeight,
                IntPtr hWndParent,
                IntPtr hMenu,
                IntPtr hInstance,
                IntPtr lpParam);

            [DllImport("user32.dll", SetLastError = true)]
            [return: MarshalAs(UnmanagedType.Bool)]
            public static extern bool DestroyWindow(IntPtr hWnd);

            [DllImport("user32.dll", SetLastError = true)]
            [return: MarshalAs(UnmanagedType.Bool)]
            public static extern bool MoveWindow(IntPtr hWnd, int X, int Y, int nWidth, int nHeight, bool bRepaint);
        }
    }
}
