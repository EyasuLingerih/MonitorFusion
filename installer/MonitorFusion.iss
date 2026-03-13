; ============================================================
;  MonitorFusion Installer — InnoSetup 6.x
;  Build: run scripts\build-installer.bat first to publish
;         the app, then compile this script with ISCC.exe
; ============================================================

#define AppName      "MonitorFusion"
#define AppVersion   "1.0.0"
#define AppPublisher "EyasuLingerih"
#define AppURL       "https://github.com/EyasuLingerih/MonitorFusion"
#define AppExeName   "MonitorFusion.exe"
#define PublishDir   "..\publish"

[Setup]
AppId={{A3F72D1E-4B8C-4A2D-9F3E-1C2D3E4F5A6B}
AppName={#AppName}
AppVersion={#AppVersion}
AppVerName={#AppName} {#AppVersion}
AppPublisher={#AppPublisher}
AppPublisherURL={#AppURL}
AppSupportURL={#AppURL}/issues
AppUpdatesURL={#AppURL}/releases
DefaultDirName={autopf}\{#AppName}
DefaultGroupName={#AppName}
AllowNoIcons=yes
; Self-contained publish = no .NET prerequisite needed
CloseApplications=yes
CloseApplicationsFilter=*{#AppExeName}*
RestartApplications=no
OutputDir=output
OutputBaseFilename=MonitorFusion-Setup-{#AppVersion}
SetupIconFile=..\src\MonitorFusion.App\Assets\tray-icon.ico
Compression=lzma2/ultra64
SolidCompression=yes
WizardStyle=modern
ArchitecturesInstallIn64BitMode=x64compatible
MinVersion=10.0.17763
UninstallDisplayIcon={app}\{#AppExeName}
UninstallDisplayName={#AppName}
PrivilegesRequired=lowest
PrivilegesRequiredOverridesAllowed=commandline

[Languages]
Name: "english"; MessagesFile: "compiler:Default.isl"

[Tasks]
Name: "desktopicon";  Description: "{cm:CreateDesktopIcon}";            GroupDescription: "{cm:AdditionalIcons}"; Flags: unchecked
Name: "startupentry"; Description: "Start {#AppName} when Windows starts"; GroupDescription: "Startup:";
Name: "contextmenu";  Description: "Add 'Open {#AppName}' to desktop right-click menu"; GroupDescription: "Shell Integration:"

[Files]
; All self-contained publish output (single exe + any extracted native libs)
Source: "{#PublishDir}\*"; DestDir: "{app}"; Flags: ignoreversion recursesubdirs createallsubdirs

[Icons]
Name: "{group}\{#AppName}";           Filename: "{app}\{#AppExeName}"
Name: "{group}\Uninstall {#AppName}"; Filename: "{uninstallexe}"
Name: "{autodesktop}\{#AppName}";     Filename: "{app}\{#AppExeName}"; Tasks: desktopicon

[Registry]
; ── Startup (HKCU — no admin needed) ──────────────────────────────────────
Root: HKCU; Subkey: "SOFTWARE\Microsoft\Windows\CurrentVersion\Run"; ValueType: string; ValueName: "{#AppName}"; ValueData: """{app}\{#AppExeName}"""; Tasks: startupentry; Flags: uninsdeletevalue

; ── Desktop right-click context menu (HKCU\SOFTWARE\Classes merges into HKCR) ──
Root: HKCU; Subkey: "SOFTWARE\Classes\DesktopBackground\Shell\{#AppName}"; ValueType: string; ValueName: ""; ValueData: "Open {#AppName}"; Tasks: contextmenu
Root: HKCU; Subkey: "SOFTWARE\Classes\DesktopBackground\Shell\{#AppName}"; ValueType: string; ValueName: "Icon"; ValueData: """{app}\{#AppExeName}"",0"; Tasks: contextmenu
Root: HKCU; Subkey: "SOFTWARE\Classes\DesktopBackground\Shell\{#AppName}\command"; ValueType: string; ValueName: ""; ValueData: """{app}\{#AppExeName}"""; Tasks: contextmenu; Flags: uninsdeletekey

[Run]
Filename: "{app}\{#AppExeName}"; Description: "{cm:LaunchProgram,{#AppName}}"; Flags: nowait postinstall skipifsilent

[UninstallRun]
; Kill the running instance gracefully before uninstalling
Filename: "taskkill"; Parameters: "/F /IM {#AppExeName}"; Flags: runhidden; RunOnceId: "KillApp"

[UninstallDelete]
; Remove settings folder from AppData if the user confirms (via Code section)
Type: filesandordirs; Name: "{app}"

[Code]
// ── Ask whether to delete user settings on uninstall ─────────────────────
procedure CurUninstallStepChanged(CurUninstallStep: TUninstallStep);
var
  SettingsDir: string;
begin
  if CurUninstallStep = usUninstall then
  begin
    SettingsDir := ExpandConstant('{userappdata}\MonitorFusion');
    if DirExists(SettingsDir) then
    begin
      if MsgBox(
        'Do you want to delete your MonitorFusion settings and profiles?' + #13#10 +
        '(' + SettingsDir + ')',
        mbConfirmation, MB_YESNO) = IDYES then
      begin
        DelTree(SettingsDir, True, True, True);
      end;
    end;
  end;
end;
