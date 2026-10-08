# Stops and removes the TaxiReceipt Windows Service. Run elevated.
# The install folder (settings, state.json, receipts) is left in place.
param([string]$ServiceName = 'TaxiReceipt')
$ErrorActionPreference = 'Stop'

$svc = Get-Service -Name $ServiceName -ErrorAction SilentlyContinue
if (-not $svc) { Write-Host "No service named $ServiceName."; return }
if ($svc.Status -ne 'Stopped') { Stop-Service $ServiceName -Force }
sc.exe delete $ServiceName | Out-Host
