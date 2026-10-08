param(
    [Parameter(Mandatory = $true)]
    [string]$BaseUrl,

    [Parameter(Mandatory = $true)]
    [string]$AccessToken,

    [Parameter(Mandatory = $false)]
    [Guid]$TicketId = [Guid]::Empty
)

$ErrorActionPreference = "Stop"

$headers = @{
    Authorization = "Bearer $AccessToken"
}

function Write-Step(
    [string]$Text
) {
    Write-Host ""
    Write-Host "============================================================" -ForegroundColor Cyan
    Write-Host " $Text" -ForegroundColor Cyan
    Write-Host "============================================================" -ForegroundColor Cyan
}

Write-Step "HD-C12 SECURITY / ROUTING SMOKE TEST"

# ================================================================
# HEALTH
# ================================================================

Write-Step "1. HEALTH"

$health =
    Invoke-RestMethod `
        -Method Get `
        -Uri "$BaseUrl/api/helpdesk/routing/health" `
        -Headers $headers

$health |
    ConvertTo-Json -Depth 10

# ================================================================
# QUEUE DRY RUN
# ================================================================

Write-Step "2. QUEUE DRY RUN"

$dryRunBody =
    @{
        maxTickets = 25
        dryRun = $true
    } |
    ConvertTo-Json

$dryRun =
    Invoke-RestMethod `
        -Method Post `
        -Uri "$BaseUrl/api/helpdesk/routing/retry-open" `
        -Headers $headers `
        -ContentType "application/json" `
        -Body $dryRunBody

$dryRun |
    ConvertTo-Json -Depth 15

# ================================================================
# SINGLE TICKET
# ================================================================

if (
    $TicketId -ne
    [Guid]::Empty
) {
    Write-Step "3. DIAGNOSTIC"

    $diagnostic =
        Invoke-RestMethod `
            -Method Get `
            -Uri "$BaseUrl/api/helpdesk/routing/tickets/$TicketId/diagnostic" `
            -Headers $headers

    $diagnostic |
        ConvertTo-Json -Depth 15

    Write-Step "4. RETRY SINGLE"

    $retry =
        Invoke-RestMethod `
            -Method Post `
            -Uri "$BaseUrl/api/helpdesk/routing/tickets/$TicketId/retry" `
            -Headers $headers

    $retry |
        ConvertTo-Json -Depth 15
}

Write-Step "HD-C12 SMOKE TEST COMPLETADO"