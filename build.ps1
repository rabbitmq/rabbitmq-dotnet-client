[CmdletBinding(PositionalBinding=$false)]
param(
    [switch]$RunTests
)

Write-Host "Run Parameters:" -ForegroundColor Cyan
Write-Host "`tPSScriptRoot: $PSScriptRoot"
Write-Host "`tRunTests: $RunTests"
Write-Host "`tdotnet --version: $(dotnet --version)"

Write-Host "Building all projects (Build.csproj traversal)..." -ForegroundColor "Magenta"
dotnet build "$PSScriptRoot\Build.csproj"
Write-Host "Done building." -ForegroundColor "Green"

if ($RunTests)
{
    $unit_csproj_file = Resolve-Path -LiteralPath (Join-Path -Path $PSScriptRoot -ChildPath 'projects' | Join-Path -ChildPath 'Unit' | Join-Path -ChildPath 'Unit.csproj')
    Write-Host "Running Unit / Integration tests from '$unit_csproj_file' (all frameworks)" -ForegroundColor "Magenta"
    dotnet test $unit_csproj_file --no-restore --no-build --logger "console;verbosity=detailed"
    if ($LastExitCode -ne 0) {
        Write-Host "Error with tests, aborting build." -Foreground "Red"
        Exit 1
    }
    Write-Host "Tests passed!" -ForegroundColor "Green"
}

Write-Host "Done."
