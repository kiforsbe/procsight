#requires -Version 7.0
param(
    [ValidateRange(1,120)][int]$Samples=15,
    [ValidatePattern('^[a-zA-Z0-9-]+$')][string]$Label='measurement'
)
$ErrorActionPreference='Stop'
$taskRoot=Split-Path $PSScriptRoot -Parent
$taskOutput=Join-Path $taskRoot 'artifacts/sampling-performance'
Push-Location $taskRoot
try {
    New-Item -ItemType Directory -Force $taskOutput | Out-Null
    dotnet restore benchmarks/Sampling --locked-mode --nologo
    if($LASTEXITCODE -ne 0){throw 'Locked restore failed'}
    dotnet build benchmarks/Sampling -c Release --no-restore --nologo
    if($LASTEXITCODE -ne 0){throw 'Sampling benchmark build failed'}
    $taskDll=Join-Path $taskRoot 'benchmarks/Sampling/bin/Release/net10.0-windows10.0.26100.0/Sampling.dll'
    foreach($taskMode in @('system','gpu','metadata','inventory')) {
        Write-Output "Measuring $taskMode ($Samples samples; no AI/accounts)..."
        $taskPath=Join-Path $taskOutput ($Label+'-'+$taskMode+'-'+[DateTime]::UtcNow.ToString('yyyyMMdd-HHmmss')+'.json')
        dotnet $taskDll $taskMode $Samples | Set-Content -LiteralPath $taskPath
        if($LASTEXITCODE -ne 0){throw "Sampling benchmark failed: $taskMode"}
        Write-Output "Aggregate timing/allocation/memory report: $taskPath"
    }
} finally { Pop-Location }
