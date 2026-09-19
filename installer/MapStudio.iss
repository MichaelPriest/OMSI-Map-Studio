#ifndef AppVersion
  #define AppVersion "0.1.0-alpha.3-dev"
#endif

#ifndef SourceDir
  #define SourceDir "..\\artifacts\\publish\\win-x64"
#endif

#ifndef OutputDir
  #define OutputDir "..\\artifacts"
#endif

#define AppName "OMSI Map Studio"
#define AppExeName "OMSI Map Studio.exe"
#define AppPublisher "MichaelPriest"
#define AppUrl "https://github.com/MichaelPriest/OMSI-Map-Studio"
#define SetupFileName "OMSI-Map-Studio-Setup-" + AppVersion + "-win-x64"

[Setup]
AppId={{AA7B4DBA-96E3-49CB-AE2F-F559A5B172E7}
AppName={#AppName}
AppVersion={#AppVersion}
AppPublisher={#AppPublisher}
AppPublisherURL={#AppUrl}
AppSupportURL={#AppUrl}
AppUpdatesURL={#AppUrl}
DefaultDirName={localappdata}\\Programs\\OMSI Map Studio
DefaultGroupName=OMSI Map Studio
DisableProgramGroupPage=yes
PrivilegesRequired=lowest
PrivilegesRequiredOverridesAllowed=dialog
ArchitecturesAllowed=x64compatible
ArchitecturesInstallIn64BitMode=x64compatible
OutputDir={#OutputDir}
OutputBaseFilename={#SetupFileName}
SetupIconFile=..\\src\\MapStudio.Desktop\\Assets\\MapStudio.ico
UninstallDisplayIcon={app}\\{#AppExeName}
Compression=lzma2/ultra64
SolidCompression=yes
WizardStyle=modern
CloseApplications=force
RestartApplications=no
VersionInfoDescription=OMSI Map Studio Setup
VersionInfoProductName=OMSI Map Studio
VersionInfoCompany=MichaelPriest
VersionInfoCopyright=OMSI Map Studio
MinVersion=10.0.17763

[Languages]
Name: "brazilianportuguese"; MessagesFile: "compiler:Languages\\BrazilianPortuguese.isl"
Name: "english"; MessagesFile: "compiler:Default.isl"

[CustomMessages]
brazilianportuguese.UninstallShortcut=Desinstalar OMSI Map Studio
english.UninstallShortcut=Uninstall OMSI Map Studio

[Tasks]
Name: "desktopicon"; Description: "{cm:CreateDesktopIcon}"; GroupDescription: "{cm:AdditionalIcons}"; Flags: unchecked

[Files]
Source: "{#SourceDir}\\*"; DestDir: "{app}"; Flags: ignoreversion recursesubdirs createallsubdirs

[Icons]
Name: "{autoprograms}\\OMSI Map Studio"; Filename: "{app}\\{#AppExeName}"; IconFilename: "{app}\\{#AppExeName}"
Name: "{autodesktop}\\OMSI Map Studio"; Filename: "{app}\\{#AppExeName}"; IconFilename: "{app}\\{#AppExeName}"; Tasks: desktopicon

[Run]
Filename: "{app}\\{#AppExeName}"; Description: "{cm:LaunchProgram,OMSI Map Studio}"; Flags: nowait postinstall skipifsilent