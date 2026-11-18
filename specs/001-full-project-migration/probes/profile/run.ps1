$ErrorActionPreference = 'Stop'
$repo = (Resolve-Path (Join-Path $PSScriptRoot '../../../..')).Path
$run = Join-Path $repo ('artifacts/migration/profile-probe/run-' + [Guid]::NewGuid().ToString('N'))
New-Item -ItemType Directory -Path $run -Force | Out-Null
Copy-Item -LiteralPath (Join-Path $PSScriptRoot 'Namespace.cs') -Destination $run
$template = Get-Content -LiteralPath (Join-Path $PSScriptRoot 'ProfileProbe.Tests.csproj.template') -Raw
$project = Join-Path $run 'ProfileProbe.Tests.csproj'
$template.Replace('@@ROOT@@', [System.Security.SecurityElement]::Escape($repo.Replace('\','/'))) | Set-Content -LiteralPath $project
& dotnet test $project -c Release --filter 'FullyQualifiedName~ProfilePersistenceTest' --logger 'trx;LogFileName=profile-contract.trx' *> (Join-Path $run 'red.log')
if ($LASTEXITCODE -ne 1) { throw "Expected failing behavioral tests, evidence: $run" }
[xml]$trx = Get-Content -LiteralPath (Join-Path $run 'TestResults/profile-contract.trx') -Raw
$results = $trx.SelectNodes("//*[local-name()='UnitTestResult']")
$failed = @($results | Where-Object outcome -eq 'Failed' | ForEach-Object testName | Sort-Object)
$expected = @('DefaultModernProfileDoesNotTargetLegacySettings', 'KnownNamesInsideUnknownNodesDoNotChangeSettings', 'MalformedRestoreKeepsLastValidInMemoryValuesAndOriginalBytes', 'UnknownAttributesAndPluginNodesSurviveTwoRestarts') | Sort-Object
if ($results.Count -ne 8 -or @(Compare-Object $failed $expected).Count -ne 0) { throw "Unexpected test outcomes, inspect $run" }
Write-Output 'Verified legacy behavior baseline: 4 passed / 4 failed / 0 skipped. This is not a passing migration gate.'
$failed | ForEach-Object { Write-Output "Expected red: $_" }
& dotnet test $project -c Release --no-build --filter 'FullyQualifiedName~CustomFormatEditingTest' --logger 'trx;LogFileName=format-editing.trx' *> (Join-Path $run 'editing.log')
if ($LASTEXITCODE -ne 0) { throw "Custom format editing regression failed, inspect $run" }
[xml]$editingTrx = Get-Content -LiteralPath (Join-Path $run 'TestResults/format-editing.trx') -Raw
$editingResults = $editingTrx.SelectNodes("//*[local-name()='UnitTestResult']")
if ($editingResults.Count -ne 5 -or @($editingResults | Where-Object outcome -ne 'Passed').Count -ne 0) { throw "Expected five passing editor cases, inspect $run" }
Write-Output 'Source-linked custom-format edit regressions: 5 passed.'
Write-Output "Evidence: $run"
