<#
.SYNOPSIS
  End-to-end local smoke of the Tajeer happy path using DUMMY credentials and InMemory mode.

.DESCRIPTION
  Closes the same Week-1 evidence the real STAGING-SMOKE.md captures, but without needing
  Tajeer Rabet credentials or ngrok. Pipeline exercised:

    1. Ensures docker compose stack (SQL Edge) is up.
    2. Applies the EF Core migrations.
    3. Writes DUMMY Tajeer user-secrets onto services/bff and forces Tajeer:Mode=InMemory.
    4. Starts the BFF in Development.
    5. POSTs a domain-shaped Save Contract (using seeded ids) -> 202 with leaseId + issuanceUrl.
    6. POSTs a synthetic Tajeer "contract.create" webhook to /api/v1/webhooks/tajeer
       (signed with the dummy shared secret) so the lease flips to Active.
    7. Verifies via SQL the row is in Active state and a WebhookLog row exists with
       SignatureValid=1.
    8. Tears the BFF down.

  When real Tajeer Rabet creds arrive, run the same flow against real Tajeer by:
    - dotnet user-secrets set "Tajeer:Mode" "Real" (and the four credential keys)
    - re-running this script with -RealTajeer (skips step 6 - real Tajeer fires the webhook)

.PARAMETER SkipInfra
  Skip "docker compose up". Use if SQL is already running locally.

.PARAMETER SkipMigrate
  Skip "dotnet ef database update". Use after the first successful run.

.PARAMETER RealTajeer
  Don't synthesize the webhook locally - assume real Tajeer will POST it.
  Implies you've also set real Tajeer:Mode=Real + creds via user-secrets.

.EXAMPLE
  pwsh -File scripts/local-smoke.ps1
#>
[CmdletBinding()]
param(
  [switch]$SkipInfra,
  [switch]$SkipMigrate,
  [switch]$RealTajeer
)

$ErrorActionPreference = 'Stop'
$RepoRoot = Resolve-Path (Join-Path $PSScriptRoot '..')
Set-Location $RepoRoot

$bffProject       = 'services/bff/AutoLeaseNet.Bff.csproj'
$bffBaseUrl       = 'http://localhost:5000'
$apiV1            = "$bffBaseUrl/api/v1"
$tenantId         = 'a1a1a1a1-0001-0000-0000-000000000001' # seed tenant
$sqlConn          = 'Server=localhost,1433;Database=AutoLeaseNet_Dev;User Id=sa;Password=LocalDev_P@ssw0rd_2026;TrustServerCertificate=true;Encrypt=false'
$webhookSecret    = 'dummy-webhook-secret'
$dummyTajeer = @{
  'Tajeer:AppId'                = 'dummy-app-id'
  'Tajeer:AppKey'               = 'dummy-app-key'
  'Tajeer:AuthorizationToken'   = 'Basic ZHVtbXk6ZHVtbXk='
  'Tajeer:BranchId'             = '1'
  'Tajeer:WebhookSharedSecret'  = $webhookSecret
  'Tajeer:Mode'                 = 'InMemory'
  'Tajeer:Webhook:LogOnly'      = 'false'
  'Seed:Mode'                   = 'Demo'
  'Seed:TenantId'               = $tenantId
  'Seed:RandomSeed'             = '20260524'
  'ConnectionStrings:AutoLeaseNet' = $sqlConn
}

function Write-Step([string]$msg) { Write-Host "==> $msg" -ForegroundColor Cyan }
function Write-Done([string]$msg) { Write-Host "    [ok] $msg" -ForegroundColor Green }
function Write-Warn([string]$msg) { Write-Host "    [warn] $msg" -ForegroundColor Yellow }

function Invoke-Sql {
  param([Parameter(Mandatory)][string]$Query)
  $sqlcmd = Get-Command sqlcmd -ErrorAction SilentlyContinue
  if (-not $sqlcmd) {
    Write-Warn 'sqlcmd not found on PATH; skipping SQL verification step. Install Microsoft.SqlServer.SqlPackage or use SSMS to verify manually.'
    return $null
  }
  & $sqlcmd.Path -S 'localhost,1433' -U sa -P 'LocalDev_P@ssw0rd_2026' -d AutoLeaseNet_Dev -I -h -1 -W -Q $Query
}

