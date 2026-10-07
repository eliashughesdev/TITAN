#ifndef SourceRoot
  #define SourceRoot "."
#endif

#ifndef OutputDir
  #define OutputDir "."
#endif

#ifndef OutputName
  #define OutputName "TitanMDM-Agent-Setup"
#endif

[Setup]

AppId={{3D681C48-1D8C-47A0-9A20-TITANMDM00001}

AppName=TitanMDM Windows Agent
AppVersion=1.0.0
AppPublisher=TitanMDM
AppPublisherURL=https://github.com/EliasHughes/mdmTitan

DefaultDirName={autopf}\TitanMDM
DefaultGroupName=TitanMDM

PrivilegesRequired=admin
PrivilegesRequiredOverridesAllowed=dialog

ArchitecturesAllowed=x64compatible
ArchitecturesInstallIn64BitMode=x64compatible

DisableProgramGroupPage=yes
DisableDirPage=yes
DisableReadyPage=no

Compression=lzma2/ultra64
SolidCompression=yes

OutputDir={#OutputDir}
OutputBaseFilename={#OutputName}

SetupLogging=yes

/*
 * La desinstalación real se implementará de manera
 * controlada por TitanMDM y no mediante eliminación
 * ciega del directorio ProgramData.
 */

Uninstallable=no

WizardStyle=modern

CloseApplications=no
RestartApplications=no

[Files]

Source: "{#SourceRoot}\Install-TitanMDMAgent.ps1"; \
DestDir: "{tmp}\TitanMDM"; \
Flags: ignoreversion

Source: "{#SourceRoot}\config.json"; \
DestDir: "{tmp}\TitanMDM"; \
Flags: ignoreversion

[Run]

Filename: "{sys}\WindowsPowerShell\v1.0\powershell.exe"; \
Parameters: "-NoLogo -NoProfile -NonInteractive -ExecutionPolicy Bypass -File ""{tmp}\TitanMDM\Install-TitanMDMAgent.ps1"" -ConfigPath ""{tmp}\TitanMDM\config.json"""; \
StatusMsg: "Instalando o reparando TitanMDM Windows Agent..."; \
Flags: runhidden waituntilterminated