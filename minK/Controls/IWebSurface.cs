using System;
using System.Threading.Tasks;

namespace mink.Controls
{
    /// <summary>
    /// Cross-platform abstraction over the embedded Chromium / OS web rendering engine.
    /// Implemented via WebView2 on Windows, extensible to CEF / WebKitGTK on Linux and WKWebView on macOS.
    /// </summary>
    public interface IWebSurface
    {
        bool IsReady { get; }
        event EventHandler? SurfaceReady;
        event EventHandler<string>? WebMessageReceived;

        void MapLocalFolder(string virtualHostName, string folderPath);
        Task NavigateAsync(string url);
        Task NavigateToStringAsync(string htmlContent);
        Task PostJsonMessageAsync(string jsonPayload);
        Task<string?> ExecuteScriptAsync(string javascript);
        Task ReloadAsync();
    }
}