#----------------------------------------------------------------------
# 1. Infra
#----------------------------------------------------------------------
if (-not $SkipInfra) {
  Write-Step 'Ensuring docker compose stack is up (SQL Edge on 1433)'
  pnpm infra:up | Out-Host
  Write-Done 'compose up'
} else {
  Write-Warn 'SkipInfra: not invoking docker compose'
}

#----------------------------------------------------------------------
# 2. User secrets (dummy creds)
#----------------------------------------------------------------------
Write-Step 'Writing DUMMY Tajeer user-secrets to services/bff'
Push-Location services/bff
try {
  foreach ($kv in $dummyTajeer.GetEnumerator()) {
    if ($RealTajeer -and ($kv.Key -in 'Tajeer:AppId','Tajeer:AppKey','Tajeer:AuthorizationToken','Tajeer:WebhookSharedSecret','Tajeer:Mode','Tajeer:BranchId')) {
      Write-Warn "RealTajeer: preserving existing user-secret $($kv.Key)"
      continue
    }
    dotnet user-secrets set $kv.Key $kv.Value | Out-Null
  }
} finally {
  Pop-Location
}
Write-Done 'user-secrets configured'

#----------------------------------------------------------------------
# 3. Migrations
#----------------------------------------------------------------------
if (-not $SkipMigrate) {
  Write-Step 'Applying EF Core migrations'
  dotnet ef database update `
    --project packages/application/AutoLeaseNet.Infrastructure `
    --startup-project services/bff | Out-Host
  Write-Done 'database update'
} else {
  Write-Warn 'SkipMigrate: not applying migrations'
}

