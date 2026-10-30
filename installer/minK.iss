; minK Windows Installer Script (Inno Setup 6)
; Builds a self-contained win-x64 setup package with CLI PATH hook,
; Explorer context menus, Windows Default Apps Capabilities, and Open With ProgIDs.

#define MyAppName "minK"
#ifndef MyAppVersion
  #define MyAppVersion "1.0.0"
#endif
#define MyAppPublisher "minK"
#define MyAppExeName "minK.exe"
#ifndef PublishDir
  #define PublishDir "staging\win-x64"
#endif
#ifndef OutputDirectory
  #define OutputDirectory "..\dist"
#endif

[Setup]
AppId={{8D4F6C2E-51B9-4E7A-9C31-6A2F0E8B4D15}
AppName={#MyAppName}
AppVersion={#MyAppVersion}
AppVerName={#MyAppName} {#MyAppVersion}
AppPublisher={#MyAppPublisher}
DefaultDirName={autopf}\{#MyAppName}
DefaultGroupName={#MyAppName}
DisableProgramGroupPage=yes
PrivilegesRequired=lowest
PrivilegesRequiredOverridesAllowed=dialog
ArchitecturesAllowed=x64compatible
ArchitecturesInstallIn64BitMode=x64compatible
ChangesAssociations=yes
ChangesEnvironment=yes
CloseApplications=yes
SetupIconFile=..\minK\Assets\mink.ico
UninstallDisplayIcon={app}\{#MyAppExeName}
UninstallDisplayName={#MyAppName}
WizardStyle=modern
Compression=lzma2/ultra64
SolidCompression=yes
OutputDir={#OutputDirectory}
OutputBaseFilename=minK-Setup-{#MyAppVersion}-win-x64

[Languages]
Name: "english"; MessagesFile: "compiler:Default.isl"

[Tasks]
Name: "desktopicon"; Description: "{cm:CreateDesktopIcon}"; GroupDescription: "{cm:AdditionalIcons}"; Flags: unchecked
Name: "contextmenufiles"; Description: "Add ""Open with minK"" action to Windows Explorer file context menu"; GroupDescription: "Windows Explorer integration:"
Name: "contextmenufolders"; Description: "Add ""Open with minK"" action to Windows Explorer directory context menu"; GroupDescription: "Windows Explorer integration:"
Name: "associate"; Description: "Register minK as an editor for supported file types (Default Apps && Open With)"; GroupDescription: "Editor associations:"
Name: "defaultcode"; Description: "Set minK as default editor for code && config files (.css, .js, .ts, .json, .md, .yaml, .py, .xml)"; GroupDescription: "Editor associations:"; Flags: unchecked
Name: "defaulthtml"; Description: "Set minK as default editor for HTML files (.html, .htm)"; GroupDescription: "Editor associations:"; Flags: unchecked
Name: "addtopath"; Description: "Add ""mink"" to PATH (enables running ""mink"" from PowerShell, Command Prompt, and Git Bash)"; GroupDescription: "Command line:"

[Files]
Source: "{#PublishDir}\*"; DestDir: "{app}"; Flags: ignoreversion recursesubdirs createallsubdirs
Source: "bin\mink.cmd"; DestDir: "{app}\bin"; Flags: ignoreversion
Source: "bin\mink"; DestDir: "{app}\bin"; Flags: ignoreversion

[Icons]
Name: "{autoprograms}\{#MyAppName}"; Filename: "{app}\{#MyAppExeName}"; Comment: "Minimalist local-first web and code editor"
Name: "{autodesktop}\{#MyAppName}"; Filename: "{app}\{#MyAppExeName}"; Comment: "Minimalist local-first web and code editor"; Tasks: desktopicon

[Registry]
; --- App Paths (enables Win+R -> mink / minK) ---
Root: HKA; Subkey: "Software\Microsoft\Windows\CurrentVersion\App Paths\minK.exe"; ValueType: string; ValueName: ""; ValueData: "{app}\{#MyAppExeName}"; Flags: uninsdeletekey
Root: HKA; Subkey: "Software\Microsoft\Windows\CurrentVersion\App Paths\minK.exe"; ValueType: string; ValueName: "Path"; ValueData: "{app}"; Flags: uninsdeletekey
Root: HKA; Subkey: "Software\Microsoft\Windows\CurrentVersion\App Paths\mink.cmd"; ValueType: string; ValueName: ""; ValueData: "{app}\bin\mink.cmd"; Flags: uninsdeletekey

; --- Explorer Context Menus ("Open with minK") ---
Root: HKA; Subkey: "Software\Classes\*\shell\minK"; ValueType: string; ValueName: ""; ValueData: "Open with minK"; Flags: uninsdeletekey; Tasks: contextmenufiles
Root: HKA; Subkey: "Software\Classes\*\shell\minK"; ValueType: string; ValueName: "Icon"; ValueData: "{app}\{#MyAppExeName}"; Flags: uninsdeletekey; Tasks: contextmenufiles
Root: HKA; Subkey: "Software\Classes\*\shell\minK\command"; ValueType: string; ValueName: ""; ValueData: """{app}\{#MyAppExeName}"" ""%1"""; Flags: uninsdeletekey; Tasks: contextmenufiles

Root: HKA; Subkey: "Software\Classes\Directory\shell\minK"; ValueType: string; ValueName: ""; ValueData: "Open with minK"; Flags: uninsdeletekey; Tasks: contextmenufolders
Root: HKA; Subkey: "Software\Classes\Directory\shell\minK"; ValueType: string; ValueName: "Icon"; ValueData: "{app}\{#MyAppExeName}"; Flags: uninsdeletekey; Tasks: contextmenufolders
Root: HKA; Subkey: "Software\Classes\Directory\shell\minK\command"; ValueType: string; ValueName: ""; ValueData: """{app}\{#MyAppExeName}"" ""%V"""; Flags: uninsdeletekey; Tasks: contextmenufolders

Root: HKA; Subkey: "Software\Classes\Directory\Background\shell\minK"; ValueType: string; ValueName: ""; ValueData: "Open with minK"; Flags: uninsdeletekey; Tasks: contextmenufolders
Root: HKA; Subkey: "Software\Classes\Directory\Background\shell\minK"; ValueType: string; ValueName: "Icon"; ValueData: "{app}\{#MyAppExeName}"; Flags: uninsdeletekey; Tasks: contextmenufolders
Root: HKA; Subkey: "Software\Classes\Directory\Background\shell\minK\command"; ValueType: string; ValueName: ""; ValueData: """{app}\{#MyAppExeName}"" ""%V"""; Flags: uninsdeletekey; Tasks: contextmenufolders

; --- ProgIDs for minK ---
Root: HKA; Subkey: "Software\Classes\minK.SourceFile"; ValueType: string; ValueName: ""; ValueData: "minK Source File"; Flags: uninsdeletekey; Tasks: associate defaultcode
Root: HKA; Subkey: "Software\Classes\minK.SourceFile\DefaultIcon"; ValueType: string; ValueName: ""; ValueData: "{app}\{#MyAppExeName},0"; Flags: uninsdeletekey; Tasks: associate defaultcode
Root: HKA; Subkey: "Software\Classes\minK.SourceFile\shell\open"; ValueType: string; ValueName: "FriendlyAppName"; ValueData: "minK"; Flags: uninsdeletekey; Tasks: associate defaultcode
Root: HKA; Subkey: "Software\Classes\minK.SourceFile\shell\open"; ValueType: string; ValueName: "Icon"; ValueData: "{app}\{#MyAppExeName}"; Flags: uninsdeletekey; Tasks: associate defaultcode
Root: HKA; Subkey: "Software\Classes\minK.SourceFile\shell\open\command"; ValueType: string; ValueName: ""; ValueData: """{app}\{#MyAppExeName}"" ""%1"""; Flags: uninsdeletekey; Tasks: associate defaultcode

Root: HKA; Subkey: "Software\Classes\minK.WebDocument"; ValueType: string; ValueName: ""; ValueData: "minK Web Document"; Flags: uninsdeletekey; Tasks: associate defaulthtml
Root: HKA; Subkey: "Software\Classes\minK.WebDocument\DefaultIcon"; ValueType: string; ValueName: ""; ValueData: "{app}\{#MyAppExeName},0"; Flags: uninsdeletekey; Tasks: associate defaulthtml
Root: HKA; Subkey: "Software\Classes\minK.WebDocument\shell\open"; ValueType: string; ValueName: "FriendlyAppName"; ValueData: "minK"; Flags: uninsdeletekey; Tasks: associate defaulthtml
Root: HKA; Subkey: "Software\Classes\minK.WebDocument\shell\open"; ValueType: string; ValueName: "Icon"; ValueData: "{app}\{#MyAppExeName}"; Flags: uninsdeletekey; Tasks: associate defaulthtml
Root: HKA; Subkey: "Software\Classes\minK.WebDocument\shell\open\command"; ValueType: string; ValueName: ""; ValueData: """{app}\{#MyAppExeName}"" ""%1"""; Flags: uninsdeletekey; Tasks: associate defaulthtml

; --- Applications\minK.exe registration (Windows "Open with" dialog) ---
Root: HKA; Subkey: "Software\Classes\Applications\minK.exe"; ValueType: string; ValueName: "FriendlyAppName"; ValueData: "minK"; Flags: uninsdeletekey; Tasks: associate defaultcode defaulthtml
Root: HKA; Subkey: "Software\Classes\Applications\minK.exe\DefaultIcon"; ValueType: string; ValueName: ""; ValueData: "{app}\{#MyAppExeName},0"; Flags: uninsdeletekey; Tasks: associate defaultcode defaulthtml
Root: HKA; Subkey: "Software\Classes\Applications\minK.exe\shell\open\command"; ValueType: string; ValueName: ""; ValueData: """{app}\{#MyAppExeName}"" ""%1"""; Flags: uninsdeletekey; Tasks: associate defaultcode defaulthtml

#sub RegisterSupportedExtension
  Root: HKA; Subkey: "Software\Classes\Applications\minK.exe\SupportedTypes"; ValueType: string; ValueName: "{#Ext}"; ValueData: ""; Flags: uninsdeletevalue; Tasks: associate defaultcode defaulthtml
  Root: HKA; Subkey: "Software\minK\Capabilities\FileAssociations"; ValueType: string; ValueName: "{#Ext}"; ValueData: "{#ProgId}"; Flags: uninsdeletevalue; Tasks: associate defaultcode defaulthtml
  Root: HKA; Subkey: "Software\Classes\{#Ext}\OpenWithProgids"; ValueType: string; ValueName: "{#ProgId}"; ValueData: ""; Flags: uninsdeletevalue; Tasks: associate defaultcode defaulthtml
#endsub

; --- Windows 10 / 11 Default Apps Capabilities & RegisteredApplications ---
Root: HKA; Subkey: "Software\minK"; Flags: uninsdeletekeyifempty
Root: HKA; Subkey: "Software\minK\Capabilities"; ValueType: string; ValueName: "ApplicationName"; ValueData: "minK"; Flags: uninsdeletekey; Tasks: associate defaultcode defaulthtml
Root: HKA; Subkey: "Software\minK\Capabilities"; ValueType: string; ValueName: "ApplicationDescription"; ValueData: "minK — Minimalist local-first web and code editor"; Flags: uninsdeletekey; Tasks: associate defaultcode defaulthtml
Root: HKA; Subkey: "Software\minK\Capabilities"; ValueType: string; ValueName: "ApplicationIcon"; ValueData: "{app}\{#MyAppExeName},0"; Flags: uninsdeletekey; Tasks: associate defaultcode defaulthtml
Root: HKA; Subkey: "Software\RegisteredApplications"; ValueType: string; ValueName: "minK"; ValueData: "Software\minK\Capabilities"; Flags: uninsdeletevalue; Tasks: associate defaultcode defaulthtml

; Web document extensions
#define ProgId "minK.WebDocument"
#define Ext ".html"
#emit RegisterSupportedExtension
#define Ext ".htm"
#emit RegisterSupportedExtension
#define Ext ".xhtml"
#emit RegisterSupportedExtension

; Code, stylesheet, markup, data, script, and plain-text extensions
#define ProgId "minK.SourceFile"
#define Ext ".css"
#emit RegisterSupportedExtension
#define Ext ".scss"
#emit RegisterSupportedExtension
#define Ext ".less"
#emit RegisterSupportedExtension
#define Ext ".js"
#emit RegisterSupportedExtension
#define Ext ".mjs"
#emit RegisterSupportedExtension
#define Ext ".cjs"
#emit RegisterSupportedExtension
#define Ext ".jsx"
#emit RegisterSupportedExtension
#define Ext ".ts"
#emit RegisterSupportedExtension
#define Ext ".tsx"
#emit RegisterSupportedExtension
#define Ext ".json"
#emit RegisterSupportedExtension
#define Ext ".jsonc"
#emit RegisterSupportedExtension
#define Ext ".webmanifest"
#emit RegisterSupportedExtension
#define Ext ".xml"
#emit RegisterSupportedExtension
#define Ext ".svg"
#emit RegisterSupportedExtension
#define Ext ".md"
#emit RegisterSupportedExtension
#define Ext ".markdown"
#emit RegisterSupportedExtension
#define Ext ".yaml"
#emit RegisterSupportedExtension
#define Ext ".yml"
#emit RegisterSupportedExtension
#define Ext ".py"
#emit RegisterSupportedExtension
#define Ext ".pyw"
#emit RegisterSupportedExtension
#define Ext ".sh"
#emit RegisterSupportedExtension
#define Ext ".ps1"
#emit RegisterSupportedExtension
#define Ext ".bat"
#emit RegisterSupportedExtension
#define Ext ".cmd"
#emit RegisterSupportedExtension
#define Ext ".ini"
#emit RegisterSupportedExtension
#define Ext ".cfg"
#emit RegisterSupportedExtension
#define Ext ".conf"
#emit RegisterSupportedExtension
#define Ext ".toml"
#emit RegisterSupportedExtension
#define Ext ".env"
#emit RegisterSupportedExtension
#define Ext ".editorconfig"
#emit RegisterSupportedExtension
#define Ext ".sql"
#emit RegisterSupportedExtension
#define Ext ".cs"
#emit RegisterSupportedExtension
#define Ext ".txt"
#emit RegisterSupportedExtension
#define Ext ".log"
#emit RegisterSupportedExtension

; --- Optional Direct Default File Type Ownership ---
Root: HKA; Subkey: "Software\Classes\.html"; ValueType: string; ValueName: ""; ValueData: "minK.WebDocument"; Flags: uninsdeletevalue; Tasks: defaulthtml
Root: HKA; Subkey: "Software\Classes\.htm"; ValueType: string; ValueName: ""; ValueData: "minK.WebDocument"; Flags: uninsdeletevalue; Tasks: defaulthtml
Root: HKA; Subkey: "Software\Classes\.xhtml"; ValueType: string; ValueName: ""; ValueData: "minK.WebDocument"; Flags: uninsdeletevalue; Tasks: defaulthtml

Root: HKA; Subkey: "Software\Classes\.css"; ValueType: string; ValueName: ""; ValueData: "minK.SourceFile"; Flags: uninsdeletevalue; Tasks: defaultcode
Root: HKA; Subkey: "Software\Classes\.scss"; ValueType: string; ValueName: ""; ValueData: "minK.SourceFile"; Flags: uninsdeletevalue; Tasks: defaultcode
Root: HKA; Subkey: "Software\Classes\.less"; ValueType: string; ValueName: ""; ValueData: "minK.SourceFile"; Flags: uninsdeletevalue; Tasks: defaultcode
Root: HKA; Subkey: "Software\Classes\.js"; ValueType: string; ValueName: ""; ValueData: "minK.SourceFile"; Flags: uninsdeletevalue; Tasks: defaultcode
Root: HKA; Subkey: "Software\Classes\.mjs"; ValueType: string; ValueName: ""; ValueData: "minK.SourceFile"; Flags: uninsdeletevalue; Tasks: defaultcode
Root: HKA; Subkey: "Software\Classes\.cjs"; ValueType: string; ValueName: ""; ValueData: "minK.SourceFile"; Flags: uninsdeletevalue; Tasks: defaultcode
Root: HKA; Subkey: "Software\Classes\.jsx"; ValueType: string; ValueName: ""; ValueData: "minK.SourceFile"; Flags: uninsdeletevalue; Tasks: defaultcode
Root: HKA; Subkey: "Software\Classes\.ts"; ValueType: string; ValueName: ""; ValueData: "minK.SourceFile"; Flags: uninsdeletevalue; Tasks: defaultcode
Root: HKA; Subkey: "Software\Classes\.tsx"; ValueType: string; ValueName: ""; ValueData: "minK.SourceFile"; Flags: uninsdeletevalue; Tasks: defaultcode
Root: HKA; Subkey: "Software\Classes\.json"; ValueType: string; ValueName: ""; ValueData: "minK.SourceFile"; Flags: uninsdeletevalue; Tasks: defaultcode
Root: HKA; Subkey: "Software\Classes\.jsonc"; ValueType: string; ValueName: ""; ValueData: "minK.SourceFile"; Flags: uninsdeletevalue; Tasks: defaultcode
Root: HKA; Subkey: "Software\Classes\.webmanifest"; ValueType: string; ValueName: ""; ValueData: "minK.SourceFile"; Flags: uninsdeletevalue; Tasks: defaultcode
Root: HKA; Subkey: "Software\Classes\.xml"; ValueType: string; ValueName: ""; ValueData: "minK.SourceFile"; Flags: uninsdeletevalue; Tasks: defaultcode
Root: HKA; Subkey: "Software\Classes\.md"; ValueType: string; ValueName: ""; ValueData: "minK.SourceFile"; Flags: uninsdeletevalue; Tasks: defaultcode
Root: HKA; Subkey: "Software\Classes\.markdown"; ValueType: string; ValueName: ""; ValueData: "minK.SourceFile"; Flags: uninsdeletevalue; Tasks: defaultcode
Root: HKA; Subkey: "Software\Classes\.yaml"; ValueType: string; ValueName: ""; ValueData: "minK.SourceFile"; Flags: uninsdeletevalue; Tasks: defaultcode
Root: HKA; Subkey: "Software\Classes\.yml"; ValueType: string; ValueName: ""; ValueData: "minK.SourceFile"; Flags: uninsdeletevalue; Tasks: defaultcode
Root: HKA; Subkey: "Software\Classes\.py"; ValueType: string; ValueName: ""; ValueData: "minK.SourceFile"; Flags: uninsdeletevalue; Tasks: defaultcode
Root: HKA; Subkey: "Software\Classes\.ini"; ValueType: string; ValueName: ""; ValueData: "minK.SourceFile"; Flags: uninsdeletevalue; Tasks: defaultcode
Root: HKA; Subkey: "Software\Classes\.cfg"; ValueType: string; ValueName: ""; ValueData: "minK.SourceFile"; Flags: uninsdeletevalue; Tasks: defaultcode
Root: HKA; Subkey: "Software\Classes\.conf"; ValueType: string; ValueName: ""; ValueData: "minK.SourceFile"; Flags: uninsdeletevalue; Tasks: defaultcode
Root: HKA; Subkey: "Software\Classes\.toml"; ValueType: string; ValueName: ""; ValueData: "minK.SourceFile"; Flags: uninsdeletevalue; Tasks: defaultcode
Root: HKA; Subkey: "Software\Classes\.env"; ValueType: string; ValueName: ""; ValueData: "minK.SourceFile"; Flags: uninsdeletevalue; Tasks: defaultcode
Root: HKA; Subkey: "Software\Classes\.sql"; ValueType: string; ValueName: ""; ValueData: "minK.SourceFile"; Flags: uninsdeletevalue; Tasks: defaultcode

[Run]
Filename: "{app}\{#MyAppExeName}"; Description: "{cm:LaunchProgram,{#StringChange(MyAppName, '&', '&&')}}"; Flags: nowait postinstall skipifsilent

[Code]
procedure GetEnvRootAndKey(var RootKey: Integer; var SubKeyName: String);
begin
  if IsAdminInstallMode then
  begin
    RootKey := HKEY_LOCAL_MACHINE;
    SubKeyName := 'SYSTEM\CurrentControlSet\Control\Session Manager\Environment';
  end
  else
  begin
    RootKey := HKEY_CURRENT_USER;
    SubKeyName := 'Environment';
  end;
end;

procedure EnvAddPath(PathToAdd: String);
var
  RootKey: Integer;
  SubKeyName: String;
  Paths: String;
begin
  GetEnvRootAndKey(RootKey, SubKeyName);
  if not RegQueryStringValue(RootKey, SubKeyName, 'Path', Paths) then
    Paths := '';

  if Pos(';' + Uppercase(PathToAdd) + ';', ';' + Uppercase(Paths) + ';') > 0 then
    Exit;

  if (Paths <> '') and (Paths[Length(Paths)] <> ';') then
    Paths := Paths + ';';

  Paths := Paths + PathToAdd;
  RegWriteExpandStringValue(RootKey, SubKeyName, 'Path', Paths);
end;

procedure EnvRemovePath(PathToRemove: String);
var
  RootKey: Integer;
  SubKeyName: String;
  Paths: String;
  P: Integer;
begin
  GetEnvRootAndKey(RootKey, SubKeyName);
  if not RegQueryStringValue(RootKey, SubKeyName, 'Path', Paths) then
    Exit;

  P := Pos(';' + Uppercase(PathToRemove) + ';', ';' + Uppercase(Paths) + ';');
  if P = 0 then
    Exit;

  if P = 1 then
  begin
    Delete(Paths, 1, Length(PathToRemove));
    if (Length(Paths) > 0) and (Paths[1] = ';') then
      Delete(Paths, 1, 1);
  end
  else
  begin
    Delete(Paths, P - 1, Length(PathToRemove) + 1);
  end;

  RegWriteExpandStringValue(RootKey, SubKeyName, 'Path', Paths);
end;

procedure CurStepChanged(CurStep: TSetupStep);
begin
  if (CurStep = ssPostInstall) and WizardIsTaskSelected('addtopath') then
  begin
    EnvAddPath(ExpandConstant('{app}\bin'));
  end;
end;

procedure CurUninstallStepChanged(CurUninstallStep: TUninstallStep);
begin
  if CurUninstallStep = usUninstall then
  begin
    EnvRemovePath(ExpandConstant('{app}\bin'));
  end;
end;
