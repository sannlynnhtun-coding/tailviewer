$ErrorActionPreference = 'Stop'
$repo = (Resolve-Path (Join-Path $PSScriptRoot '../../../..')).Path
$run = Join-Path $repo ('artifacts/migration/storage-probe/run-' + [Guid]::NewGuid().ToString('N'))
New-Item -ItemType Directory -Path $run -Force | Out-Null
Copy-Item -LiteralPath (Join-Path $PSScriptRoot 'Program.cs') -Destination $run
$template = Get-Content -LiteralPath (Join-Path $PSScriptRoot 'StorageProbe.csproj.template') -Raw
$template.Replace('@@ROOT@@', [System.Security.SecurityElement]::Escape($repo.Replace('\','/'))) | Set-Content -LiteralPath (Join-Path $run 'StorageProbe.csproj')
function Run([string]$Executable, [string[]]$Arguments, [string]$Log, [int]$Expected = 0) {
    & $Executable @Arguments *> (Join-Path $run $Log)
    if ($LASTEXITCODE -ne $Expected) { throw "Unexpected exit $LASTEXITCODE ($Log); evidence: $run" }
}
Run 'dotnet' @('build', (Join-Path $run 'StorageProbe.csproj'), '-c', 'Release', '-v', 'minimal') 'build.log'
$legacy = Join-Path $run 'bin/Release/net452/repository.exe'
$modern = Join-Path $run 'bin/Release/net10.0/repository.dll'
# SDK legacy package content flattens an x86 copy; the probe host is Windows x64.
Copy-Item -LiteralPath (Join-Path $run 'bin/Release/net452/x64/SQLite.Interop.dll') -Destination (Join-Path $run 'bin/Release/net452/SQLite.Interop.dll') -Force
$nullDb = Join-Path $run 'legacy-valid.db'
Run $legacy @('create', $nullDb, 'null') 'null-create.log'
$nullHash = (Get-FileHash -LiteralPath $nullDb).Hash
Run $legacy @('export', $nullDb, (Join-Path $run 'legacy-null-export')) 'null-legacy-export.log' 1
Copy-Item -LiteralPath $nullDb -Destination (Join-Path $run 'modern-null-copy.db')
Run 'dotnet' @($modern, 'export', (Join-Path $run 'modern-null-copy.db'), (Join-Path $run 'modern-null-export')) 'null-modern-export.log' 1
foreach ($name in @('null-legacy-export.log', 'null-modern-export.log')) {
    if (!(Select-String -LiteralPath (Join-Path $run $name) -SimpleMatch 'System.InvalidCastException')) { throw "Unexpected null failure: $name" }
}
if ((Get-FileHash -LiteralPath $nullDb).Hash -ne $nullHash) { throw 'Source changed' }
$emptyDb = Join-Path $run 'legacy-empty.db'
Run $legacy @('create', $emptyDb) 'empty-create.log'
Run $legacy @('export', $emptyDb, (Join-Path $run 'legacy-empty-export')) 'empty-legacy-export.log'
Copy-Item -LiteralPath $emptyDb -Destination (Join-Path $run 'modern-empty-copy.db')
Run 'dotnet' @($modern, 'export', (Join-Path $run 'modern-empty-copy.db'), (Join-Path $run 'modern-empty-export')) 'empty-modern-export.log'
Run 'dotnet' @($modern, 'export', (Join-Path $run 'modern-empty-copy.db'), (Join-Path $run 'modern-empty-export')) 'empty-modern-reopen.log'
Run 'dotnet' @($modern, 'neutral', (Join-Path $run 'legacy-empty-export')) 'neutral-modern-read.log'
Run 'python' @((Join-Path $PSScriptRoot 'verify.py'), $run) 'verify.log'
Get-Content -LiteralPath (Join-Path $run 'verify.log')
Get-Content -LiteralPath (Join-Path $run 'neutral-modern-read.log')
Write-Output "PASS T009 evidence: $run"
