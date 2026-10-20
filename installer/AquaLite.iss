#define AppVersion GetVersionNumbersString(SourcePath + "..\build\app\AquaLite.exe")

[Setup]
AppId={{889E602C-877E-41DA-942A-1C9B2349140F}
AppName=AquaLite
AppVersion={#AppVersion}
AppPublisher=AquaLite
DefaultDirName={autopf}\AquaLite
DefaultGroupName=AquaLite
DisableProgramGroupPage=yes
PrivilegesRequired=admin
ArchitecturesAllowed=x64compatible
ArchitecturesInstallIn64BitMode=x64compatible
MinVersion=10.0
WizardStyle=modern
SetupIconFile=..\build\generated\assets\aqualite.ico
UninstallDisplayIcon={app}\AquaLite.exe
OutputDir=..\dist
OutputBaseFilename=AquaLite-Instalador
Compression=lzma2
SolidCompression=yes
CloseApplications=yes
RestartApplications=no
LicenseFile=..\LICENSE
SetupLogging=yes

[Languages]
Name: "brazilianportuguese"; MessagesFile: "compiler:Languages\BrazilianPortuguese.isl"

[Tasks]
Name: "desktopicon"; Description: "Criar atalho na área de trabalho"; GroupDescription: "Atalhos:"; Flags: unchecked

[Dirs]
Name: "{commonappdata}\AquaLite"; Permissions: admins-full system-full; Flags: uninsneveruninstall

[Files]
Source: "..\build\app\*"; DestDir: "{app}"; Flags: ignoreversion recursesubdirs createallsubdirs
Source: "installed.mode"; DestDir: "{app}"; Flags: ignoreversion

[Icons]
Name: "{autoprograms}\AquaLite"; Filename: "{app}\AquaLite.exe"
Name: "{autodesktop}\AquaLite"; Filename: "{app}\AquaLite.exe"; Tasks: desktopicon

[Run]
Filename: "{app}\AquaLite.exe"; Description: "Abrir o painel do AquaLite"; Flags: nowait postinstall skipifsilent

[UninstallRun]
Filename: "{app}\AquaLite.exe"; Parameters: "--shutdown"; Flags: runhidden waituntilterminated; RunOnceId: "AquaLitePanel"
Filename: "{app}\AquaLite.exe"; Parameters: "--uninstall-service"; Flags: runhidden waituntilterminated; RunOnceId: "AquaLiteService"

[Code]
function PrepareToInstall(var NeedsRestart: Boolean): String;
var
  Code: Integer;
begin
  Result := '';
  if FileExists(ExpandConstant('{app}\installed.mode')) then begin
    Exec(ExpandConstant('{app}\AquaLite.exe'), '--shutdown', '', SW_HIDE, ewWaitUntilTerminated, Code);
    if not Exec(ExpandConstant('{app}\AquaLite.exe'), '--stop-service', '', SW_HIDE, ewWaitUntilTerminated, Code) or (Code <> 0) then
      Result := 'Não foi possível parar a versão anterior. Feche o painel e tente novamente.';
  end;
end;

procedure CurStepChanged(CurStep: TSetupStep);
var
  Code: Integer;
begin
  if CurStep = ssPostInstall then begin
    if not Exec(ExpandConstant('{app}\AquaLite.exe'), '--install-service', '', SW_HIDE, ewWaitUntilTerminated, Code) or (Code <> 0) then
      RaiseException('Não foi possível ativar a tela na inicialização. Consulte ' + ExpandConstant('{commonappdata}\AquaLite\instalacao-erro.txt') + '.');
  end;
end;
