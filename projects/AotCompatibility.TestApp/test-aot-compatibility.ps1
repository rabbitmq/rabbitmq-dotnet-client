$DebugPreference = "Continue"
$ErrorActionPreference = 'Stop'
# Set-PSDebug -Strict -Trace 1
Set-PSDebug -Off
Set-StrictMode -Version 'Latest' -ErrorAction 'Stop' -Verbose

New-Variable -Name rootDirectory  -Option Constant -Value $PSScriptRoot
Write-Host "[INFO] rootDirectory: $rootDirectory"

$runtime = $IsWindows ? "win-x64" : ($IsMacOS ? "macos-x64" : "linux-x64")
$app = $IsWindows ? "./AotCompatibility.TestApp.exe" : "./AotCompatibility.TestApp"

$publishOutput = dotnet publish --runtime=$runtime $rootDirectory/AotCompatibility.TestApp.csproj -nodeReuse:false '/p:UseSharedCompilation=false' '/p:Configuration=Release'

$actualWarningCount = 0

foreach ($line in $($publishOutput -split "`r`n"))
{
    if (($line -like "*analysis warning IL*") -or ($line -like "*analysis error IL*"))
    {
        Write-Host $line
        $actualWarningCount += 1
    }
}

Write-Host "Actual warning count is:", $actualWarningCount
$expectedWarningCount = 0

if ($LastExitCode -ne 0)
{
    Write-Error -ErrorAction Continue -Message "[ERROR] error while publishing AotCompatibility Test App, LastExitCode is $LastExitCode"
    Write-Error -ErrorAction Continue -Message $publishOutput
}

Push-Location "$rootDirectory/bin/Release/net6.0/$runtime"
try
{
    Write-Host "[INFO] executing: $app"
    $app
    Write-Host "[INFO] finished executing test app"

    if ($LastExitCode -ne 0)
    {
        Write-Error -ErrorAction Continue -Message "[ERROR] there was an error while executing AotCompatibility Test App. LastExitCode is: $LastExitCode"
    }
}
finally
{
    Pop-Location
}

$exitCode = 0
if ($actualWarningCount -ne $expectedWarningCount)
{
    $exitCode = 1
    Write-Error -ErrorAction Continue -Message "Actual warning count: $actualWarningCount is not as expected, which is: $expectedWarningCount"
}

Exit $exitCode
