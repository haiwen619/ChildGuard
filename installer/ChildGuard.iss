#ifndef MyAppVersion
#define MyAppVersion "1.0.0"
#endif

#ifndef MyAppSourceDir
#define MyAppSourceDir "..\publish\ChildGuard-v1.0.0-win-x64"
#endif

#ifndef MyOutputBaseFilename
#define MyOutputBaseFilename "ChildGuard-v1.0.0-Setup"
#endif

[Setup]
AppId={{5A8E8D2E-139B-4D88-99AF-7A08B019E0A1}
AppName=童伴守护 (ChildGuard)
AppVersion={#MyAppVersion}
AppPublisher=haiwennaAI
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

[Languages]
Name: "english"; MessagesFile: "compiler:Default.isl"

[Tasks]
Name: "desktopicon"; Description: "{cm:CreateDesktopIcon}"; GroupDescription: "{cm:AdditionalIcons}"; Flags: unchecked

[Files]
Source: "{#MyAppSourceDir}\*"; DestDir: "{app}"; Flags: ignoreversion recursesubdirs createallsubdirs

[Icons]
Name: "{group}\童伴守护"; Filename: "{app}\Haidou.ChildGuard.Wpf.exe"; IconFilename: "{app}\Resources\logo.ico"
Name: "{group}\{cm:UninstallProgram,童伴守护}"; Filename: "{uninstallexe}"
Name: "{autodesktop}\童伴守护"; Filename: "{app}\Haidou.ChildGuard.Wpf.exe"; IconFilename: "{app}\Resources\logo.ico"; Tasks: desktopicon

[Run]
Filename: "{app}\Haidou.ChildGuard.Wpf.exe"; Description: "{cm:LaunchProgram,童伴守护}"; Flags: nowait postinstall skipifsilent
