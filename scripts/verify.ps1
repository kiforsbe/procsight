param([ValidateSet('Debug','Release')][string]$Configuration = 'Release')
$ErrorActionPreference = 'Stop'
$taskRoot = Split-Path $PSScriptRoot -Parent
Push-Location $taskRoot
try {
    dotnet restore ProcSight.slnx --locked-mode --nologo
    if ($LASTEXITCODE -ne 0) { throw 'Locked restore failed' }
    dotnet build ProcSight.slnx -c $Configuration --no-restore --nologo
    if ($LASTEXITCODE -ne 0) { throw 'Build failed' }
    foreach ($taskProject in @('Core','Application','Infrastructure','Windows')) {
        dotnet test "tests/ProcSight.$taskProject.Tests" -c $Configuration --no-build --no-restore --nologo
        if ($LASTEXITCODE -ne 0) { throw "$taskProject tests failed" }
    }
    git diff --check
    if ($LASTEXITCODE -ne 0) { throw 'Whitespace check failed' }
    git diff --cached --check
    if ($LASTEXITCODE -ne 0) { throw 'Staged whitespace check failed' }
} finally { Pop-Location }
