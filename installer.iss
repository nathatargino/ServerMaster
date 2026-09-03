[Setup]
AppName=Server Master
AppVersion=1.0.2
AppPublisher=Server Master Team
AppPublisherURL=https://github.com/nathatargino/ServerMaster
AppCopyright=Copyright (C) 2026 Server Master
DefaultDirName={autopf}\Server Master
DefaultGroupName=Server Master
OutputBaseFilename=ServerMaster-Setup
OutputDir=Output
Compression=lzma2
SolidCompression=yes
ArchitecturesInstallIn64BitMode=x64
SetupIconFile=src\ServerMaster.App\Assets\logo.ico
UninstallDisplayIcon={app}\ServerMaster.exe
PrivilegesRequired=admin

[Files]
Source: "publish\*"; DestDir: "{app}"; Flags: ignoreversion recursesubdirs createallsubdirs

[Icons]
Name: "{group}\Server Master"; Filename: "{app}\ServerMaster.exe"
Name: "{autodesktop}\Server Master"; Filename: "{app}\ServerMaster.exe"; Tasks: desktopicon

[Tasks]
Name: "desktopicon"; Description: "Criar um Ã­cone na Ã¡rea de trabalho"; GroupDescription: "Ãcones adicionais:"

[Run]
Filename: "{app}\ServerMaster.exe"; Description: "Iniciar Server Master"; Flags: nowait postinstall skipifsilent

