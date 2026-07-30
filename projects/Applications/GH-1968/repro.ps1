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
      * slow  - the test passed but its duration exceeded -SlowSeconds.
                Connection.CloseAsync raises any non-abort timeout below 30s up to 30s,
                so the test's own 6s _waitSpan is ignored and a run that waits out the
                full timeout is approaching the failure regardless of how it ends.
                This is the test's own duration from the trx, not process wall clock,
                which is dominated by startup overhead.

    Outcome, duration and failure message all come from the trx (`--logger trx`), so
    nothing here depends on console formatting.

    One `dotnet test` per iteration, so every iteration is a fresh process. That is
    deliberate: issue #1960 was a cold-start race on net472 that an in-process loop
    reported as 1/N. It costs roughly 15s of startup per iteration.

    Requires a broker reachable at localhost:5672. The test fixture hardcodes that
    and has no host override, so under WSL2 this depends on localhost forwarding.

.PARAMETER Configuration
    Build configuration (default: Debug, which is what CI uses). CI's
    integration-win32 job builds Debug and passes this test on net472, so Debug is
    the configuration to match when comparing against a known-green baseline. Pass
    Release to check for configuration-dependent behaviour.

.PARAMETER Count
    Number of runs (default: 25).

.PARAMETER SlowSeconds
    Test duration above which a passing run is flagged as slow (default: 5).
    Healthy runs finish in well under a second.

.PARAMETER LogDirectory
    Where to write per-run trx and console logs (default: a GH-1968 directory under
    the temp path). Only failing and slow runs are kept.

.EXAMPLE
    .\repro.ps1
    .\repro.ps1 -Count 100 -SlowSeconds 2
    .\repro.ps1 -Configuration Release -Count 10
#>
[CmdletBinding(PositionalBinding = $false)]
param(
    [ValidateSet('Debug', 'Release')]
    [string] $Configuration = 'Debug',
    [int]    $Count = 25,
    [int]    $SlowSeconds = 5,
    [string] $LogDirectory = (Join-Path ([System.IO.Path]::GetTempPath()) 'GH-1968')
)

$ErrorActionPreference = 'Stop'
Set-StrictMode -Version Latest

# Deliberately the opposite of build.ps1. A failing `dotnet test` is the measurement
# here, not an error, so a non-zero exit must not terminate the loop.
$PSNativeCommandUseErrorActionPreference = $false

# $IsWindows exists only on PowerShell Core, where this normally runs. Under Windows
# PowerShell it is absent, and absent means Windows, so the edition is checked first to
# avoid dereferencing an undefined variable under Set-StrictMode.
if ($PSVersionTable.PSEdition -eq 'Core' -and -not $IsWindows) {
    Write-Host "net472 is Windows-only, so this script cannot reproduce #1968 here." -ForegroundColor Red
    exit 1
}

$repoRoot = Resolve-Path -LiteralPath (Join-Path $PSScriptRoot '..\..\..')
$proj = Join-Path $repoRoot 'projects\Test\Integration\Integration.csproj'
$testName = 'TestCleanClosureWithSocketClosedOutOfBand'

New-Item -ItemType Directory -Force -Path $LogDirectory | Out-Null
# XmlDocument.Load resolves relative paths against the process working directory,
# not PowerShell's, so make this absolute before handing it out.
$LogDirectory = (Resolve-Path -LiteralPath $LogDirectory).Path

Write-Host "Building Integration (net472, $Configuration) once..." -ForegroundColor Magenta
dotnet build -c $Configuration -f net472 $proj | Out-Null
if ($LASTEXITCODE -ne 0) {
    Write-Host "BUILD FAILED (exit $LASTEXITCODE)" -ForegroundColor Red
    exit 1
}

# Reads one test's outcome, duration and failure message out of a trx. Console output
# is unusable for this: at default verbosity a net472 pass prints no per-test line and
# leaves the summary Duration field empty, and a failure reports its time on a per-test
# line instead of in the summary. The trx has a duration attribute either way.
function Get-TestResult {
    param([string] $Path, [string] $TestName)

    $doc = New-Object System.Xml.XmlDocument
    $doc.Load($Path)

    $ns = New-Object System.Xml.XmlNamespaceManager($doc.NameTable)
    $ns.AddNamespace('t', 'http://microsoft.com/schemas/VisualStudio/TeamTest/2010')

    foreach ($node in $doc.SelectNodes('//t:UnitTestResult', $ns)) {
        if ($node.GetAttribute('testName') -notlike "*$TestName*") {
            continue
        }

        # duration is an invariant "00:00:00.0275298", present on passes and failures
        # alike. Parsed defensively so a format change degrades to an unknown timing
        # rather than terminating the loop.
        $seconds = $null
        $duration = $node.GetAttribute('duration')
        if ($duration) {
            try {
                $seconds = ([TimeSpan]::Parse($duration,
                    [System.Globalization.CultureInfo]::InvariantCulture)).TotalSeconds
            } catch {
                $seconds = $null
            }
        }

        # ErrorInfo is absent on a pass.
        $message = ''
        $stack = ''
        $messageNode = $node.SelectSingleNode('t:Output/t:ErrorInfo/t:Message', $ns)
        if ($messageNode) { $message = $messageNode.InnerText }
        $stackNode = $node.SelectSingleNode('t:Output/t:ErrorInfo/t:StackTrace', $ns)
        if ($stackNode) { $stack = $stackNode.InnerText }

        return [pscustomobject] @{
            Outcome = $node.GetAttribute('outcome')
            Seconds = $seconds
            Message = $message
            Stack   = $stack
        }
    }

    return $null
}

