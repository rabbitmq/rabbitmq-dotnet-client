$ProgressPreference = 'Continue'
$ErrorActionPreference = 'Stop'
Set-StrictMode -Version 2.0

[Net.ServicePointManager]::SecurityProtocol = [Net.ServicePointManager]::SecurityProtocol -bor 'Tls12'

Write-Host '[INFO] Removing all existing versions of Erlang...'
Get-ChildItem -Path 'C:\Program Files\erl*\Uninstall.exe' | %{ Start-Process -Wait -NoNewWindow -FilePath $_ -ArgumentList '/S' }

$erlang_download_url = 'https://github.com/erlang/otp/releases/download/OTP-24.2.1/otp_win64_24.2.1.exe'
$erlang_installer_path = Join-Path -Path $HOME -ChildPath 'otp_win64_24.2.1.exe'
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

$rabbitmq_installer_download_url = 'https://github.com/rabbitmq/rabbitmq-server/releases/download/v3.9.13/rabbitmq-server-3.9.13.exe'
$rabbitmq_installer_path = Join-Path -Path $HOME -ChildPath 'rabbitmq-server-3.9.13.exe'

$erlang_reg_path = 'HKLM:\SOFTWARE\Ericsson\Erlang'
if (Test-Path 'HKLM:\SOFTWARE\WOW6432Node\')
{
    $erlang_reg_path = 'HKLM:\SOFTWARE\WOW6432Node\Ericsson\Erlang'
}
$erlang_erts_version = Get-ChildItem -Path $erlang_reg_path -Name
$erlang_home = (Get-ItemProperty -LiteralPath $erlang_reg_path\$erlang_erts_version).'(default)'

Write-Host "[INFO] Setting ERLANG_HOME to $erlang_home"
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

$regPath = 'HKLM:\SOFTWARE\Microsoft\Windows\CurrentVersion\Uninstall\RabbitMQ'
if (Test-Path 'HKLM:\SOFTWARE\WOW6432Node\')
{
    $regPath = 'HKLM:\SOFTWARE\WOW6432Node\Microsoft\Windows\CurrentVersion\Uninstall\RabbitMQ'
}
$rabbitmq_base_path = Split-Path -Parent (Get-ItemProperty $regPath 'UninstallString').UninstallString
$rabbitmq_version = (Get-ItemProperty $regPath "DisplayVersion").DisplayVersion

$rabbitmq_home = Join-Path -Path $rabbitmq_base_path -ChildPath "rabbitmq_server-$rabbitmq_version"
Write-Host "[INFO] Setting RABBITMQ_HOME to $rabbitmq_home"
[Environment]::SetEnvironmentVariable('RABBITMQ_HOME', $rabbitmq_home, 'Machine')
$env:RABBITMQ_HOME = $rabbitmq_home

$rabbitmqctl_path = Join-Path -Path $rabbitmq_base_path -ChildPath "rabbitmq_server-$rabbitmq_version" | Join-Path -ChildPath 'sbin' | Join-Path -ChildPath 'rabbitmqctl.bat'

[Environment]::SetEnvironmentVariable('RABBITMQ_RABBITMQCTL_PATH', $rabbitmqctl_path, 'Machine')
Write-Host "[INFO] Setting RABBITMQ_RABBITMQCTL_PATH to $rabbitmqctl_path"
$env:RABBITMQ_RABBITMQCTL_PATH = $rabbitmqctl_path

$epmd_running = $false
[int]$count = 1

$epmd_exe = Join-Path -Path $erlang_home -ChildPath "erts-$erlang_erts_version" | Join-Path -ChildPath 'bin' | Join-Path -ChildPath 'epmd.exe'

Write-Host "[INFO] Waiting for epmd ($epmd_exe) to report that RabbitMQ has started"

Do {
    $epmd_running = & $epmd_exe -names | Select-String -CaseSensitive -SimpleMatch -Quiet -Pattern 'name rabbit at port'
    if ($epmd_running -eq $true) {
        Write-Host '[INFO] epmd reports that RabbitMQ is running'
        break
    }

    if ($count -gt 60) {
        throw '[ERROR] too many tries waiting for epmd to report RabbitMQ running'
    }

    Write-Host "[INFO] epmd NOT reporting yet that RabbitMQ is running, count: $count"
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

$ErrorActionPreference = 'Continue'
Write-Host '[INFO] Getting RabbitMQ status in 5 seconds...'
Start-Sleep -Seconds 5
& $rabbitmqctl_path status
