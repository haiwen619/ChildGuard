#ifndef MyAppVersion
#define MyAppVersion "1.0.7"
#endif

#ifndef MyAppSourceDir
#define MyAppSourceDir "..\publish\ChildGuard-v1.0.7-win-x64"
#endif

#ifndef MyOutputBaseFilename
#define MyOutputBaseFilename "ChildGuard-v1.0.7-Setup"
#endif

[Setup]
AppId={{5A8E8D2E-139B-4D88-99AF-7A08B019E0A1}
AppName=童伴守护 (ChildGuard)
AppVersion={#MyAppVersion}
AppPublisher=海文娜AI
AppPublisherURL=https://github.com/haiwen619/ChildGuard
AppSupportURL=https://github.com/haiwen619/ChildGuard/issues
AppUpdatesURL=https://github.com/haiwen619/ChildGuard/releases
DefaultDirName={autopf}\ChildGuard
DefaultGroupName=童伴守护
AllowNoIcons=yes
OutputDir=..\publish
OutputBaseFilename={#MyOutputBaseFilename}
SetupIconFile=..\src\Haidou.ChildGuard.Wpf\Resources\logo.ico
Compression=lzma2/ultra64
SolidCompression=yes
WizardStyle=modern
PrivilegesRequired=admin
ArchitecturesInstallIn64BitMode=x64compatible
UninstallDisplayIcon={app}\Resources\logo.ico
CloseApplications=force
RestartApplications=no
VersionInfoVersion={#MyAppVersion}
VersionInfoCompany=海文娜AI
VersionInfoDescription=童伴守护 (ChildGuard) 安装向导
VersionInfoTextVersion={#MyAppVersion}
VersionInfoCopyright=Copyright (C) 2026 海文娜AI. All rights reserved.
VersionInfoProductName=童伴守护 (ChildGuard)
VersionInfoProductVersion={#MyAppVersion}

[Languages]
Name: "english"; MessagesFile: "compiler:Default.isl"

[Tasks]
Name: "desktopicon"; Description: "{cm:CreateDesktopIcon}"; GroupDescription: "{cm:AdditionalIcons}"; Flags: unchecked

[InstallDelete]
Type: files; Name: "{app}\coreclr.dll"
Type: files; Name: "{app}\Haidou.ChildGuard.Wpf.exe"
Type: files; Name: "{app}\Haidou.Watchdog.exe"

[Files]
Source: "{#MyAppSourceDir}\*"; DestDir: "{app}"; Flags: ignoreversion recursesubdirs createallsubdirs

[Icons]
Name: "{group}\童伴守护"; Filename: "{app}\Haidou.ChildGuard.Wpf.exe"; IconFilename: "{app}\Resources\logo.ico"
Name: "{group}\{cm:UninstallProgram,童伴守护}"; Filename: "{uninstallexe}"
Name: "{autodesktop}\童伴守护"; Filename: "{app}\Haidou.ChildGuard.Wpf.exe"; IconFilename: "{app}\Resources\logo.ico"; Tasks: desktopicon

[Run]
Filename: "{app}\Haidou.ChildGuard.Wpf.exe"; Description: "{cm:LaunchProgram,童伴守护}"; Flags: nowait postinstall skipifsilent runascurrentuser

[Code]
function InitializeSetup(): Boolean;
var
  ErrorCode: Integer;
begin
  // 安装前静默终止正在运行的旧进程，确保文件不被锁定占用
  ShellExec('open', 'taskkill.exe', '/F /IM Haidou.ChildGuard.Wpf.exe /IM Haidou.Watchdog.exe', '', SW_HIDE, ewWaitUntilTerminated, ErrorCode);
  Result := True;
end;

function InitializeUninstall(): Boolean;
var
  ErrorCode: Integer;
begin
  // 卸载前强制终止后台守护进程
  ShellExec('open', 'taskkill.exe', '/F /IM Haidou.ChildGuard.Wpf.exe /IM Haidou.Watchdog.exe', '', SW_HIDE, ewWaitUntilTerminated, ErrorCode);
  Result := True;
end;