$ProgressPreference = 'Continue'
$VerbosePreference = 'Continue'
$ErrorActionPreference = 'Stop'
Set-StrictMode -Version 2.0

[Net.ServicePointManager]::SecurityProtocol = [Net.ServicePointManager]::SecurityProtocol -bor 'Tls12'

New-Variable -Name curdir  -Option Constant -Value $PSScriptRoot
Write-Host "[INFO] curdir: $curdir"

New-Variable -Name ci_dir -Option Constant -Value (Join-Path -Path $env:GITHUB_WORKSPACE -ChildPath '.ci')

New-Variable -Name certs_dir -Option Constant -Value (Join-Path -Path $ci_dir -ChildPath 'certs')

New-Variable -Name ci_windows_dir -Option Constant -Value (Join-Path -Path $ci_dir -ChildPath 'windows')

New-Variable -Name ca_certificate_file -Option Constant -Value `
    (Resolve-Path -LiteralPath (Join-Path -Path $certs_dir -ChildPath 'ca_certificate.pem'))

Write-Host "[INFO] importing CA cert from '$ca_certificate_file'"
Import-Certificate -Verbose -CertStoreLocation Cert:\LocalMachine\Root -FilePath $ca_certificate_file

New-Variable -Name versions_path -Option Constant -Value `
    (Resolve-Path -LiteralPath (Join-Path -Path $ci_windows_dir -ChildPath 'versions.json'))
$versions = Get-Content $versions_path | ConvertFrom-Json
Write-Host "[INFO] versions: $versions"
$erlang_ver = $versions.erlang
$rabbitmq_ver = $versions.rabbitmq

$base_installers_dir = Join-Path -Path $HOME -ChildPath 'installers'
if (-Not (Test-Path $base_installers_dir))
{
    New-Item -Verbose -ItemType Directory $base_installers_dir
}

$erlang_download_url = "https://github.com/erlang/otp/releases/download/OTP-$erlang_ver/otp_win64_$erlang_ver.exe"
$erlang_installer_path = Join-Path -Path $base_installers_dir -ChildPath "otp_win64_$erlang_ver.exe"
$erlang_install_dir = Join-Path -Path $HOME -ChildPath 'erlang'

Write-Host '[INFO] Downloading Erlang...'

if (-Not (Test-Path -LiteralPath $erlang_installer_path))
{
    Invoke-WebRequest -UseBasicParsing -Uri $erlang_download_url -OutFile $erlang_installer_path
}
else
{
    Write-Host "[INFO] Found '$erlang_installer_path' in cache!"
}

Write-Host "[INFO] Installing Erlang to $erlang_install_dir..."
& $erlang_installer_path '/S' "/D=$erlang_install_dir" | Out-Null

$rabbitmq_installer_download_url = "https://github.com/rabbitmq/rabbitmq-server/releases/download/v$rabbitmq_ver/rabbitmq-server-$rabbitmq_ver.exe"
$rabbitmq_installer_path = Join-Path -Path $base_installers_dir -ChildPath "rabbitmq-server-$rabbitmq_ver.exe"
Write-Host "[INFO] rabbitmq installer path $rabbitmq_installer_path"

