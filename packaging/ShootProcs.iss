#ifndef AppVersion
  #error AppVersion must be supplied by build.ps1.
#endif
#ifndef PublishDir
  #error PublishDir must be supplied by build.ps1.
#endif

[Setup]
AppId={{9325CD84-7B69-4583-9542-FD32A0823521}
AppName=ShootProcs
AppVersion={#AppVersion}
AppPublisher=jsburckhardt
AppPublisherURL=https://github.com/jsburckhardt/ShootProcs
AppSupportURL=https://github.com/jsburckhardt/ShootProcs/issues
AppUpdatesURL=https://github.com/jsburckhardt/ShootProcs/releases
DefaultDirName={localappdata}\Programs\ShootProcs
DefaultGroupName=ShootProcs
DisableProgramGroupPage=yes
PrivilegesRequired=lowest
ArchitecturesAllowed=x64compatible
ArchitecturesInstallIn64BitMode=x64compatible
MinVersion=10.0.22000
LicenseFile=..\LICENSE
SetupIconFile=..\src\ShootProcs.App\Assets\ShootProcs.ico
UninstallDisplayIcon={app}\ShootProcs.exe
OutputBaseFilename=ShootProcs-Setup
Compression=lzma2
SolidCompression=yes
WizardStyle=modern
CloseApplications=yes
CloseApplicationsFilter=ShootProcs.exe
RestartApplications=no

[Tasks]
Name: "desktopicon"; Description: "Create a &desktop shortcut"; Flags: unchecked

[Files]
Source: "{#PublishDir}\*"; DestDir: "{app}"; Flags: ignoreversion recursesubdirs createallsubdirs

[Icons]
Name: "{autoprograms}\ShootProcs"; Filename: "{app}\ShootProcs.exe"
Name: "{autodesktop}\ShootProcs"; Filename: "{app}\ShootProcs.exe"; Tasks: desktopicon

[Run]
Filename: "{app}\ShootProcs.exe"; Description: "Launch ShootProcs"; Flags: nowait postinstall skipifsilent
