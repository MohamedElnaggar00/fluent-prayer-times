# Verifies that both installers close a running tray instance on their own and (native installer) start it again.
$ErrorActionPreference = 'Stop'
function RunSetup($file, $argList) {
  # Start-Process -Wait would also wait for the app that setup restarts, so wait for setup itself only
  $p = Start-Process $file -ArgumentList $argList -PassThru
  $null = $p.Handle
  if (-not $p.WaitForExit(240000)) { Stop-Process -Id $p.Id -Force; Fail 'setup timed out' }
  return $p
}
function Fail($m) { Write-Host "FAIL: $m"; exit 1 }
dotnet publish FluentPrayerTimes.csproj -c Release -r win-x64 -p:Platform=x64 --self-contained false -p:DebugType=none -o publish
if ($LASTEXITCODE -ne 0) { Fail 'publish' }
Invoke-WebRequest https://aka.ms/windowsappsdk/1.6/latest/windowsappruntimeinstall-x64.exe -OutFile wari.exe
Start-Process .\wari.exe -ArgumentList '--quiet' -Wait
Compress-Archive -Path publish/* -DestinationPath installer/payload.zip
dotnet publish installer/Installer.csproj -c Release -r win-x64 --self-contained true -p:DebugType=none -o setup
if ($LASTEXITCODE -ne 0) { Fail 'installer publish' }
& "${env:ProgramFiles(x86)}/Inno Setup 6/ISCC.exe" installer/RuntimeBootstrapper.iss
if ($LASTEXITCODE -ne 0) { Fail 'iscc' }
$native = (Get-ChildItem out/*.exe)[0].FullName
$exe = Join-Path $env:ProgramFiles 'FluentPrayerTimes/FluentPrayerTimes.exe'
$silent = '/VERYSILENT','/SUPPRESSMSGBOXES','/NORESTART'

# 1) Scenario from the bug report: installed 1.3.0 is running in the tray, the new installer runs over it.
gh release download v1.3.0 -p '*runtime-dependent*' -O old-setup.exe
$p = Start-Process .\old-setup.exe -ArgumentList $silent -PassThru -Wait
if ($p.ExitCode -ne 0) { Fail "old install $($p.ExitCode)" }
$old = Start-Process $exe -ArgumentList '--tray' -PassThru
Start-Sleep -Seconds 8
if ($old.HasExited) { Fail 'old app not running' }
$p = RunSetup $native ($silent + @("/LOG=$PWD/upgrade-old.log"))
if ($p.ExitCode -ne 0) { Get-Content upgrade-old.log | Where-Object { $_ -notmatch 'File entry|Dest filename|Time stamp|Dest file exists|Installing the file|Successfully installed|Version of|Leaving temporary|Skipping|Same version' } | Select-Object -First 70 | ForEach-Object { 'LOG> ' + $_ }; Get-Process FluentPrayerTimes -ErrorAction SilentlyContinue | Format-Table Id,Path; Fail "upgrade over running 1.3.0 exit $($p.ExitCode)" }
Start-Sleep -Seconds 8
if (Get-Process -Id $old.Id -ErrorAction SilentlyContinue) { Fail 'old instance still running' }
$re = Get-Process FluentPrayerTimes -ErrorAction SilentlyContinue
if (-not $re) { Fail 'app not restarted after upgrade' }
Write-Host "OK 1: upgrade over running 1.3.0 (old $($old.Id), new $($re.Id))"

# 2) New app handles the graceful quit signal itself (no force kill needed).
$cur = $re | Select-Object -First 1
$e = [Threading.EventWaitHandle]::OpenExisting('FluentPrayerTimes.Quit'); [void]$e.Set()
$d = (Get-Date).AddSeconds(10)
while ((Get-Process -Id $cur.Id -ErrorAction SilentlyContinue) -and (Get-Date) -lt $d) { Start-Sleep -Milliseconds 200 }
if (Get-Process -Id $cur.Id -ErrorAction SilentlyContinue) { Fail 'app ignored quit signal' }
Write-Host 'OK 2: graceful quit signal'

# 3) Native installer again over a running new build.
$run = Start-Process $exe -ArgumentList '--tray' -PassThru
Start-Sleep -Seconds 8
$p = RunSetup $native $silent
if ($p.ExitCode -ne 0) { Fail "native reinstall exit $($p.ExitCode)" }
Start-Sleep -Seconds 8
if (Get-Process -Id $run.Id -ErrorAction SilentlyContinue) { Fail 'native: running instance not closed' }
if (-not (Get-Process FluentPrayerTimes -ErrorAction SilentlyContinue)) { Fail 'native: not restarted' }
Write-Host 'OK 3: native reinstall over running'
Get-Process FluentPrayerTimes -ErrorAction SilentlyContinue | Stop-Process -Force
Start-Sleep -Seconds 2

# 4) Self-contained installer over a running instance.
$run = Start-Process $exe -ArgumentList '--tray' -PassThru
Start-Sleep -Seconds 8
$p = Start-Process setup/FluentPrayerTimes-Setup.exe -ArgumentList '--verify-install' -PassThru -Wait
if ($p.ExitCode -ne 0) { Fail "self-contained exit $($p.ExitCode)" }
if (Get-Process -Id $run.Id -ErrorAction SilentlyContinue) { Fail 'self-contained: running instance not closed' }
Write-Host 'OK 4: self-contained install over running'

# 5) Uninstall while running.
$run = Start-Process $exe -ArgumentList '--tray' -PassThru
Start-Sleep -Seconds 6
$unins = Get-ChildItem "$env:ProgramFiles/FluentPrayerTimes/unins*.exe" -ErrorAction SilentlyContinue | Select-Object -First 1
if ($unins) {
  $p = Start-Process $unins.FullName -ArgumentList '/VERYSILENT','/SUPPRESSMSGBOXES','/NORESTART' -PassThru -Wait
  if (Get-Process -Id $run.Id -ErrorAction SilentlyContinue) { Fail 'uninstall left app running' }
  Write-Host 'OK 5: uninstall while running'
} else { Write-Host 'skip 5: Inno uninstaller not present (self-contained install replaced it)'; Get-Process FluentPrayerTimes -ErrorAction SilentlyContinue | Stop-Process -Force }
Write-Host 'ALL OK'
