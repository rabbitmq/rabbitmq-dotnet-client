<#
.SYNOPSIS
    Cold-start gate for issue #1960 (abort loses the SetCloseReason race).

.DESCRIPTION
    On net472 the #1960 failure is a COLD-START race: only the first connection
    after process start loses it, because the abort code path is not yet
    JIT-compiled and takes ~10ms -- the window in which the already-warm MainLoop
    wins SetCloseReason with a Library reason. Warm iterations win the race in
    ~0.1ms, so an in-process loop only ever shows "1/N". To exercise the race
    deterministically here, run ONE iteration per FRESH process.

    This script builds the repro ONCE, then runs it -Count times as separate
    processes (one cold iteration each) and tallies the non-zero exit codes the
    app returns on a timeout.

    Pre-fix: expect ~Count/Count failures. Post-fix: expect ~0/Count.

    The underlying bug is NOT net472-specific -- it is a sequential-shutdown-handler
    deadlock on _mainLoopCts.Token. `dotnet run ... -- <host> <n> force-race`
    reproduces it on any platform and TFM without needing a cold process; net472
    just loses the race reliably. See README.md.

.PARAMETER Host_
    Broker hostname (default: localhost).

.PARAMETER Count
    Number of fresh-process runs (default: 20).

.EXAMPLE
    .\repro.ps1
    .\repro.ps1 -Host_ localhost -Count 50
#>
param(
    [string] $Host_ = 'localhost',
    [int]    $Count = 20
)

$ErrorActionPreference = 'Stop'
$proj = $PSScriptRoot

Write-Host "Building GH-1960 (net472, Release) once..." -ForegroundColor Cyan
dotnet build -c Release -f net472 "$proj\GH-1960.csproj" | Out-Null
if ($LASTEXITCODE -ne 0) {
    Write-Host "BUILD FAILED (exit $LASTEXITCODE)" -ForegroundColor Red
    exit 1
}

$fail = 0
for ($i = 0; $i -lt $Count; $i++) {
    # --no-build keeps every process cold at the CLR level without rebuilding.
    # One iteration per process (arg 2 = 1) so each run is a fresh cold start.
    dotnet run -c Release -f net472 --no-build --project $proj -- $Host_ 1 2>&1 | Out-Null
    if ($LASTEXITCODE -ne 0) {
        $fail++
        Write-Host ("run {0,3}: FAIL (exit {1})" -f $i, $LASTEXITCODE) -ForegroundColor Red
    } else {
        Write-Host ("run {0,3}: pass" -f $i) -ForegroundColor DarkGray
    }
}

Write-Host ""
$color = if ($fail -eq 0) { 'Green' } else { 'Yellow' }
Write-Host ("cold-start failures: {0} / {1}" -f $fail, $Count) -ForegroundColor $color
