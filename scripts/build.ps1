param([ValidateSet('Debug','Release')][string]$Configuration = 'Debug')
$ErrorActionPreference = 'Stop'
$taskRoot = Split-Path $PSScriptRoot -Parent
Push-Location $taskRoot
try {
    dotnet build ProcSight.slnx -c $Configuration --nologo
    if ($LASTEXITCODE -ne 0) { throw 'Build failed' }
} finally { Pop-Location }
