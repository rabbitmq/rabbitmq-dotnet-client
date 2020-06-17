$ProgressPreference = 'SilentlyContinue'
$ErrorActionPreference = 'Stop'
Set-StrictMode -Version 2.0

[Net.ServicePointManager]::SecurityProtocol = [Net.ServicePointManager]::SecurityProtocol -bor 'Tls12'

Write-Host '[INFO] Removing all existing versions of Erlang...'
Get-ChildItem -Path 'C:\Program Files\erl*\Uninstall.exe' | %{ Start-Process -Wait -NoNewWindow -FilePath $_ -ArgumentList '/S' }

$erlang_download_url = 'http://erlang.org/download/otp_win64_23.0.2.exe'
$erlang_installer_path = Join-Path -Path $HOME -ChildPath 'otp_win64_23.0.2.exe'
$erlang_install_dir = Join-Path -Path $HOME -ChildPath 'erlang'

Write-Host '[INFO] Downloading Erlang...'

if (-Not (Test-Path $erlang_installer_path))
{
    Invoke-WebRequest -UseBasicParsing -Uri $erlang_download_url -OutFile $erlang_installer_path
}
else
{
    Write-Host "[INFO] Found" $erlang_installer_path "in cache."
}

Write-Host "[INFO] Installing Erlang to $erlang_install_dir..."
& $erlang_installer_path '/S' "/D=$erlang_install_dir" | Out-Null

$rabbitmq_installer_download_url = 'https://github.com/rabbitmq/rabbitmq-server/releases/download/v3.8.5/rabbitmq-server-3.8.5.exe'
$rabbitmq_installer_path = Join-Path -Path $HOME -ChildPath 'rabbitmq-server-3.8.5.exe'

$erlang_reg_path = 'HKLM:\SOFTWARE\Ericsson\Erlang'
if (Test-Path 'HKLM:\SOFTWARE\WOW6432Node\')
{
    $erlang_reg_path = 'HKLM:\SOFTWARE\WOW6432Node\Ericsson\Erlang'
}
$erlang_erts_version = Get-ChildItem -Path $erlang_reg_path -Name
$erlang_home = (Get-ItemProperty -LiteralPath $erlang_reg_path\$erlang_erts_version).'(default)'

$env:ERLANG_HOME = $erlang_home
[Environment]::SetEnvironmentVariable('ERLANG_HOME', $erlang_home, 'Machine')

Write-Host '[INFO] Downloading RabbitMQ'

if (-Not (Test-Path $rabbitmq_installer_path))
{
    Invoke-WebRequest -UseBasicParsing -Uri $rabbitmq_installer_download_url -OutFile $rabbitmq_installer_path
}
else
{
    Write-Host "[INFO] Found $rabbitmq_installer_path in cache."
}

Write-Host '[INFO] Creating Erlang cookie files'

function Set-ErlangCookie {
    Param($Path, $Value = 'RABBITMQ-COOKIE')
    Remove-Item -Force $Path -ErrorAction SilentlyContinue
    [System.IO.File]::WriteAllText($Path, $Value, [System.Text.Encoding]::ASCII)
}

$erlang_cookie_user = Join-Path -Path $HOME -ChildPath '.erlang.cookie'
$erlang_cookie_system = Join-Path -Path $env:SystemRoot -ChildPath 'System32\config\systemprofile\.erlang.cookie'

Set-ErlangCookie -Path $erlang_cookie_user
Set-ErlangCookie -Path $erlang_cookie_system

Write-Host '[INFO] Installing and starting RabbitMQ with default config'

& $rabbitmq_installer_path '/S' | Out-Null
(Get-Service -Name RabbitMQ).Status

Write-Host '[INFO] Setting RABBITMQ_RABBITMQCTL_PATH'

$regPath = 'HKLM:\SOFTWARE\Microsoft\Windows\CurrentVersion\Uninstall\RabbitMQ'
if (Test-Path 'HKLM:\SOFTWARE\WOW6432Node\')
{
    $regPath = 'HKLM:\SOFTWARE\WOW6432Node\Microsoft\Windows\CurrentVersion\Uninstall\RabbitMQ'
}
$path = Split-Path -Parent (Get-ItemProperty $regPath 'UninstallString').UninstallString
$rabbitmq_version = (Get-ItemProperty $regPath "DisplayVersion").DisplayVersion

$rabbitmq_home = "$path\rabbitmq_server-$rabbitmq_version"
[Environment]::SetEnvironmentVariable('RABBITMQ_HOME', $rabbitmq_home, 'Machine')
$env:RABBITMQ_HOME = $rabbitmq_home

$rabbitmqctl_path = "$path\rabbitmq_server-$rabbitmq_version\sbin\rabbitmqctl.bat"
[Environment]::SetEnvironmentVariable('RABBITMQ_RABBITMQCTL_PATH', $rabbitmqctl_path, 'Machine')
$env:RABBITMQ_RABBITMQCTL_PATH = $rabbitmqctl_path

$epmd_running = $false
[int]$count = 1

$epmd = [System.IO.Path]::Combine($erlang_home, "erts-$erlang_erts_version", "bin", "epmd.exe")

Write-Host "[INFO] Waiting for epmd ($epmd) to report that RabbitMQ has started"

Do {
    $epmd_running = & $epmd -names | Select-String -CaseSensitive -SimpleMatch -Quiet -Pattern 'name rabbit at port 25672'
    if ($epmd_running -eq $true) {
        Write-Host '[INFO] epmd reports that RabbitMQ is at port 25672'
        break
    }

    if ($count -gt 60) {
        throw '[ERROR] too many tries waiting for epmd to report RabbitMQ on port 25672'
    }

    Write-Host "[INFO] epmd NOT reporting yet that RabbitMQ is at port 25672, count: $count"
    $count = $count + 1
    Start-Sleep -Seconds 5

} While ($true)

[int]$count = 1

Do {
    $proc_id = (Get-Process -Name erl).Id
    if (-Not ($proc_id -is [array])) {
        & $rabbitmqctl_path wait -t 300000 -P $proc_id
        if ($LASTEXITCODE -ne 0) {
            throw "[ERROR] rabbitmqctl wait returned error: $LASTEXITCODE"
        }
        break
    }

    if ($count -gt 120) {
        throw '[ERROR] too many tries waiting for just one erl process to be running'
    }

    Write-Host '[INFO] multiple erl instances running still'
    $count = $count + 1
    Start-Sleep -Seconds 5

} While ($true)

Write-Host '[INFO] Getting RabbitMQ status'
& $rabbitmqctl_path status
