$ErrorActionPreference = 'Stop'
$repo = (Resolve-Path (Join-Path $PSScriptRoot '../../../..')).Path
$run = Join-Path $repo ('artifacts/migration/profile-probe/run-' + [Guid]::NewGuid().ToString('N'))
New-Item -ItemType Directory -Path $run -Force | Out-Null
Copy-Item -LiteralPath (Join-Path $PSScriptRoot 'Namespace.cs') -Destination $run
$template = Get-Content -LiteralPath (Join-Path $PSScriptRoot 'ProfileProbe.Tests.csproj.template') -Raw
$project = Join-Path $run 'ProfileProbe.Tests.csproj'
$template.Replace('@@ROOT@@', [System.Security.SecurityElement]::Escape($repo.Replace('\','/'))) | Set-Content -LiteralPath $project
& dotnet test $project -c Release --filter 'FullyQualifiedName~ProfilePersistenceTest' --logger 'trx;LogFileName=profile-contract.trx' *> (Join-Path $run 'profile.log')
if ($LASTEXITCODE -ne 0) { throw "Modern profile contract tests failed, evidence: $run" }
[xml]$trx = Get-Content -LiteralPath (Join-Path $run 'TestResults/profile-contract.trx') -Raw
$results = $trx.SelectNodes("//*[local-name()='UnitTestResult']")
if ($results.Count -ne 8 -or @($results | Where-Object outcome -ne 'Passed').Count -ne 0) { throw "Expected eight passing modern profile cases, inspect $run" }
Write-Output 'Modern profile behavior: 8 passed / 0 failed / 0 skipped. Atomic file-store safety remains a separate gate.'
& dotnet test $project -c Release --no-build --filter 'FullyQualifiedName~CustomFormatEditingTest' --logger 'trx;LogFileName=format-editing.trx' *> (Join-Path $run 'editing.log')
if ($LASTEXITCODE -ne 0) { throw "Custom format editing regression failed, inspect $run" }
[xml]$editingTrx = Get-Content -LiteralPath (Join-Path $run 'TestResults/format-editing.trx') -Raw
$editingResults = $editingTrx.SelectNodes("//*[local-name()='UnitTestResult']")
if ($editingResults.Count -ne 5 -or @($editingResults | Where-Object outcome -ne 'Passed').Count -ne 0) { throw "Expected five passing editor cases, inspect $run" }
Write-Output 'Source-linked custom-format edit regressions: 5 passed.'
Write-Output "Evidence: $run"
