; Inno Setup 6 script for the portable publish output.
; Build:
;   dotnet publish src/OpenCodeUsagePlus -c Release -r win-x64 --self-contained true `
;     -p:PublishSingleFile=true -p:IncludeNativeLibrariesForSelfExtract=true `
;     -p:PublishTrimmed=false -o publish
;   iscc packaging\installer.iss

#define AppName "OpenCode Usage Plus"
#define AppVersion "0.1.0"
#define AppExe "OpenCodeUsagePlus.exe"

[Setup]
AppId={{7F2A5B1E-9C3D-4E3B-9C2A-1D6B0C4E8F11}
AppName={#AppName}
AppVersion={#AppVersion}
AppPublisher=OpenCode Usage Plus contributors
DefaultDirName={localappdata}\Programs\OpenCode Usage Plus
DefaultGroupName={#AppName}
PrivilegesRequired=lowest
DisableProgramGroupPage=yes
OutputDir=..\artifacts
OutputBaseFilename=OpenCodeUsagePlus-{#AppVersion}-setup
Compression=lzma2
SolidCompression=yes
ArchitecturesAllowed=x64compatible
ArchitecturesInstallIn64BitMode=x64compatible
WizardStyle=modern
; Uncomment when signing:
; SignTool=signtool

[Tasks]
Name: "autostart"; Description: "Start with Windows"; GroupDescription: "Startup:"; Flags: unchecked

[Files]
Source: "..\publish\{#AppExe}"; DestDir: "{app}"; Flags: ignoreversion

[Icons]
Name: "{group}\{#AppName}"; Filename: "{app}\{#AppExe}"
Name: "{group}\Uninstall {#AppName}"; Filename: "{uninstallexe}"

[Registry]
Root: HKCU; Subkey: "Software\Microsoft\Windows\CurrentVersion\Run"; ValueType: string; \
  ValueName: "OpenCodeUsagePlus"; ValueData: """{app}\{#AppExe}"" --tray"; \
  Tasks: autostart; Flags: uninsdeletevalue

[Run]
Filename: "{app}\{#AppExe}"; Parameters: "--tray"; Description: "Launch {#AppName}"; Flags: nowait postinstall skipifsilent

[UninstallDelete]
Type: filesandordirs; Name: "{localappdata}\OpenCode Usage Plus"
