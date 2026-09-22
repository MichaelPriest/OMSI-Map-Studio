#ifndef AppVersion
  #define AppVersion "0.2.0-alpha.5-test.10.3-native"
#endif

#ifndef SourceDir
  #define SourceDir "..\artifacts\publish\win-x64"
#endif

#ifndef OutputDir
  #define OutputDir "..\artifacts"
#endif

#define AppName "OMSI Map Studio Native Preview"
#define AppExeName "MapStudio.Native.exe"
#define AppPublisher "MichaelPriest"
#define AppUrl "https://github.com/MichaelPriest/OMSI-Map-Studio"
#define SetupFileName "OMSI-Map-Studio-Native-Preview-Setup-" + AppVersion + "-win-x64"

[Setup]
AppId={{D8F56E5B-E90F-4CB9-9ECA-1B6E9832ED34}
AppName={#AppName}
AppVersion={#AppVersion}
AppPublisher={#AppPublisher}
AppPublisherURL={#AppUrl}
AppSupportURL={#AppUrl}
AppUpdatesURL={#AppUrl}
DefaultDirName={localappdata}\Programs\OMSI Map Studio Native Preview
DefaultGroupName=OMSI Map Studio Native Preview
DisableProgramGroupPage=yes
PrivilegesRequired=lowest
PrivilegesRequiredOverridesAllowed=dialog
ArchitecturesAllowed=x64compatible
ArchitecturesInstallIn64BitMode=x64compatible
OutputDir={#OutputDir}
OutputBaseFilename={#SetupFileName}
SetupIconFile=..\src\MapStudio.Desktop\Assets\MapStudio.ico
UninstallDisplayIcon={app}\Assets\MapStudio.ico
Compression=lzma2/ultra64
SolidCompression=yes
WizardStyle=modern
CloseApplications=force
RestartApplications=no
VersionInfoDescription=OMSI Map Studio Native Preview Setup
VersionInfoProductName=OMSI Map Studio Native Preview
VersionInfoCompany=MichaelPriest
MinVersion=10.0.17763

[Languages]
Name: "brazilianportuguese"; MessagesFile: "compiler:Languages\BrazilianPortuguese.isl"
Name: "english"; MessagesFile: "compiler:Default.isl"

[CustomMessages]
brazilianportuguese.UninstallShortcut=Desinstalar OMSI Map Studio Native Preview
english.UninstallShortcut=Uninstall OMSI Map Studio Native Preview

[Tasks]
Name: "desktopicon"; Description: "{cm:CreateDesktopIcon}"; GroupDescription: "{cm:AdditionalIcons}"; Flags: unchecked

[Files]
Source: "{#SourceDir}\*"; DestDir: "{app}"; Flags: ignoreversion recursesubdirs createallsubdirs

[Icons]
Name: "{autoprograms}\OMSI Map Studio Native Preview"; Filename: "{app}\{#AppExeName}"; IconFilename: "{app}\Assets\MapStudio.ico"
Name: "{autodesktop}\OMSI Map Studio Native Preview"; Filename: "{app}\{#AppExeName}"; IconFilename: "{app}\Assets\MapStudio.ico"; Tasks: desktopicon

[Run]
Filename: "{app}\{#AppExeName}"; Description: "{cm:LaunchProgram,OMSI Map Studio Native Preview}"; Flags: nowait postinstall skipifsilent
