$ProgressPreference = 'Continue'
$ErrorActionPreference = 'Stop'
Set-StrictMode -Version 2.0

$erlang_reg_path = 'HKLM:\SOFTWARE\Ericsson\Erlang'
if (Test-Path 'HKLM:\SOFTWARE\WOW6432Node\')
{
    $erlang_reg_path = 'HKLM:\SOFTWARE\WOW6432Node\Ericsson\Erlang'
}
$erlang_erts_version = Get-ChildItem -Path $erlang_reg_path -Name
$erlang_home = (Get-ItemProperty -LiteralPath $erlang_reg_path\$erlang_erts_version).'(default)'

Write-Host "[INFO] Setting ERLANG_HOME to '$erlang_home'..."
$env:ERLANG_HOME = $erlang_home
[Environment]::SetEnvironmentVariable('ERLANG_HOME', $erlang_home, 'Machine')

$rabbitmq_base_path = (Get-ItemProperty -Name Install_Dir -Path 'HKLM:\SOFTWARE\WOW6432Node\VMware, Inc.\RabbitMQ Server').Install_Dir
$regPath = 'HKLM:\SOFTWARE\Microsoft\Windows\CurrentVersion\Uninstall\RabbitMQ'
if (Test-Path 'HKLM:\SOFTWARE\WOW6432Node\')
{
    $regPath = 'HKLM:\SOFTWARE\WOW6432Node\Microsoft\Windows\CurrentVersion\Uninstall\RabbitMQ'
}
$rabbitmq_version = (Get-ItemProperty $regPath "DisplayVersion").DisplayVersion
$rabbitmqctl_path = Resolve-Path -LiteralPath (Join-Path -Path $rabbitmq_base_path -ChildPath "rabbitmq_server-$rabbitmq_version" | Join-Path -ChildPath 'sbin' | Join-Path -ChildPath 'rabbitmqctl.bat')

Write-Host "[INFO] Setting RABBITMQ_RABBITMQCTL_PATH to '$rabbitmqctl_path'..."
$env:RABBITMQ_RABBITMQCTL_PATH = $rabbitmqctl_path
[Environment]::SetEnvironmentVariable('RABBITMQ_RABBITMQCTL_PATH', $rabbitmqctl_path, 'Machine')

New-Variable -Name ci_dir -Option Constant -Value (Join-Path -Path $env:GITHUB_WORKSPACE -ChildPath '.ci')
New-Variable -Name certs_dir -Option Constant -Value (Join-Path -Path $ci_dir -ChildPath 'certs')

$csproj_file = Resolve-Path -LiteralPath (Join-Path -Path $env:GITHUB_WORKSPACE -ChildPath 'projects' | Join-Path -ChildPath 'Unit' | Join-Path -ChildPath 'Unit.csproj')
dotnet test --environment RABBITMQ_RABBITMQCTL_PATH=$rabbitmqctl_path --environment PASSWORD=grapefruit --environment SSL_CERTS_DIR=$certs_dir $csproj_file --no-restore --no-build --logger "console;verbosity=detailed"
