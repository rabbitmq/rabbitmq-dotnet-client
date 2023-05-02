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

if ($RunTests) {
    Write-Host "Running tests: Build.csproj traversal (all frameworks)" -ForegroundColor "Magenta"
    dotnet test "$PSScriptRoot\Build.csproj" --no-build --logger "console;verbosity=detailed"
    if ($LastExitCode -ne 0) {
        Write-Host "Error with tests, aborting build." -Foreground "Red"
        Exit 1
    }
    Write-Host "Tests passed!" -ForegroundColor "Green"
}

Write-Host "Done."