# Shortens a failure to one line, so the summary can distinguish an actual #1968
# timeout from an unrelated failure without opening every log.
function Get-FailureReason {
    param($Result)

    $text = $Result.Message + "`n" + $Result.Stack

    if ($text -match 'OperationCanceledException') {
        return 'OperationCanceledException (#1968)'
    }

    # xunit's assertion messages put the offending type or value on an "Actual:" line,
    # which identifies the failure far better than the first line does.
    if ($text -match '(?m)^\s*Actual:\s+(.+)$') {
        return 'assertion, actual ' + $Matches[1].Trim()
    }

    foreach ($line in $Result.Message -split "`r?`n") {
        if (-not [string]::IsNullOrWhiteSpace($line)) {
            return $line.Trim()
        }
    }

    return 'unrecognized, see log'
}

$fail = 0
$slow = 0
$unknown = 0
$reasons = @{}
for ($i = 1; $i -le $Count; $i++) {
    $stem = "run-{0:d3}" -f $i
    $log = Join-Path $LogDirectory "$stem.log"
    $trx = Join-Path $LogDirectory "$stem.trx"

    Remove-Item -LiteralPath $trx -ErrorAction SilentlyContinue

    dotnet test -c $Configuration -f net472 --no-build $proj `
        --filter "FullyQualifiedName~$testName" `
        --results-directory $LogDirectory `
        --logger "trx;LogFileName=$stem.trx" *>&1 |
        Out-File -LiteralPath $log -Encoding utf8
    $exit = $LASTEXITCODE

    $result = $null
    if (Test-Path -LiteralPath $trx) {
        $result = Get-TestResult -Path $trx -TestName $testName
    }

    # No result for this test means either a filter that matched nothing, which exits 0
    # and writes a trx with no UnitTestResult nodes, so it would otherwise read as a
    # clean run repeated -Count times, or a test host that died before writing a trx.
    # Neither is a measurement, so stop rather than tally it.
    if ($null -eq $result) {
        Write-Host ("run {0,3}: no result for '{1}' (exit {2})" -f $i, $testName, $exit) -ForegroundColor Red
        Write-Host "Was the test renamed, or did the test host crash?" -ForegroundColor Red
        Write-Host "log: $log" -ForegroundColor Red
        exit 1
    }

    # A skipped test is NotExecuted with no ErrorInfo, so counting it as a failure would
    # invent a reason for something that never ran.
    if ($result.Outcome -eq 'NotExecuted') {
        Write-Host ("run {0,3}: '{1}' was skipped, so nothing was measured." -f $i, $testName) -ForegroundColor Red
        Write-Host "log: $log" -ForegroundColor Red
        exit 1
    }

    $secs = $result.Seconds
    $shown = if ($null -eq $secs) { '     ?' } else { '{0,6:n2}' -f $secs }

    if ($result.Outcome -ne 'Passed') {
        $fail++
        $reason = Get-FailureReason -Result $result
        if ($reasons.ContainsKey($reason)) { $reasons[$reason]++ } else { $reasons[$reason] = 1 }
        Write-Host ("run {0,3}: {1}  {2}s  {3}" -f $i, $result.Outcome.ToUpperInvariant(), $shown, $reason) `
            -ForegroundColor Red
    } elseif ($null -eq $secs) {
        # Kept distinct from slow rather than lumped in with it: an unparsed duration
        # means the timing is unknown, not that it was long, and calling it slow
        # reports a signal that was never measured.
        $unknown++
        Write-Host ("run {0,3}: pass, no duration in trx  {1}" -f $i, $trx) -ForegroundColor Yellow
    } elseif ($secs -gt $SlowSeconds) {
        $slow++
        Write-Host ("run {0,3}: slow  {1}s  {2}" -f $i, $shown, $log) -ForegroundColor Yellow
    } else {
        Write-Host ("run {0,3}: pass  {1}s" -f $i, $shown) -ForegroundColor DarkGray
        Remove-Item -LiteralPath $log, $trx -ErrorAction SilentlyContinue
    }
}

Write-Host ""
$color = if ($fail -eq 0 -and $slow -eq 0) { 'Green' } else { 'Yellow' }
Write-Host ("{0}, net472    failures: {1} / {2}    slow passes: {3} / {2}" -f `
    $Configuration, $fail, $Count, $slow) -ForegroundColor $color
if ($unknown -gt 0) {
    Write-Host ("{0} run(s) passed with no duration recorded." -f $unknown) -ForegroundColor Yellow
}

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

if ($fail -gt 0 -and $Configuration -eq 'Release') {
    Write-Host ""
    Write-Host "This was a Release run. CI builds Debug and passes this test on net472," -ForegroundColor Magenta
    Write-Host "so compare against -Configuration Debug before concluding anything." -ForegroundColor Magenta
}

if ($fail -gt 0 -or $slow -gt 0 -or $unknown -gt 0) {
    Write-Host ""
    Write-Host "logs: $LogDirectory" -ForegroundColor Magenta
}