if (Test-Path -LiteralPath 'HKLM:\SOFTWARE\WOW6432Node\')
{
    New-Variable -Name erlangRegKeyPath -Option Constant `
        -Value 'HKLM:\SOFTWARE\WOW6432Node\Ericsson\Erlang'
}
else
{
    New-Variable -Name erlangRegKeyPath -Option Constant `
        -Value 'HKLM:\SOFTWARE\Ericsson\Erlang'
}

New-Variable -Name erlangRegKey -Option Constant `
    -Value (Get-ChildItem $erlangRegKeyPath)

if ($erlangRegKey -eq $null) {
    Write-Error "Could not find Erlang installation registry key at $erlangRegKeyPath"
}

New-Variable -Name erlangErtsVersion -Option Constant `
    -Value (Select-Object -InputObject $erlangRegKey -Last 1).PSChildName
Write-Verbose "erlangErtsVersion: $erlangErtsVersion"

New-Variable -Name erlangErtsRegKeyPath -Option Constant `
    -Value "HKLM:\SOFTWARE\WOW6432Node\Ericsson\Erlang\$erlangErtsVersion"

New-Variable -Name erlangErtsRegKey -Option Constant `
    -Value (Get-ItemProperty -LiteralPath HKLM:\SOFTWARE\WOW6432Node\Ericsson\Erlang\$erlangErtsVersion)

if ($erlangErtsRegKey -eq $null) {
    Write-Error "Could not find Erlang erts registry key at $erlangErtsRegKeyPath"
}

New-Variable -Name erlangProgramFilesPath -Option Constant `
    -Value ($erlangErtsRegKey.'(default)')

if (Test-Path -LiteralPath $erlangProgramFilesPath) {
    Write-Verbose "Erlang installation directory: '$erlangProgramFilesPath'"
}
else {
    Write-Error 'Could not find Erlang installation directory!'
}

New-Variable -Name allowedExes  -Option Constant -Value @('erl.exe', 'epmd.exe', 'werl.exe')

New-Variable -Name exes  -Option Constant -Value `
    $(Get-ChildItem -Filter '*.exe' -Recurse -LiteralPath $erlangProgramFilesPath | Where-Object { $_.Name -in $allowedExes })

foreach ($exe in $exes) {
    $fwRuleName = "rabbitmq-allow-$($exe.Name)-$(Get-Random)"
    Write-Verbose "Updating or creating firewall rule for '$exe' - fwRuleName: $fwRuleName"
    if (!(Get-NetFirewallRule -ErrorAction 'SilentlyContinue' -Name $fwRuleName)) {
        New-NetFirewallRule -Enabled True -Name $fwRuleName -DisplayName $fwRuleName -Direction In -Program $exe -Profile Any -Action Allow
    }
    else {
        Set-NetFirewallRule -Enabled True -Name $fwRuleName -DisplayName $fwRuleName -Direction In -Program $exe -Profile Any -Action Allow
    }
}

Write-Host "[INFO] Setting ERLANG_HOME to '$erlangProgramFilesPath'..."
$env:ERLANG_HOME = $erlangProgramFilesPath
[Environment]::SetEnvironmentVariable('ERLANG_HOME', $erlangProgramFilesPath, 'Machine')
Add-Content -Verbose -LiteralPath $env:GITHUB_ENV -Value "ERLANG_HOME=$erlangProgramFilesPath"

Write-Host "[INFO] Setting RABBITMQ_SERVER_ADDITIONAL_ERL_ARGS..."
$env:RABBITMQ_SERVER_ADDITIONAL_ERL_ARGS = '-rabbitmq_stream advertised_host localhost'
[Environment]::SetEnvironmentVariable('RABBITMQ_SERVER_ADDITIONAL_ERL_ARGS', '-rabbitmq_stream advertised_host localhost', 'Machine')

Write-Host '[INFO] Downloading RabbitMQ...'

if (-Not (Test-Path -LiteralPath $rabbitmq_installer_path))
{
    Invoke-WebRequest -UseBasicParsing -Uri $rabbitmq_installer_download_url -OutFile $rabbitmq_installer_path
}
else
{
    Write-Host "[INFO] Found '$rabbitmq_installer_path' in cache!"
}

Write-Host "[INFO] Installer dir '$base_installers_dir' contents:"
Get-ChildItem -Verbose -LiteralPath $base_installers_dir

$rabbitmq_conf_in_file = Join-Path -Path $ci_windows_dir -ChildPath 'rabbitmq.conf.in'
$rabbitmq_appdata_dir = Join-Path -Path $env:AppData -ChildPath 'RabbitMQ'
New-Item -Path $rabbitmq_appdata_dir -ItemType Directory
$rabbitmq_conf_file = Join-Path -Path $rabbitmq_appdata_dir -ChildPath 'rabbitmq.conf'

Write-Host "[INFO] Creating RabbitMQ configuration file in '$rabbitmq_appdata_dir'"
Get-Content $rabbitmq_conf_in_file | %{ $_ -replace '@@CERTS_DIR@@', $certs_dir } | %{ $_ -replace '\\', '/' } | Set-Content -LiteralPath $rabbitmq_conf_file
Get-Content $rabbitmq_conf_file

Write-Host '[INFO] Creating Erlang cookie files...'

function Set-ErlangCookie
{
    Param($Path, $Value = 'RABBITMQ-COOKIE')
    Remove-Item -Force $Path -ErrorAction SilentlyContinue
    [System.IO.File]::WriteAllText($Path, $Value, [System.Text.Encoding]::ASCII)
}

$erlang_cookie_user = Join-Path -Path $HOME -ChildPath '.erlang.cookie'
$erlang_cookie_system = Join-Path -Path $env:SystemRoot -ChildPath 'System32\config\systemprofile\.erlang.cookie'

Set-ErlangCookie -Path $erlang_cookie_user
Set-ErlangCookie -Path $erlang_cookie_system

Write-Host '[INFO] Installing and starting RabbitMQ...'

& $rabbitmq_installer_path '/S' | Out-Null
(Get-Service -Name RabbitMQ).Status

$rabbitmq_base_path = (Get-ItemProperty -Name Install_Dir -LiteralPath 'HKLM:\SOFTWARE\WOW6432Node\VMware, Inc.\RabbitMQ Server').Install_Dir
$regPath = 'HKLM:\SOFTWARE\Microsoft\Windows\CurrentVersion\Uninstall\RabbitMQ'
if (Test-Path -LiteralPath 'HKLM:\SOFTWARE\WOW6432Node\')
{
    $regPath = 'HKLM:\SOFTWARE\WOW6432Node\Microsoft\Windows\CurrentVersion\Uninstall\RabbitMQ'
}
$rabbitmq_version = (Get-ItemProperty $regPath 'DisplayVersion').DisplayVersion
Write-Host "[INFO] RabbitMQ version path: $rabbitmq_base_path and version: $rabbitmq_version"

$rabbitmq_home = Join-Path -Path $rabbitmq_base_path -ChildPath "rabbitmq_server-$rabbitmq_version"
Write-Host "[INFO] Setting RABBITMQ_HOME to '$rabbitmq_home'..."
[Environment]::SetEnvironmentVariable('RABBITMQ_HOME', $rabbitmq_home, 'Machine')
$env:RABBITMQ_HOME = $rabbitmq_home

$rabbitmqctl_path = Join-Path -Path $rabbitmq_base_path -ChildPath "rabbitmq_server-$rabbitmq_version" | Join-Path -ChildPath 'sbin' | Join-Path -ChildPath 'rabbitmqctl.bat'
$rabbitmq_plugins_path = Join-Path -Path $rabbitmq_base_path -ChildPath "rabbitmq_server-$rabbitmq_version" | Join-Path -ChildPath 'sbin' | Join-Path -ChildPath 'rabbitmq-plugins.bat'

Write-Host "[INFO] Setting RABBITMQ_RABBITMQCTL_PATH to '$rabbitmqctl_path'..."
$env:RABBITMQ_RABBITMQCTL_PATH = $rabbitmqctl_path
[Environment]::SetEnvironmentVariable('RABBITMQ_RABBITMQCTL_PATH', $rabbitmqctl_path, 'Machine')

$epmd_running = $false
[int]$count = 1

$epmd_exe = Join-Path -Path $erlangProgramFilesPath -ChildPath "erts-$erlangErtsVersion" | Join-Path -ChildPath 'bin' | Join-Path -ChildPath 'epmd.exe'

Write-Host "[INFO] Waiting for epmd ($epmd_exe) to report that RabbitMQ has started..."

Do {
    $epmd_running = & $epmd_exe -names | Select-String -CaseSensitive -SimpleMatch -Quiet -Pattern 'name rabbit at port'
    if ($epmd_running -eq $true) {
        Write-Host '[INFO] epmd reports that RabbitMQ is running!'
        break
    }

    if ($count -gt 60) {
        throw '[ERROR] too many tries waiting for epmd to report RabbitMQ running!'
    }

    Write-Host "[INFO] epmd NOT reporting yet that RabbitMQ is running, count: '$count'..."
    $count = $count + 1
    Start-Sleep -Seconds 5

} While ($true)

[int]$count = 1

Do {
    $proc_id = (Get-Process -Name erl).Id
    if (-Not ($proc_id -is [array])) {
        & $rabbitmqctl_path await_startup
        if ($LASTEXITCODE -ne 0) {
            throw "[ERROR] 'rabbitmqctl await_startup' returned error: $LASTEXITCODE"
        }
        break
    }

    if ($count -gt 120) {
        throw '[ERROR] too many tries waiting for just one erl process to be running!'
    }

    Write-Host '[INFO] multiple erl instances running still...'
    $count = $count + 1
    Start-Sleep -Seconds 5

} While ($true)

$ErrorActionPreference = 'Continue'
Write-Host '[INFO] Getting RabbitMQ status...'
& $rabbitmqctl_path status

$ErrorActionPreference = 'Continue'
Write-Host '[INFO] Enabling plugins...'
& $rabbitmq_plugins_path enable rabbitmq_management rabbitmq_stream rabbitmq_stream_management rabbitmq_amqp1_0

echo Q | openssl s_client -connect localhost:5671 -CAfile "$certs_dir/ca_certificate.pem" -cert "$certs_dir/client_localhost_certificate.pem" -key "$certs_dir/client_localhost_key.pem" -pass pass:grapefruit
if ($LASTEXITCODE -ne 0)
{
    throw "[ERROR] 'openssl s_client' returned error: $LASTEXITCODE"
}


$rabbitmqctl_path = Resolve-Path -LiteralPath `
    (Join-Path -Path $rabbitmq_base_path -ChildPath "rabbitmq_server-$rabbitmq_version" | Join-Path -ChildPath 'sbin' | Join-Path -ChildPath 'rabbitmqctl.bat')

Write-Host "[INFO] Setting RABBITMQ_RABBITMQCTL_PATH to '$rabbitmqctl_path'..."
$env:RABBITMQ_RABBITMQCTL_PATH = $rabbitmqctl_path
[Environment]::SetEnvironmentVariable('RABBITMQ_RABBITMQCTL_PATH', $rabbitmqctl_path, 'Machine')
Add-Content -Verbose -LiteralPath $env:GITHUB_OUTPUT -Value "path=$rabbitmqctl_path"
Add-Content -Verbose -LiteralPath $env:GITHUB_ENV -Value "RABBITMQ_RABBITMQCTL_PATH=$rabbitmqctl_path"
