# RuleFlow - one-command installer for Windows (evaluation build).
#
#   irm https://raw.githubusercontent.com/RoberthDudiver/ruleflow/main/install.ps1 | iex
#
# Options (env vars):
#   $env:RULEFLOW_REPO    public releases repo (default RoberthDudiver/ruleflow)
#   $env:RULEFLOW_HOME    install dir (default %LOCALAPPDATA%\RuleFlow)
#   $env:RULEFLOW_PORT    HTTP port (default 8080)
#   $env:RULEFLOW_SERVICE = "1"  also install & start a Windows Service (run as Administrator)
$ErrorActionPreference = 'Stop'

$repo = if ($env:RULEFLOW_REPO) { $env:RULEFLOW_REPO } else { 'RoberthDudiver/ruleflow' }
$dest = if ($env:RULEFLOW_HOME) { $env:RULEFLOW_HOME } else { "$env:LOCALAPPDATA\RuleFlow" }
$port = if ($env:RULEFLOW_PORT) { $env:RULEFLOW_PORT } else { '8080' }

Write-Host "> Installing RuleFlow into $dest"
$rel = Invoke-RestMethod "https://api.github.com/repos/$repo/releases/latest"
$asset = $rel.assets | Where-Object { $_.name -like '*win-x64.zip' } | Select-Object -First 1
if (-not $asset) { throw "No win-x64 asset found in $repo releases." }

New-Item -ItemType Directory -Force -Path $dest | Out-Null
$zip = "$env:TEMP\ruleflow.zip"
Write-Host "> Downloading $($asset.browser_download_url)"
Invoke-WebRequest $asset.browser_download_url -OutFile $zip
Expand-Archive -Path $zip -DestinationPath $dest -Force
Remove-Item $zip -Force

# Flatten the win-x64 subfolder produced by the archive.
$sub = Join-Path $dest 'win-x64'
if (Test-Path $sub) { Get-ChildItem $sub | Move-Item -Destination $dest -Force; Remove-Item $sub -Recurse -Force }

$exe = Join-Path $dest 'Dudiver.RuleFlow.Server.exe'

if ($env:RULEFLOW_SERVICE -eq '1') {
    Write-Host "> Registering Windows Service 'RuleFlow' (requires Administrator)"
    New-Service -Name RuleFlow -BinaryPathName "`"$exe`"" -DisplayName 'RuleFlow' -StartupType Automatic -ErrorAction SilentlyContinue | Out-Null
    [Environment]::SetEnvironmentVariable('ASPNETCORE_URLS', "http://0.0.0.0:$port", 'Machine')
    Start-Service RuleFlow
    Write-Host "OK Service 'RuleFlow' started on http://localhost:$port"
} else {
    Write-Host "OK Installed. Start it with:"
    Write-Host "    cd `"$dest`"; `$env:ASPNETCORE_URLS='http://0.0.0.0:$port'; .\Dudiver.RuleFlow.Server.exe"
}

Write-Host "-> Open http://localhost:$port and complete the installation wizard."
