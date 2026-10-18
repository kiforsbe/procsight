#requires -Version 7.0
$ErrorActionPreference = 'Stop'
$workflow = Get-Content (Join-Path $PSScriptRoot '../.github/workflows/windows-build.yml') -Raw
$marker = '      - name: Prepare draft release'
$start = $workflow.IndexOf($marker, [StringComparison]::Ordinal)
if ($start -lt 0) { throw 'Draft release job is missing.' }
$body = ($workflow.Substring($start) -split '        run: \|\r?\n', 2)[1]
$body = ($body -split '\r?\n' | ForEach-Object { $_ -replace '^          ', '' }) -join "`n"
$release = [scriptblock]::Create($body)
$sandbox = Join-Path ([IO.Path]::GetTempPath()) ('procsight-draft-test-' + [guid]::NewGuid())
$null = New-Item -ItemType Directory $sandbox
$names = @('REQUESTED_TAG','EXPECTED_COMMIT','ARTIFACT_NAME','GH_REPO','GITHUB_RUN_ID','GITHUB_STEP_SUMMARY')
$saved = @{}
foreach ($name in $names) { $saved[$name] = [Environment]::GetEnvironmentVariable($name) }
$checks = 0
try {
    $env:REQUESTED_TAG = 'v1.2.3'
    $env:EXPECTED_COMMIT = 'a' * 40
    $env:ARTIFACT_NAME = 'synthetic-build'
    $env:GH_REPO = 'example/synthetic'
    $env:GITHUB_RUN_ID = '123'
    $env:GITHUB_STEP_SUMMARY = Join-Path $sandbox 'summary.md'
    function gh {
        $script:LASTEXITCODE = 0
        $call = $args -join ' '
        $script:calls.Add($call)
        if ($script:fail -and $call.StartsWith($script:fail)) { $script:LASTEXITCODE = 1; return }
        if ($call.StartsWith('run download')) {
            $null = New-Item -ItemType Directory 'release-assets'
            $msi = 'release-assets/ProcSight-1.2.3-x64-unsigned.msi'
            Set-Content $msi 'Synthetic MSI content'
            $hash = (Get-FileHash $msi -Algorithm SHA256).Hash
            if ($script:scenario -eq 'bad-hash') { $hash = '0' * 64 }
            $manifest = @{Version='1.2.3';Commit=$env:EXPECTED_COMMIT;Profile='Msi';Signed=$false;Artifact=[IO.Path]::GetFileName($msi);SHA256=$hash}
            if ($script:scenario -eq 'bad-name') { $manifest.Artifact = '../outside.msi' }
            if ($script:scenario -eq 'bad-version') { $manifest.Version = '1.2.4' }
            if ($script:scenario -eq 'bad-commit') { $manifest.Commit = 'c' * 40 }
            if ($script:scenario -eq 'signed') { $manifest.Signed = $true }
            $manifest | ConvertTo-Json | Set-Content 'release-assets/release-artifacts.json'
            if ($script:scenario -eq 'extra-file') { Set-Content 'release-assets/unexpected.txt' 'extra' }
        } elseif ($call -match '^api repos/.+/commits/') {
            if ($script:scenario -eq 'moved-tag') { 'b' * 40 } else { $env:EXPECTED_COMMIT }
        } elseif ($call -match '^api .*releases') {
            if ($script:scenario -in @('published','draft','collision')) {
                $assets = @()
                if ($script:scenario -eq 'collision') { $assets = @(@{name='release-artifacts.json'}) }
                ,@(@{tag_name='v1.2.3';draft=($script:scenario -ne 'published');assets=$assets}) | ConvertTo-Json -Depth 5 -Compress
            } else { '[[]]' }
        } elseif ($call.StartsWith('release create') -or $call.StartsWith('release upload')) {
            'https://github.com/example/synthetic/releases'
        } else { throw "Unexpected synthetic CLI call: $call" }
    }
    foreach ($case in @('new','draft','published','collision','bad-hash','bad-name','bad-version','bad-commit','signed','extra-file','moved-tag','download-error','api-error','create-error','upload-error','invalid-tag')) {
        $script:scenario = $case
        $script:fail = switch ($case) {
            'download-error' { 'run download' }
            'api-error' { 'api' }
            'create-error' { 'release create' }
            'upload-error' { 'release upload' }
            default { '' }
        }
        if ($case -eq 'upload-error') { $script:scenario = 'draft' }
        $env:REQUESTED_TAG = if ($case -eq 'invalid-tag') { '--invalid' } else { 'v1.2.3' }
        $script:calls = [Collections.Generic.List[string]]::new()
        $caseDir = Join-Path $sandbox $case
        $null = New-Item -ItemType Directory $caseDir
        Push-Location $caseDir
        try {
            $errorText = $null
            try { & $release | Out-Null } catch { $errorText = $_.Exception.Message }
            if ($case -in @('new','draft')) {
                if ($errorText) { throw "$case unexpectedly failed: $errorText" }
                $action = if ($case -eq 'new') { 'create' } else { 'upload' }
                if (!($script:calls | Where-Object { $_ -like "release $action *" })) { throw "$case did not $action assets" }
                if ($case -eq 'new' -and !($script:calls | Where-Object { $_ -match '--draft' -and $_ -match '--verify-tag' })) { throw 'Creation must remain draft and verify the tag' }
            } else {
                if (!$errorText) { throw "$case should have failed" }
                $expectedError = switch ($case) {
                    'published' { 'already has a published release' }
                    'collision' { 'Draft already contains these assets' }
                    'extra-file' { 'Unexpected release artifact contents' }
                    'moved-tag' { 'Remote tag no longer matches' }
                    'download-error' { 'Could not download' }
                    'api-error' { 'Remote tag no longer matches' }
                    'create-error' { 'Draft creation or asset upload failed' }
                    'upload-error' { 'Draft asset upload failed' }
                    'invalid-tag' { 'Invalid release tag or build commit' }
                    default { 'Release artifact does not match' }
                }
                if (!$errorText.Contains($expectedError)) { throw "$case failed for the wrong reason: $errorText" }
                if ($case -notin @('create-error','upload-error') -and ($script:calls | Where-Object { $_ -match '^release (create|upload) ' })) { throw "$case attempted a release write" }
            }
            if ($script:calls | Where-Object { $_ -match '--clobber|release edit|--draft=false' }) { throw 'Release must not publish or replace assets' }
            $checks++
        } finally { Pop-Location }
    }
    Write-Host "GitHub draft release checks passed: $checks (synthetic CLI only; no network or release changes)."
} finally {
    foreach ($name in $names) { [Environment]::SetEnvironmentVariable($name, $saved[$name]) }
    Remove-Item Function:gh
    Remove-Item -LiteralPath $sandbox -Recurse -Force
}
