; Nexus installer (Inno Setup 6). Built automatically by the GitHub workflow.
; Parameters passed by the workflow: /DStage=<pasta com os arquivos> /DIcon=<arquivo .ico> /DOutDir=<pasta de saida>

#define AppName "Nexus"
#define AppVersion "1.60"

[Setup]
AppId={{6D1C5E0B-4C7A-4B9E-9F3A-7E2B5A9C1D44}
AppName={#AppName}
AppVersion={#AppVersion}
AppPublisher=Nexus
DefaultDirName={autopf}\{#AppName}
DefaultGroupName={#AppName}
DisableProgramGroupPage=yes
PrivilegesRequired=lowest
PrivilegesRequiredOverridesAllowed=dialog
ArchitecturesAllowed=x64compatible
ArchitecturesInstallIn64BitMode=x64compatible
Compression=lzma2
SolidCompression=yes
WizardStyle=modern
SetupIconFile={#Icon}
UninstallDisplayIcon={app}\Nexus.exe
OutputDir={#OutDir}
OutputBaseFilename=Nexus-Setup

[Languages]
#if FileExists(CompilerPath + "Languages\BrazilianPortuguese.isl")
Name: "brazilianportuguese"; MessagesFile: "compiler:Languages\BrazilianPortuguese.isl"
#endif
Name: "english"; MessagesFile: "compiler:Default.isl"

[Tasks]
Name: "desktopicon"; Description: "Criar atalho na area de trabalho / Create a desktop shortcut"; GroupDescription: "Atalhos / Shortcuts:"

[Files]
Source: "{#Stage}\*"; DestDir: "{app}"; Flags: recursesubdirs ignoreversion

[Icons]
Name: "{autoprograms}\{#AppName}"; Filename: "{app}\Nexus.exe"
Name: "{autodesktop}\{#AppName}"; Filename: "{app}\Nexus.exe"; Tasks: desktopicon

[Run]
Filename: "{app}\Nexus.exe"; Description: "Abrir o Nexus / Launch Nexus"; Flags: nowait postinstall skipifsilent
