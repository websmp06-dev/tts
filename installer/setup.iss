; Instalador de TTS Virtual Mic (Inno Setup 6.1 o superior)
; Compilar:  ISCC.exe installer\setup.iss   (después de publicar la app en .\publish)

#define AppName "TTS Virtual Mic"
#define AppVersion "1.0.0"
#define AppExe "TtsVirtualMic.exe"
; Paquete oficial de VB-Audio. Si cambia la versión, actualizá solo esta URL.
#define VbCableUrl "https://download.vb-audio.com/Download_CABLE/VBCABLE_Driver_Pack43.zip"

[Setup]
AppId={{8C2B6E0A-5F6E-4B7B-9A55-2D4C1E7A9F10}
AppName={#AppName}
AppVersion={#AppVersion}
DefaultDirName={autopf}\{#AppName}
DefaultGroupName={#AppName}
OutputDir=..\dist
OutputBaseFilename=TtsVirtualMic-Setup
Compression=lzma2
SolidCompression=yes
PrivilegesRequired=admin
ArchitecturesAllowed=x64compatible
ArchitecturesInstallIn64BitMode=x64compatible
WizardStyle=modern
UninstallDisplayIcon={app}\{#AppExe}
CloseApplications=yes

[Languages]
Name: "spanish"; MessagesFile: "compiler:Languages\Spanish.isl"

[Tasks]
Name: "desktopicon"; Description: "Crear un acceso directo en el escritorio"; GroupDescription: "Accesos directos:"
Name: "installcable"; Description: "Instalar el micrófono virtual (VB-CABLE, requiere Internet)"; GroupDescription: "Micrófono virtual:"; Check: NoCable

[Files]
Source: "..\publish\*"; DestDir: "{app}"; Flags: recursesubdirs ignoreversion

[Icons]
Name: "{group}\{#AppName}"; Filename: "{app}\{#AppExe}"
Name: "{group}\Desinstalar {#AppName}"; Filename: "{uninstallexe}"
Name: "{autodesktop}\{#AppName}"; Filename: "{app}\{#AppExe}"; Tasks: desktopicon

[Run]
Filename: "{app}\{#AppExe}"; Description: "Abrir {#AppName}"; Flags: nowait postinstall skipifsilent

[UninstallDelete]
Type: filesandordirs; Name: "{app}\vbcable"

[Code]
var
  DownloadPage: TDownloadWizardPage;
  CableChecked: Boolean;
  HasCableValue: Boolean;
  CableDownloaded: Boolean;
  CableInstalledNow: Boolean;

function DetectCable: Boolean;
var
  Code: Integer;
begin
  Result := Exec('powershell.exe',
    '-NoProfile -ExecutionPolicy Bypass -Command "if (Get-CimInstance Win32_PnPEntity | Where-Object { $_.Name -like ''*VB-Audio Virtual Cable*'' }) { exit 0 } else { exit 1 }"',
    '', SW_HIDE, ewWaitUntilTerminated, Code) and (Code = 0);
end;

function HasCable: Boolean;
begin
  if not CableChecked then
  begin
    HasCableValue := DetectCable;
    CableChecked := True;
  end;
  Result := HasCableValue;
end;

function NoCable: Boolean;
begin
  Result := not HasCable;
end;

procedure InitializeWizard;
begin
  DownloadPage := CreateDownloadPage(SetupMessage(msgWizardPreparing), SetupMessage(msgPreparingDesc), nil);
end;

function NextButtonClick(CurPageID: Integer): Boolean;
begin
  Result := True;
  CableDownloaded := False;
  if (CurPageID = wpReady) and WizardIsTaskSelected('installcable') then
  begin
    DownloadPage.Clear;
    DownloadPage.Add('{#VbCableUrl}', 'vbcable.zip', '');
    DownloadPage.Show;
    try
      try
        DownloadPage.Download;
        CableDownloaded := True;
      except
        SuppressibleMsgBox('No se pudo descargar VB-CABLE:' + #13#10 + GetExceptionMessage + #13#10#13#10 +
          'La aplicación se instalará igual. Podés instalar VB-CABLE más tarde desde https://vb-audio.com/Cable/ ' +
          '(ejecutá VBCABLE_Setup_x64.exe como administrador) y luego reiniciar Windows.',
          mbError, MB_OK, IDOK);
      end;
    finally
      DownloadPage.Hide;
    end;
  end;
end;

procedure CurStepChanged(CurStep: TSetupStep);
var
  Code: Integer;
  Zip, Dest: String;
begin
  if (CurStep = ssPostInstall) and CableDownloaded then
  begin
    Zip := ExpandConstant('{tmp}\vbcable.zip');
    Dest := ExpandConstant('{app}\vbcable');
    // Se conserva una copia local para poder desinstalar el driver más adelante.
    Exec('powershell.exe',
      '-NoProfile -ExecutionPolicy Bypass -Command "Expand-Archive -LiteralPath ''' + Zip + ''' -DestinationPath ''' + Dest + ''' -Force"',
      '', SW_HIDE, ewWaitUntilTerminated, Code);
    if FileExists(Dest + '\VBCABLE_Setup_x64.exe') then
    begin
      WizardForm.StatusLabel.Caption := 'Instalando el micrófono virtual (VB-CABLE)...';
      if Exec(Dest + '\VBCABLE_Setup_x64.exe', '-i -h', Dest, SW_SHOW, ewWaitUntilTerminated, Code) and (Code = 0) then
        CableInstalledNow := True
      else
        SuppressibleMsgBox('VB-CABLE no se instaló correctamente (código ' + IntToStr(Code) + '). ' +
          'Podés ejecutar manualmente "' + Dest + '\VBCABLE_Setup_x64.exe" como administrador.',
          mbError, MB_OK, IDOK);
    end
    else
      SuppressibleMsgBox('No se pudo extraer el paquete de VB-CABLE.', mbError, MB_OK, IDOK);
  end;
end;

function NeedRestart: Boolean;
begin
  // Tras instalar un driver de audio, Windows suele necesitar reiniciar para exponer el micrófono.
  Result := CableInstalledNow;
end;

procedure CurUninstallStepChanged(CurUninstallStep: TUninstallStep);
var
  Code: Integer;
  Setup: String;
begin
  if CurUninstallStep = usUninstall then
  begin
    Setup := ExpandConstant('{app}\vbcable\VBCABLE_Setup_x64.exe');
    if FileExists(Setup) then
      if MsgBox('¿Querés desinstalar también el micrófono virtual VB-CABLE?' + #13#10 +
                '(Elegí "No" si lo usás con otras aplicaciones.)', mbConfirmation, MB_YESNO) = IDYES then
        Exec(Setup, '-u -h', ExtractFilePath(Setup), SW_SHOW, ewWaitUntilTerminated, Code);
  end;
end;
