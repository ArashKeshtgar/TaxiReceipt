# Publishes TaxiReceipt and installs it as a Windows Service.
# Run from an elevated (Administrator) PowerShell:
#   .\deploy\install-service.ps1                      # publish to C:\Services\TaxiReceipt and install
#   .\deploy\install-service.ps1 -Account LocalSystem
#
# The service starts automatically (delayed, so SQL Server is up first) and
# Windows restarts it after a crash: 1 min, 1 min, then 5 min.
param(
    [string]$InstallDir = 'C:\Services\TaxiReceipt',
    [string]$ServiceName = 'TaxiReceipt',
    # Default: the service's own virtual account, NT SERVICE\<ServiceName> —
    # no password, and no rights beyond what is granted to it: read access to
    # the clock database (db\grant-service-account.sql) and its own folder.
    [string]$Account = ''
)
$ErrorActionPreference = 'Stop'

$admin = ([Security.Principal.WindowsPrincipal][Security.Principal.WindowsIdentity]::GetCurrent()).IsInRole(
    [Security.Principal.WindowsBuiltInRole]::Administrator)
if (-not $admin) { throw 'Run this from an elevated (Run as administrator) PowerShell.' }
if (-not $Account) { $Account = "NT SERVICE\$ServiceName" }

$project = Join-Path $PSScriptRoot '..\src\TaxiReceipt.Service\TaxiReceipt.Service.csproj'

$existing = Get-Service -Name $ServiceName -ErrorAction SilentlyContinue
if ($existing -and $existing.Status -ne 'Stopped') {
    Write-Host "Stopping the existing $ServiceName service..."
    Stop-Service $ServiceName -Force
}

# Settings already on the machine survive a reinstall.
$settings = Join-Path $InstallDir 'appsettings.json'
$keep = if (Test-Path $settings) { Get-Content $settings -Raw -Encoding utf8 } else { $null }

Write-Host "Publishing to $InstallDir..."
dotnet publish $project -c Release -r win-x64 --self-contained false -o $InstallDir
if ($LASTEXITCODE -ne 0) { throw 'dotnet publish failed.' }
if ($keep) { Set-Content $settings $keep -Encoding utf8; Write-Host '  kept the existing appsettings.json' }

$exe = Join-Path $InstallDir 'TaxiReceipt.Service.exe'
if (-not $existing) {
    Write-Host "Creating service $ServiceName (runs as $Account)..."
    sc.exe create $ServiceName binPath= "`"$exe`"" start= delayed-auto obj= $Account DisplayName= 'TaxiReceipt - late-night taxi receipts' | Out-Host
    if ($LASTEXITCODE -ne 0) { throw 'sc.exe create failed.' }
    sc.exe description $ServiceName 'Watches the attendance-clock database and prints a taxi receipt for staff punching in the night window.' | Out-Null
    sc.exe failure $ServiceName reset= 86400 actions= restart/60000/restart/60000/restart/300000 | Out-Null
}

# The Event Log source the service logs under. Creating one needs admin, so
# it's done here; the service's own account couldn't.
if (-not [System.Diagnostics.EventLog]::SourceExists($ServiceName)) {
    [System.Diagnostics.EventLog]::CreateEventSource($ServiceName, 'Application')
    Write-Host "Registered Event Log source '$ServiceName'."
}

# The service writes state.json and receipts\ in its own folder. A virtual
# account only exists once the service does, so this comes after sc create.
icacls $InstallDir /grant "${Account}:(OI)(CI)M" /T /Q | Out-Null
if ($LASTEXITCODE -ne 0) { throw "icacls couldn't grant $Account access to $InstallDir." }

Start-Service $ServiceName
Get-Service $ServiceName | Format-Table Name, Status, StartType -AutoSize
Write-Host "Logs: Event Viewer > Windows Logs > Application, source '$ServiceName'."
