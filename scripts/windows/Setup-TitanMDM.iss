
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
AppId={{3D681C48-1D8C-47A0-9A20-0A710D000001}
AppName=TitanMDM Windows Agent
AppVersion=1.0.0
AppPublisher=TitanMDM
AppPublisherURL=https://github.com/eliashughesdev/TITAN

DefaultDirName={autopf}\TitanMDM
DefaultGroupName=TitanMDM

PrivilegesRequired=admin
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
Uninstallable=no
WizardStyle=modern

CloseApplications=no
RestartApplications=no

[Files]
Source: "{#SourceRoot}\Install-TitanMDMAgent.ps1"; DestDir: "{tmp}\TitanMDM"; Flags: ignoreversion
Source: "{#SourceRoot}\config.json"; DestDir: "{tmp}\TitanMDM"; Flags: ignoreversion

[Code]
procedure CurStepChanged(CurStep: TSetupStep);
var
  ResultCode: Integer;
  PowerShellPath: String;
  InstallerPath: String;
  ConfigPath: String;
  Parameters: String;
begin
  if CurStep <> ssPostInstall then
    Exit;

  PowerShellPath :=
    ExpandConstant('{sys}\WindowsPowerShell\v1.0\powershell.exe');

  InstallerPath :=
    ExpandConstant('{tmp}\TitanMDM\Install-TitanMDMAgent.ps1');

  ConfigPath :=
    ExpandConstant('{tmp}\TitanMDM\config.json');

  if not FileExists(InstallerPath) then
  begin
    RaiseException(
      'No se encontró el script interno de instalación TitanMDM.');
  end;

  if not FileExists(ConfigPath) then
  begin
    RaiseException(
      'No se encontró la configuración de instalación TitanMDM.');
  end;

  Parameters :=
    '-NoLogo -NoProfile -NonInteractive -ExecutionPolicy Bypass ' +
    '-File "' + InstallerPath + '" ' +
    '-ConfigPath "' + ConfigPath + '" -Silent';

  WizardForm.StatusLabel.Caption :=
    'Instalando o actualizando TitanMDM Windows Agent...';

  if not Exec(
    PowerShellPath,
    Parameters,
    '',
    SW_HIDE,
    ewWaitUntilTerminated,
    ResultCode) then
  begin
    RaiseException(
      'No fue posible iniciar el instalador interno de TitanMDM.');
  end;

  if ResultCode <> 0 then
  begin
    RaiseException(
      'La instalación del agente TitanMDM falló. Código: ' +
      IntToStr(ResultCode) +
      '. Revisa el registro de instalación en ' +
      'C:\ProgramData\TitanMDM\logs\installer.log.');
  end;
end;
