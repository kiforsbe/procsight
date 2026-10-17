#requires -Version 7.0
param([ValidateSet('Debug','Release')][string]$Configuration = 'Release')
$ErrorActionPreference = 'Stop'
$taskRoot = Split-Path $PSScriptRoot -Parent
Push-Location $taskRoot
try {
    foreach ($taskScript in @('test-release.ps1','test-packaging.ps1','test-release-workflow.ps1')) {
        & (Join-Path $PSScriptRoot $taskScript)
        if (!$?) { throw "$taskScript failed" }
    }
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
