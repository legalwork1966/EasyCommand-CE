[Setup]
AppName=Easy Command - Community Edition
AppVersion=1.0.0
AppPublisher=Starlight IT Solutions
DefaultDirName={autopf}\EasyCommand
DefaultGroupName=Easy Command
OutputDir={#SourcePath}\installer_output
OutputBaseFilename=EasyCommand-CE-Setup
Compression=lzma
SolidCompression=yes
WizardStyle=modern

[Files]
Source: "publish\win-x64\*"; DestDir: "{app}"; Flags: ignoreversion recursesubdirs createallsubdirs; Excludes: "*.pdb"


[Icons]
Name: "{group}\Easy Command"; Filename: "{app}\EasyCommand.exe"
Name: "{commondesktop}\Easy Command"; Filename: "{app}\EasyCommand.exe"; Tasks: desktopicon

[Tasks]
Name: "desktopicon"; Description: "Create a Desktop icon"; GroupDescription: "Additional icons:"

[Run]
Filename: "{app}\EasyCommand.exe"; Description: "Launch Easy Command"; Flags: nowait postinstall skipifsilent