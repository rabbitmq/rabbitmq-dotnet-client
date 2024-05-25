[CmdletBinding(PositionalBinding=$false)]
param(
    [switch]$RunTests,
    [switch]$RunTestsUntilFailure
)

$ErrorActionPreference = 'Stop'
Set-StrictMode -Version Latest
$PSNativeCommandUseErrorActionPreference = $true

Write-Host "Run Parameters:" -ForegroundColor Cyan
Write-Host "`tPSScriptRoot: $PSScriptRoot"
Write-Host "`tRunTests: $RunTests"
Write-Host "`tRunTestsUntilFailure: $RunTestsUntilFailure"
Write-Host "`tdotnet --version: $(dotnet --version)"

Write-Host "[INFO] building all projects (Build.csproj traversal)..." -ForegroundColor "Magenta"
dotnet build "$PSScriptRoot\Build.csproj"
Write-Host "[INFO] done building." -ForegroundColor "Green"

if ($RunTests -or $RunTestsUntilFailure)
{
    $tests_dir = Join-Path -Path $PSScriptRoot -ChildPath 'projects' | Join-Path -ChildPath 'Test'
    $unit_csproj_file = Resolve-Path -LiteralPath (Join-Path -Path $tests_dir -ChildPath 'Unit' | Join-Path -ChildPath 'Unit.csproj')
    $integration_csproj_file = Resolve-Path -LiteralPath (Join-Path -Path $tests_dir -ChildPath 'Integration' | Join-Path -ChildPath 'Integration.csproj')
    $sequential_integration_csproj_file = Resolve-Path -LiteralPath (Join-Path -Path $tests_dir -ChildPath 'SequentialIntegration' | Join-Path -ChildPath 'SequentialIntegration.csproj')

    Do
    {
        foreach ($csproj_file in $unit_csproj_file, $integration_csproj_file, $sequential_integration_csproj_file)
        {
            Write-Host "[INFO] running Unit / Integration tests from '$csproj_file' (all frameworks)" -ForegroundColor "Magenta"
            & dotnet test $csproj_file --environment 'RABBITMQ_LONG_RUNNING_TESTS=true' --no-restore --no-build --logger "console;verbosity=detailed"
            if ($LASTEXITCODE -ne 0)
            {
                Write-Host "[ERROR] tests errored, exiting" -Foreground "Red"
                Exit 1
            }
            else
            {
                Write-Host "[INFO] tests passed" -ForegroundColor "Green"
            }
        }
    } While ($RunTestsUntilFailure)
}