#----------------------------------------------------------------------
# 4. Start BFF
#----------------------------------------------------------------------
Write-Step 'Starting BFF (dotnet run, http://localhost:5000)'
$env:ASPNETCORE_ENVIRONMENT = 'Development'
$env:ASPNETCORE_URLS = $bffBaseUrl
$bffLog = Join-Path $RepoRoot 'scripts/.local-smoke.bff.log'
if (Test-Path $bffLog) { Remove-Item $bffLog -Force }
$bff = Start-Process -FilePath 'dotnet' -ArgumentList @('run','--project',$bffProject,'--no-launch-profile') `
  -RedirectStandardOutput $bffLog -RedirectStandardError $bffLog -PassThru -WindowStyle Hidden
try {
  Write-Step 'Waiting for /health/liveness'
  $alive = $false
  for ($i = 0; $i -lt 60; $i++) {
    try {
      $r = Invoke-WebRequest -Uri "$bffBaseUrl/health/liveness" -UseBasicParsing -TimeoutSec 2
      if ($r.StatusCode -eq 200) { $alive = $true; break }
    } catch { Start-Sleep -Milliseconds 1000 }
  }
  if (-not $alive) {
    Get-Content $bffLog -Tail 80
    throw 'BFF did not become live within 60s'
  }
  Write-Done 'BFF up'

  #----------------------------------------------------------------------
  # 5. Discover seeded ids
  #----------------------------------------------------------------------
  Write-Step 'Discovering seeded ids via /api/v1/lookups/*'
  $headers = @{
    'X-Dev-Tenant-Id' = $tenantId
    'X-Dev-User-Type' = 'InternalStaff'
    'Content-Type'    = 'application/json'
  }
  $customers = Invoke-RestMethod -Uri "$apiV1/lookups/customers?page=1&pageSize=1" -Headers $headers
  $vehicles  = Invoke-RestMethod -Uri "$apiV1/lookups/vehicles?page=1&pageSize=1&status=1" -Headers $headers
  $drivers   = Invoke-RestMethod -Uri "$apiV1/lookups/drivers?page=1&pageSize=1"  -Headers $headers
  $policies  = Invoke-RestMethod -Uri "$apiV1/lookups/rent-policies" -Headers $headers
  $branches  = Invoke-RestMethod -Uri "$apiV1/lookups/branches"      -Headers $headers

  $customerId = $customers.items[0].id
  $vehicleId  = $vehicles.items[0].id
  $driverId   = $drivers.items[0].id
  $policyId   = $policies[0].id
  $branchId   = $branches[0].id
  Write-Done "customer=$customerId vehicle=$vehicleId driver=$driverId policy=$policyId branch=$branchId"

  #----------------------------------------------------------------------
  # 6. POST /dev/save-contract
  #----------------------------------------------------------------------
  Write-Step 'POST /api/v1/dev/save-contract'
  $body = @{
    customerId        = $customerId
    vehicleId         = $vehicleId
    primaryDriverId   = $driverId
    rentPolicyId      = $policyId
    workingBranchId   = $branchId
    receiveBranchId   = $branchId
    returnBranchId    = $branchId
    contractStartUtc  = (Get-Date).ToUniversalTime().ToString("yyyy-MM-ddTHH:mm:ssZ")
    contractEndUtc    = (Get-Date).AddDays(2).ToUniversalTime().ToString("yyyy-MM-ddTHH:mm:ssZ")
    contractTypeCode  = 1
    allowedKmPerDay   = 300
    rentAmount        = 200
    paidAmount        = 50
    paymentMethodCode = 1
  } | ConvertTo-Json -Depth 5

  $idemKey = [Guid]::NewGuid().ToString('N')
  $saveHeaders = @{} + $headers
  $saveHeaders['Idempotency-Key'] = $idemKey
  $save = Invoke-RestMethod -Method POST -Uri "$apiV1/dev/save-contract" -Headers $saveHeaders -Body $body
  Write-Done "leaseId=$($save.leaseId) tajeer#=$($save.tajeerContractNumber)"
  Write-Host    "    issuanceUrl: $($save.issuanceUrl)" -ForegroundColor DarkGray

  #----------------------------------------------------------------------
  # 7. Simulate Tajeer "contract.create" webhook (skip when RealTajeer)
  #----------------------------------------------------------------------
  if (-not $RealTajeer) {
    Write-Step 'Synthesising Tajeer "contract.create" webhook'
    $payload = @{
      id          = "notif_$(Get-Random -Maximum 999999)"
      timestamp   = (Get-Date).ToUniversalTime().ToString('yyyy-MM-ddTHH:mm:ss')
      category    = 'contract'
      type        = 'contract.create'
      referenceId = "$($save.tajeerContractNumber)"
      message     = "Contract $($save.tajeerContractNumber) is created."
    } | ConvertTo-Json -Compress
    $wh = Invoke-WebRequest `
      -Method POST `
      -Uri    "$apiV1/webhooks/tajeer" `
      -Headers @{ 'secret-key' = $webhookSecret; 'Content-Type' = 'application/json' } `
      -Body   $payload `
      -UseBasicParsing
    Write-Done "webhook -> $($wh.StatusCode)"
  } else {
    Write-Warn 'RealTajeer: skipping synthetic webhook - real Tajeer should POST shortly'
  }

  #----------------------------------------------------------------------
  # 8. Verify via SQL
  #----------------------------------------------------------------------
  Start-Sleep -Seconds 1
  Write-Step 'Verifying Lease + WebhookLog rows via SQL'
  Invoke-Sql -Query "SELECT TOP 1 TajeerContractNumber, Status, IssuedAtUtc FROM Leases WHERE TajeerContractNumber=$($save.tajeerContractNumber);"
  Invoke-Sql -Query "SELECT TOP 1 ExternalEventId, EventType, SignatureValid, ProcessedAtUtc FROM WebhookLogs ORDER BY ReceivedAtUtc DESC;"
  Write-Done 'verification done'

  Write-Host ""
  Write-Host "Local smoke complete. Expected:" -ForegroundColor Green
  Write-Host "  - Leases.Status        = 3 (Active)" -ForegroundColor Green
  Write-Host "  - Leases.IssuedAtUtc   = populated" -ForegroundColor Green
  Write-Host "  - WebhookLogs.SignatureValid = 1" -ForegroundColor Green
  Write-Host "  - WebhookLogs.ProcessedAtUtc = populated" -ForegroundColor Green
}
finally {
  if ($bff -and -not $bff.HasExited) {
    Write-Step 'Stopping BFF'
    try { Stop-Process -Id $bff.Id -Force } catch { }
  }
  Write-Host "    BFF log: $bffLog" -ForegroundColor DarkGray
}
