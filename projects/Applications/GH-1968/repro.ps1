<#
.SYNOPSIS
    Measures the failure rate of issue #1968 on net472.

.DESCRIPTION
    TestCleanClosureWithSocketClosedOutOfBand intermittently fails on net472 with a
    bare OperationCanceledException, which none of the test's catch blocks handle.

    This CANNOT reproduce on net8.0. The throwing frame is TaskExtensions.DoWaitAsync,
    which lives inside `#if !NET`, so on net8.0 the built-in Task.WaitAsync is used
    instead. net472 exists as a target framework only on Windows, and the net472 test
    binaries run against the netstandard2.0 client build.

    Two signatures, both reported:
      * FAIL  - the test itself failed. Check the saved log for the exception type;
                OperationCanceledException is #1968, anything else is not.
      * slow  - the test passed but its reported duration exceeded -SlowSeconds.
                Connection.CloseAsync raises any non-abort timeout below 30s up to 30s,
                so the test's own 6s _waitSpan is ignored and a run that waits out the
                full timeout is approaching the failure regardless of how it ends.
                This reads the duration out of the `dotnet test` summary line rather
                than timing the process, since process wall clock is dominated by
                startup overhead.

    One `dotnet test` per iteration, so every iteration is a fresh process. That is
    deliberate: issue #1960 was a cold-start race on net472 that an in-process loop
    reported as 1/N. It costs roughly 15s of startup per iteration.

    Requires a broker reachable at localhost:5672. The test fixture hardcodes that
    and has no host override, so under WSL2 this depends on localhost forwarding.

.PARAMETER Count
    Number of runs (default: 25).

.PARAMETER SlowSeconds
    Reported test duration above which a passing run is flagged as slow (default: 5).
    Healthy runs finish in well under a second.

.PARAMETER LogDirectory
    Where to write per-run logs (default: a GH-1968 directory under the temp path).
    Only failing and slow runs are kept.

.EXAMPLE
    .\repro.ps1
    .\repro.ps1 -Count 100 -SlowSeconds 2
#>
[CmdletBinding(PositionalBinding = $false)]
param(
    [int]    $Count = 25,
    [int]    $SlowSeconds = 5,
    [string] $LogDirectory = (Join-Path ([System.IO.Path]::GetTempPath()) 'GH-1968')
)

$ErrorActionPreference = 'Stop'
Set-StrictMode -Version Latest

# Deliberately the opposite of build.ps1. A failing `dotnet test` is the measurement
# here, not an error, so a non-zero exit must not terminate the loop.
$PSNativeCommandUseErrorActionPreference = $false

# $IsWindows only exists on PowerShell Core. On Windows PowerShell 5.1 it is
# absent, and absent means Windows, so check the edition before dereferencing it
# under Set-StrictMode.
if ($PSVersionTable.PSEdition -eq 'Core' -and -not $IsWindows) {
    Write-Host "net472 is Windows-only, so this script cannot reproduce #1968 here." -ForegroundColor Red
    exit 1
}

$repoRoot = Resolve-Path -LiteralPath (Join-Path $PSScriptRoot '..\..\..')
$proj = Join-Path $repoRoot 'projects\Test\Integration\Integration.csproj'
$testName = 'TestCleanClosureWithSocketClosedOutOfBand'

New-Item -ItemType Directory -Force -Path $LogDirectory | Out-Null

Write-Host "Building Integration (net472, Release) once..." -ForegroundColor Magenta
dotnet build -c Release -f net472 $proj | Out-Null
if ($LASTEXITCODE -ne 0) {
    Write-Host "BUILD FAILED (exit $LASTEXITCODE)" -ForegroundColor Red
    exit 1
}

# Pulls the duration out of the summary line, e.g.
#   Passed!  - Failed: 0, Passed: 1, ... Duration: 158 ms - Integration.dll (net472)
# The unit varies (ms, s, m), so capture it and normalize. A "1 m 5 s" duration
# truncates to 60s, which is fine: this only feeds a threshold comparison, and
# anything reported in minutes is far past any threshold worth setting.
function Get-ReportedSeconds {
    param([string] $Path)

    $match = Select-String -LiteralPath $Path -Pattern 'Duration:\s+([\d.,]+)\s*(ms|s|m)\b' |
        Select-Object -First 1
    if (-not $match) {
        return $null
    }

    $value = [double]::Parse($match.Matches[0].Groups[1].Value.Replace(',', ''),
        [System.Globalization.CultureInfo]::InvariantCulture)
    switch ($match.Matches[0].Groups[2].Value) {
        'ms' { return $value / 1000.0 }
        's'  { return $value }
        'm'  { return $value * 60.0 }
    }
}

$fail = 0
$slow = 0
for ($i = 1; $i -le $Count; $i++) {
    $log = Join-Path $LogDirectory ("run-{0:d3}.log" -f $i)
    dotnet test -c Release -f net472 --no-build $proj `
        --filter "FullyQualifiedName~$testName" *>&1 |
        Out-File -LiteralPath $log -Encoding utf8
    $exit = $LASTEXITCODE

    # A filter that matches nothing exits 0 and prints no summary, which would otherwise
    # read as a clean run repeated -Count times. Fail loudly instead.
    if (Select-String -LiteralPath $log -Pattern 'No test matches the given testcase filter' -Quiet) {
        Write-Host "Filter '$testName' matched no tests. Was the test renamed?" -ForegroundColor Red
        Write-Host "log: $log" -ForegroundColor Red
        exit 1
    }

    $secs = Get-ReportedSeconds -Path $log

    # A run whose duration cannot be parsed is not treated as a pass: it means the run
    # never got as far as a summary line, which is itself worth looking at.
    $shown = if ($null -eq $secs) { '     ?' } else { '{0,6:n2}' -f $secs }

    if ($exit -ne 0) {
        $fail++
        Write-Host ("run {0,3}: FAIL  {1}s  {2}" -f $i, $shown, $log) -ForegroundColor Red
    } elseif ($null -eq $secs -or $secs -gt $SlowSeconds) {
        $slow++
        Write-Host ("run {0,3}: slow  {1}s  {2}" -f $i, $shown, $log) -ForegroundColor Yellow
    } else {
        Write-Host ("run {0,3}: pass  {1}s" -f $i, $shown) -ForegroundColor DarkGray
        Remove-Item -LiteralPath $log -ErrorAction SilentlyContinue
    }
}

Write-Host ""
$color = if ($fail -eq 0 -and $slow -eq 0) { 'Green' } else { 'Yellow' }
Write-Host ("failures: {0} / {1}    slow passes: {2} / {1}" -f $fail, $Count, $slow) -ForegroundColor $color
if ($fail -gt 0 -or $slow -gt 0) {
    $glob = Join-Path $LogDirectory '*.log'
    Write-Host "logs: $LogDirectory" -ForegroundColor Magenta
    Write-Host "classify: Select-String OperationCanceledException $glob" -ForegroundColor Magenta
}
