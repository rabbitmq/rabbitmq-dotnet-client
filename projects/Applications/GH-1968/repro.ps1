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
      * FAIL  - the test failed. This is NOT automatically #1968. Only a failure whose
                exception is OperationCanceledException is; the script prints the
                distinct failure reasons it saw so an unrelated failure is obvious.
                A deterministic 100% failure rate is a different bug by definition,
                since #1968 is intermittent, and it masks #1968 entirely because the
                run never reaches the timeout.
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

# Sums every value/unit pair in a duration string, so "1 m 5 s" is 65 rather than 60.
function ConvertTo-Seconds {
    param([string] $Text)

    if ([string]::IsNullOrWhiteSpace($Text)) {
        return $null
    }

    $total = 0.0
    $found = $false
    foreach ($m in [regex]::Matches($Text, '(\d+(?:[.,]\d+)?)\s*(ms|s|m|h)\b')) {
        $value = [double]::Parse($m.Groups[1].Value.Replace(',', ''),
            [System.Globalization.CultureInfo]::InvariantCulture)
        switch ($m.Groups[2].Value) {
            'ms' { $total += $value / 1000.0 }
            's'  { $total += $value }
            'm'  { $total += $value * 60.0 }
            'h'  { $total += $value * 3600.0 }
        }
        $found = $true
    }

    if ($found) { return $total } else { return $null }
}

# Two sources, because neither alone covers both outcomes:
#
#   pass:  Passed!  - Failed: 0, ... Duration: 98 ms - Integration.dll (net472)
#   fail:    Failed Test.Integration...TestName [86 ms]
#            Failed!  - Failed: 1, ... Duration:  - Integration.dll (net472)
#
# A failing run leaves the summary Duration field EMPTY and reports the time on a
# per-test line instead. Reading only the summary therefore lost the duration on
# exactly the runs that matter, which is the signal that separates a ~30s close
# timeout from a fast assertion failure. Prefer the per-test line, since it is the
# test's own time rather than the assembly's, and fall back to the summary.
function Get-ReportedSeconds {
    param([string] $Path)

    $lines = @(Get-Content -LiteralPath $Path)

    foreach ($line in $lines) {
        if ($line -match '^\s+(?:Failed|Passed)\s+\S+\s+\[(.+?)\]\s*$') {
            $secs = ConvertTo-Seconds -Text $Matches[1]
            if ($null -ne $secs) {
                return $secs
            }
        }
    }

    foreach ($line in $lines) {
        if ($line -match 'Duration:\s*(.*?)\s+-\s+\S+\.dll') {
            $secs = ConvertTo-Seconds -Text $Matches[1]
            if ($null -ne $secs) {
                return $secs
            }
        }
    }

    return $null
}

# Extracts a short reason for a failing run, so the summary can distinguish an
# actual #1968 timeout from an unrelated failure without opening every log.
function Get-FailureReason {
    param([string] $Path)

    if (Select-String -LiteralPath $Path -Pattern 'OperationCanceledException' -Quiet) {
        return 'OperationCanceledException (#1968)'
    }

    $assert = Select-String -LiteralPath $Path -Pattern '^\s*Actual:\s+(.+)$' |
        Select-Object -First 1
    if ($assert) {
        return 'assertion, actual ' + $assert.Matches[0].Groups[1].Value.Trim()
    }

    $err = Select-String -LiteralPath $Path -Pattern '^\s*Error Message:\s*$' -Context 0, 1 |
        Select-Object -First 1
    if ($err -and $err.Context.PostContext.Count -gt 0) {
        return $err.Context.PostContext[0].Trim()
    }

    return 'unrecognized, see log'
}

$fail = 0
$slow = 0
$reasons = @{}
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
        $reason = Get-FailureReason -Path $log
        if ($reasons.ContainsKey($reason)) { $reasons[$reason]++ } else { $reasons[$reason] = 1 }
        Write-Host ("run {0,3}: FAIL  {1}s  {2}" -f $i, $shown, $reason) -ForegroundColor Red
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

if ($fail -gt 0) {
    Write-Host ""
    Write-Host "failure reasons:" -ForegroundColor Magenta
    foreach ($entry in $reasons.GetEnumerator() | Sort-Object -Property Value -Descending) {
        Write-Host ("  {0,3}x  {1}" -f $entry.Value, $entry.Key)
    }

    $is1968 = $reasons.Keys | Where-Object { $_ -like '*#1968*' }
    if (-not $is1968) {
        Write-Host ""
        Write-Host "None of these are #1968, which is an OperationCanceledException." -ForegroundColor Yellow
        Write-Host "Fix the failure above first; while it fails the run never reaches the" -ForegroundColor Yellow
        Write-Host "close timeout, so #1968 cannot be observed at all." -ForegroundColor Yellow
    } elseif ($fail -eq $Count) {
        Write-Host ""
        Write-Host "A 100% rate is suspicious for #1968, which is intermittent." -ForegroundColor Yellow
    }
}

if ($fail -gt 0 -or $slow -gt 0) {
    Write-Host ""
    Write-Host "logs: $LogDirectory" -ForegroundColor Magenta
}
