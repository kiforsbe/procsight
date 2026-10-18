param([ValidateSet('Debug','Release')][string]$Configuration = 'Debug')
$taskRoot = Split-Path $PSScriptRoot -Parent
$taskExe = Join-Path $taskRoot "src/ProcSight.App/bin/$Configuration/net10.0-windows10.0.26100.0/win-x64/ProcSight.App.exe"
if (-not (Test-Path -LiteralPath $taskExe)) { throw 'Build first: ./scripts/build.ps1' }
& $taskExe
