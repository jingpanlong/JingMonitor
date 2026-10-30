param([string]$Dotnet = 'dotnet', [string]$OutDir = "$PSScriptRoot\dist")
$ErrorActionPreference = 'Stop'
$env:DOTNET_CLI_TELEMETRY_OPTOUT='1'
New-Item -ItemType Directory -Path $OutDir -Force | Out-Null
& $Dotnet publish "$PSScriptRoot\JingMonitor\JingMonitor.csproj" -c Release -r win-x64 --self-contained true -o "$OutDir\portable"
if ($LASTEXITCODE -ne 0) { throw 'Publish failed' }
Copy-Item "$OutDir\portable\JingMonitor.exe" "$OutDir\JingMonitor.exe" -Force
$csc = "$env:WINDIR\Microsoft.NET\Framework64\v4.0.30319\csc.exe"
& $csc /nologo /target:winexe /optimize+ "/win32icon:$PSScriptRoot\JingMonitor\app.ico" /reference:System.Windows.Forms.dll "/out:$OutDir\Uninstall.exe" "$PSScriptRoot\installer\Uninstall.cs"
if ($LASTEXITCODE -ne 0) { throw 'Uninstaller compile failed' }
& $csc /nologo /target:winexe /optimize+ "/win32icon:$PSScriptRoot\JingMonitor\app.ico" /reference:System.Windows.Forms.dll /reference:System.Drawing.dll /reference:Microsoft.CSharp.dll "/resource:$OutDir\JingMonitor.exe,payload" "/resource:$OutDir\Uninstall.exe,uninstall" "/resource:$PSScriptRoot\JingMonitor\logo.png,brandlogo" "/resource:$PSScriptRoot\README.txt,readme" "/resource:$PSScriptRoot\THIRD-PARTY-NOTICES.txt,notices" "/out:$OutDir\JingMonitor-Setup.exe" "$PSScriptRoot\installer\Setup.cs"
if ($LASTEXITCODE -ne 0) { throw 'Installer compile failed' }
Write-Host "Build complete: $OutDir"
