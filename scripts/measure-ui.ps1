#requires -Version 7.0
param(
    [ValidateSet('Dark','Light')][string]$Theme='Dark',
    [ValidateSet(700,1200)][int]$Width=1200,
    [ValidatePattern('^[a-zA-Z0-9-]+$')][string]$Label='measurement',
    [switch]$NoBuild
)
$ErrorActionPreference='Stop'
$taskRoot=Split-Path $PSScriptRoot -Parent
$taskOutput=Join-Path $taskRoot 'artifacts/ui-performance'
$taskBuild=Join-Path $taskOutput 'app'
Push-Location $taskRoot
try {
    if(!$NoBuild) {
        dotnet restore src/ProcSight.App --locked-mode --nologo
        if($LASTEXITCODE -ne 0){throw 'Locked restore failed'}
        # Keep the native fixture entry point and SDK bootstrap constants; optimize managed code.
        dotnet build src/ProcSight.App -c Debug --no-restore --nologo -p:Optimize=true "-p:BaseOutputPath=$taskBuild/"
        if($LASTEXITCODE -ne 0){throw 'Benchmark build failed'}
    }
    $taskExe=Join-Path $taskBuild 'Debug/net10.0-windows10.0.26100.0/win-x64/ProcSight.App.exe'
    if(!(Test-Path -LiteralPath $taskExe)){throw 'Build the benchmark before using -NoBuild'}
    $taskCapture=Join-Path $taskOutput ($Label+'-'+[DateTime]::UtcNow.ToString('yyyyMMdd-HHmmss-fff')+'.png')
    $taskProcess=Start-Process -FilePath $taskExe -ArgumentList @('--visual-fixture',('"'+$taskCapture+'"'),$Theme,$Width,'ui-performance') -WindowStyle Hidden -PassThru
    if(!$taskProcess.WaitForExit(60000)) {
        $taskProcess.Kill()
        throw 'Synthetic benchmark exceeded 60 seconds; partial measurements remain in artifacts/ui-performance'
    }
    if(Test-Path -LiteralPath ($taskCapture+'.error')){throw(Get-Content -LiteralPath ($taskCapture+'.error') -Raw)}
    if(!(Test-Path -LiteralPath ($taskCapture+'.checks'))){throw ('Benchmark failed with exit code '+$taskProcess.ExitCode)}
    $taskResults=Get-Content -LiteralPath ($taskCapture+'.performance.json') -Raw | ConvertFrom-Json
    $taskResults | Select-Object name,meanMs,p95Ms,meanAllocatedBytes | Format-Table -AutoSize
    Write-Output ('Synthetic report: '+$taskCapture+'.performance.json')
} finally { Pop-Location }
