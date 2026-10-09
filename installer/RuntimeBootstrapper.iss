#ifndef AppVersion
#define AppVersion "1.3.0"
#endif
[Setup]
AppId=FluentPrayerTimes
AppName=Fluent Prayer Times
AppVersion={#AppVersion}
AppPublisher=Mohamed Elnaggar
DefaultDirName={autopf}\FluentPrayerTimes
DefaultGroupName=Fluent Prayer Times
OutputBaseFilename=FluentPrayerTimes-v{#AppVersion}-installer-.NET-runtime-dependent-win-x64
OutputDir=..\out
ArchitecturesAllowed=x64compatible
ArchitecturesInstallIn64BitMode=x64compatible
PrivilegesRequired=admin
Compression=lzma2
SolidCompression=yes
WizardStyle=modern
SetupIconFile=..\app.ico
CloseApplications=no
RestartApplications=no
UninstallDisplayIcon={app}\app.ico
[Files]
Source: "..\publish\*"; DestDir: "{app}"; Flags: ignoreversion recursesubdirs createallsubdirs
[Icons]
Name: "{group}\Fluent Prayer Times"; Filename: "{app}\FluentPrayerTimes.exe"
Name: "{commondesktop}\Fluent Prayer Times"; Filename: "{app}\FluentPrayerTimes.exe"
[Run]
Filename: "{app}\FluentPrayerTimes.exe"; Description: "Launch Fluent Prayer Times"; Flags: nowait postinstall skipifsilent runasoriginaluser; Check: NotWasRunning
Filename: "{app}\FluentPrayerTimes.exe"; Parameters: "--tray"; Flags: nowait runasoriginaluser; Check: WasRunning
[Code]
var AppWasRunning: Boolean;
function WasRunning: Boolean;
begin
  Result := AppWasRunning;
end;
function NotWasRunning: Boolean;
begin
  Result := not AppWasRunning;
end;
function RunPowerShell(Script: String): Boolean;
var Code: Integer;
begin
  Result := Exec(ExpandConstant('{sys}\WindowsPowerShell\v1.0\powershell.exe'),
    '-NoProfile -NonInteractive -ExecutionPolicy Bypass -Command "' + Script + '"', '', SW_HIDE, ewWaitUntilTerminated, Code);
  Log('AFC: powershell exec=' + IntToStr(Integer(Result)) + ' code=' + IntToStr(Code));
  Result := Result and (Code = 0);
end;
// Asks the tray app to exit through its quit event, waits, then force-closes anything left.
procedure StopApp;
begin
  RunPowerShell('try{$e=[Threading.EventWaitHandle]::OpenExisting(''FluentPrayerTimes.Quit'');[void]$e.Set()}catch{}; ' +
    '$d=(Get-Date).AddSeconds(8); while((Get-Process FluentPrayerTimes -ErrorAction SilentlyContinue) -and ((Get-Date) -lt $d)){Start-Sleep -Milliseconds 200}; ' +
    'Get-Process FluentPrayerTimes -ErrorAction SilentlyContinue | Stop-Process -Force -ErrorAction SilentlyContinue; ' +
    'Start-Sleep -Milliseconds 500');
end;
procedure CurStepChanged(CurStep: TSetupStep);
begin
  Log('AFC: step ' + IntToStr(Integer(CurStep)));
  if CurStep = ssInstall then
  begin
    AppWasRunning := RunPowerShell('if(-not (Get-Process FluentPrayerTimes -ErrorAction SilentlyContinue)){exit 1}');
    if AppWasRunning then StopApp;
  end;
end;
function InitializeUninstall: Boolean;
begin
  StopApp;
  Result := True;
end;
function HasDotNet8: Boolean;
var R: TFindRec;
begin
  Result := False;
  if FindFirst(ExpandConstant('{commonpf64}\dotnet\shared\Microsoft.NETCore.App\8.*'), R) then
  begin
    repeat
      if (R.Attributes and FILE_ATTRIBUTE_DIRECTORY) <> 0 then Result := True;
    until Result or not FindNext(R);
    FindClose(R);
  end;
end;
function PowerShell(Script: String): Boolean;
var Code: Integer;
begin
  Result := Exec(ExpandConstant('{sys}\WindowsPowerShell\v1.0\powershell.exe'),
    '-NoProfile -NonInteractive -ExecutionPolicy Bypass -Command "' + Script + '"', '', SW_HIDE, ewWaitUntilTerminated, Code);
  Result := Result and (Code = 0);
end;
function InstallDependency(Url, Args: String): Boolean;
var Script: String;
begin
  Script := '$ErrorActionPreference=''Stop''; $f=[IO.Path]::GetTempFileName()+''.exe''; try { ' +
    '[Net.ServicePointManager]::SecurityProtocol=[Net.SecurityProtocolType]::Tls12; ' +
    'Invoke-WebRequest -UseBasicParsing -Uri ''' + Url + ''' -OutFile $f; ' +
    '$s=Get-AuthenticodeSignature -LiteralPath $f; if($s.Status -ne ''Valid'' -or $s.SignerCertificate.Subject -notmatch ''O=Microsoft Corporation''){throw ''Invalid Microsoft signature''}; ' +
    '$p=Start-Process -FilePath $f -ArgumentList ''' + Args + ''' -Wait -PassThru; if($p.ExitCode -ne 0 -and $p.ExitCode -ne 3010){throw ''Dependency installation failed''} ' +
    '} finally {Remove-Item $f -Force -ErrorAction SilentlyContinue}';
  Result := PowerShell(Script);
end;
function HasWar: Boolean;
begin
  Result := PowerShell('if(-not (Get-AppxPackage -AllUsers -Name Microsoft.WindowsAppRuntime.1.6 | Where-Object {$_.Architecture -eq ''X64'' -and $_.Status -eq ''Ok''})){exit 1}');
end;
function HasVc: Boolean;
var Installed: Cardinal;
begin
  Result := RegQueryDWordValue(HKLM64, 'SOFTWARE\Microsoft\VisualStudio\14.0\VC\Runtimes\x64', 'Installed', Installed) and (Installed = 1);
end;
function PrepareToInstall(var NeedsRestart: Boolean): String;
begin
  Result := '';
  if not HasDotNet8 then
    if not InstallDependency('https://aka.ms/dotnet/8.0/dotnet-runtime-win-x64.exe', '/install /quiet /norestart') or not HasDotNet8 then begin Result := 'Could not install .NET 8 x64.'; exit; end;
  if not HasVc then
    if not InstallDependency('https://aka.ms/vc14/vc_redist.x64.exe', '/install /quiet /norestart') or not HasVc then begin Result := 'Could not install Visual C++ x64.'; exit; end;
  if not HasWar then
    if not InstallDependency('https://aka.ms/windowsappsdk/1.6/latest/windowsappruntimeinstall-x64.exe', '--quiet') or not HasWar then begin Result := 'Could not install Windows App Runtime 1.6 x64.'; exit; end;
end;
procedure OpenCredit(Sender: TObject);
var Code: Integer;
begin
  ShellExec('open', 'https://instinct.com', '', '', SW_SHOWNORMAL, ewNoWait, Code);
end;
procedure InitializeWizard;
var Credit: TNewStaticText;
begin
  Credit := TNewStaticText.Create(WizardForm);
  Credit.Parent := WizardForm.WelcomePage;
  Credit.Caption := 'brought to you by Instinct';
  Credit.Left := WizardForm.WelcomeLabel2.Left;
  Credit.Top := WizardForm.WelcomeLabel2.Top + WizardForm.WelcomeLabel2.Height + ScaleY(8);
  Credit.Font.Color := clBlue;
  Credit.Cursor := crHand;
  Credit.OnClick := @OpenCredit;
end;
