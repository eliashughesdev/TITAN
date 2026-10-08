$ErrorActionPreference = "Stop"

Set-Location C:\TitanMDM

Write-Host ""
Write-Host "============================================================" -ForegroundColor Cyan
Write-Host " TITANMDM - HD-D4 + HD-D5 + GLOBAL COVERAGE" -ForegroundColor Cyan
Write-Host "============================================================" -ForegroundColor Cyan
Write-Host ""

$files = @{
    Specialties = "src\frontend\titanmdm-web\src\pages\helpdesk\HelpdeskSpecialtiesPage.tsx"
    GroupPlanning = "src\backend\TitanMDM.Api\Controllers\HelpdeskGroupPlanningController.cs"
    HelpdeskService = "src\backend\TitanMDM.Infrastructure\Helpdesk\HelpdeskService.cs"
    RoutingOperations = "src\backend\TitanMDM.Infrastructure\Helpdesk\HelpdeskService.RoutingOperations.cs"
    Interface = "src\backend\TitanMDM.Application\Helpdesk\IHelpdeskService.cs"
    RoutingController = "src\backend\TitanMDM.Api\Controllers\HelpdeskRoutingOperationsController.cs"
    RoutingApi = "src\frontend\titanmdm-web\src\api\helpdeskRoutingApi.ts"
    RoutingPanel = "src\frontend\titanmdm-web\src\pages\helpdesk\HelpdeskRoutingOperationsPanel.tsx"
}

foreach ($key in $files.Keys) {
    $path = $files[$key]

    if (!(Test-Path $path)) {
        throw "No existe el archivo requerido: $path"
    }
}

$backupRoot =
    Join-Path `
        "artifacts" `
        ("hd-d4-d5-backup-" + (Get-Date -Format "yyyyMMdd-HHmmss"))

New-Item `
    -ItemType Directory `
    -Path $backupRoot `
    -Force |
    Out-Null

foreach ($key in $files.Keys) {
    $source =
        $files[$key]

    $destination =
        Join-Path `
            $backupRoot `
            (($source -replace '[\\/:*?"<>|]', '_'))

    Copy-Item `
        $source `
        $destination `
        -Force
}

Write-Host "[OK] Backup creado en $backupRoot" -ForegroundColor Green

function Get-Text {
    param(
        [Parameter(Mandatory)]
        [string]$Path
    )

    return [System.IO.File]::ReadAllText(
        (Resolve-Path $Path)
    )
}

function Save-Text {
    param(
        [Parameter(Mandatory)]
        [string]$Path,

        [Parameter(Mandatory)]
        [string]$Content
    )

    $utf8 =
        New-Object System.Text.UTF8Encoding($false)

    [System.IO.File]::WriteAllText(
        (Resolve-Path $Path),
        $Content,
        $utf8
    )
}

function Replace-RegexOnce {
    param(
        [Parameter(Mandatory)]
        [string]$Path,

        [Parameter(Mandatory)]
        [string]$Pattern,

        [Parameter(Mandatory)]
        [string]$Replacement,

        [Parameter(Mandatory)]
        [string]$Label
    )

    $text =
        Get-Text $Path

    $regex =
        [regex]::new(
            $Pattern,
            [System.Text.RegularExpressions.RegexOptions]::Singleline
        )

    $matches =
        $regex.Matches($text)

    if ($matches.Count -eq 0) {
        throw "No se encontró el bloque esperado: $Label en $Path"
    }

    if ($matches.Count -gt 1) {
        throw "El bloque '$Label' apareció más de una vez en $Path. Se aborta para no dañar código."
    }

    $updated =
        $regex.Replace(
            $text,
            $Replacement,
            1
        )

    Save-Text `
        -Path $Path `
        -Content $updated

    Write-Host "[OK] $Label" -ForegroundColor Green
}

function Insert-BeforeOnce {
    param(
        [Parameter(Mandatory)]
        [string]$Path,

        [Parameter(Mandatory)]
        [string]$Marker,

        [Parameter(Mandatory)]
        [string]$Content,

        [Parameter(Mandatory)]
        [string]$Guard,

        [Parameter(Mandatory)]
        [string]$Label
    )

    $text =
        Get-Text $Path

    if ($text.Contains($Guard)) {
        Write-Host "[SKIP] $Label ya estaba aplicado." -ForegroundColor Yellow
        return
    }

    $position =
        $text.IndexOf(
            $Marker,
            [StringComparison]::Ordinal
        )

    if ($position -lt 0) {
        throw "No se encontró el marcador para: $Label"
    }

    $updated =
        $text.Insert(
            $position,
            $Content
        )

    Save-Text `
        -Path $Path `
        -Content $updated

    Write-Host "[OK] $Label" -ForegroundColor Green
}

# ============================================================
# HD-D1/D2 UI
# ZERO COVERAGES = GLOBAL COVERAGE
# ============================================================

$path =
    $files.Specialties

$text =
    Get-Text $path

# ------------------------------------------------------------
# ADD COVERAGE
# ------------------------------------------------------------

$pattern = @'
(?s)  function addCoverage\(\) \{.*?(?=  function removeCoverage\()
'@

$replacement = @'
  function addCoverage() {
    if (!selected) {
      return
    }

    /*
     * Enterprise rule:
     *
     * __all__ = cobertura global.
     *
     * Cero registros de cobertura significa:
     *
     * - todas las localidades activas;
     * - todas las sublocalidades activas;
     * - sin necesidad de duplicar registros en DB.
     */
    if (
      coverageSiteId ===
      '__all__'
    ) {
      update({
        coverages: [],
      })

      setCoverageSiteId(
        '',
      )

      setCoverageLocationId(
        '',
      )

      setCoverageCategory(
        '',
      )

      setCoveragePriority(
        100,
      )

      setSuccess(
        'Cobertura global activada: el grupo podrá recibir tickets de todas las localidades y sublocalidades activas.',
      )

      return
    }

    if (!coverageSiteId) {
      setError(
        'Selecciona una localidad o utiliza "Todas las localidades".',
      )

      return
    }

    const category =
      coverageCategory
        .trim()
        .toLowerCase()
      ||
      null

    if (
      category
      &&
      !selected.tasks
        .includes(
          category,
        )
    ) {
      setError(
        'La categoría seleccionada debe existir dentro del grupo.',
      )

      return
    }

    const duplicate =
      selected.coverages
        .some(
          coverage =>
            coverage.siteId ===
              coverageSiteId
            &&
            (
              coverage.siteLocationId
              ??
              null
            ) ===
              (
                coverageLocationId
                  ? coverageLocationId
                  : null
              )
            &&
            (
              coverage.category
              ??
              null
            ) ===
              category,
        )

    if (duplicate) {
      setError(
        'Esa cobertura ya está agregada al grupo.',
      )

      return
    }

    update({
      coverages: [
        ...selected.coverages,

        {
          siteId:
            coverageSiteId,

          siteLocationId:
            coverageLocationId
              ? coverageLocationId
              : null,

          category,

          priority:
            coveragePriority,
        },
      ],
    })

    setCoverageSiteId(
      '',
    )

    setCoverageLocationId(
      '',
    )

    setCoverageCategory(
      '',
    )

    setCoveragePriority(
      100,
    )
  }

'@

Replace-RegexOnce `
    -Path $path `
    -Pattern $pattern `
    -Replacement $replacement `
    -Label "Frontend: cobertura global"

# ------------------------------------------------------------
# REMOVE OLD COVERAGE REQUIREMENT
# ------------------------------------------------------------

$pattern = @'
(?s)\s+if \(\s*!selected\.coverages\.length\s*\) \{\s*setError\(\s*'Agrega al menos una localidad de cobertura\.'\s*,?\s*\)\s*return\s*\}
'@

$text =
    Get-Text $path

$regex =
    [regex]::new(
        $pattern,
        [System.Text.RegularExpressions.RegexOptions]::Singleline
    )

if ($regex.IsMatch($text)) {
    $text =
        $regex.Replace(
            $text,
            "",
            1
        )

    Save-Text `
        -Path $path `
        -Content $text

    Write-Host "[OK] Frontend: eliminado bloqueo de cobertura obligatoria" -ForegroundColor Green
}
else {
    Write-Host "[SKIP] Bloque antiguo de cobertura obligatoria ya no existe." -ForegroundColor Yellow
}

# ------------------------------------------------------------
# ADD "TODAS LAS LOCALIDADES"
# ------------------------------------------------------------

$text =
    Get-Text $path

if (!$text.Contains('value="__all__"')) {
    $pattern = @'
(<option value="">\s*Selecciona\s*</option>)
'@

    $replacement = @'
$1

                          <option value="__all__">
                            Todas las localidades
                          </option>
'@

    Replace-RegexOnce `
        -Path $path `
        -Pattern $pattern `
        -Replacement $replacement `
        -Label "Frontend: opción Todas las localidades"
}
else {
    Write-Host "[SKIP] Opción global ya existe." -ForegroundColor Yellow
}

# ------------------------------------------------------------
# SUBLOCATION DISABLED FOR GLOBAL
# ------------------------------------------------------------

$text =
    Get-Text $path

$oldPattern = @'
disabled=\{\s*!coverageSiteId\s*\}
'@

$regex =
    [regex]::new(
        $oldPattern,
        [System.Text.RegularExpressions.RegexOptions]::Singleline
    )

if ($regex.IsMatch($text)) {
    $text =
        $regex.Replace(
            $text,
@'
disabled={
                            !coverageSiteId
                            ||
                            coverageSiteId ===
                              '__all__'
                          }
'@,
            1
        )

    Save-Text `
        -Path $path `
        -Content $text

    Write-Host "[OK] Frontend: sublocalidad deshabilitada en cobertura global" -ForegroundColor Green
}

# ------------------------------------------------------------
# GLOBAL COVERAGE DISPLAY
# ------------------------------------------------------------

$text =
    Get-Text $path

$text =
    $text.Replace(
        'Sin cobertura configurada.',
        'Cobertura global · Todas las localidades y sublocalidades activas.'
    )

Save-Text `
    -Path $path `
    -Content $text

Write-Host "[OK] Frontend: indicador visual de cobertura global" -ForegroundColor Green

# ============================================================
# GROUP PLANNING BACKEND
# ZERO COVERAGES IS VALID AND READY
# ============================================================

$path =
    $files.GroupPlanning

$text =
    Get-Text $path

# ------------------------------------------------------------
# READY FOR AUTO ROUTING
# ------------------------------------------------------------

$pattern = @'
readyForAutomaticRouting\s*=\s*request\.Coverages\.Length\s*>\s*0\s*&&\s*request\.Technicians\.Any
'@

$regex =
    [regex]::new(
        $pattern,
        [System.Text.RegularExpressions.RegexOptions]::Singleline
    )

if ($regex.IsMatch($text)) {
    $text =
        $regex.Replace(
            $text,
            "readyForAutomaticRouting =`r`n                    request.Technicians.Any",
            1
        )

    Save-Text `
        -Path $path `
        -Content $text

    Write-Host "[OK] Backend: cobertura global considerada ready" -ForegroundColor Green
}

# ------------------------------------------------------------
# REMOVE "SIN COBERTURA" READINESS ERROR
# ------------------------------------------------------------

$text =
    Get-Text $path

$pattern = @'
(?s)\s*if \(\s*coverageCount\s*==\s*0\s*\)\s*\{\s*issues\.Add\(\s*"Sin cobertura"\s*\);\s*\}
'@

$regex =
    [regex]::new(
        $pattern,
        [System.Text.RegularExpressions.RegexOptions]::Singleline
    )

if ($regex.IsMatch($text)) {
    $text =
        $regex.Replace(
            $text,
            "",
            1
        )

    Save-Text `
        -Path $path `
        -Content $text

    Write-Host "[OK] Backend: 0 coberturas = global" -ForegroundColor Green
}
else {
    Write-Host "[SKIP] Readiness global ya corregido." -ForegroundColor Yellow
}

# ============================================================
# HD-D4
# IMMEDIATE ROUTING WHEN CREATING A TICKET
# ============================================================

$path =
    $files.HelpdeskService

$pattern = @'
(?s)        // ========================================================\r?\n        // ROUTING\r?\n        // ========================================================.*?(?=        await _db\r?\n            \.SaveChangesAsync\()
'@

$replacement = @'
        // ========================================================
        // HD-D4 - IMMEDIATE ENTERPRISE ROUTING
        // ========================================================

        /*
         * El routing determinístico se intenta inmediatamente.
         *
         * Reglas:
         *
         * - dispositivo -> localidad;
         * - solicitante -> localidad;
         * - grupo solicitado -> cobertura;
         * - cobertura global -> no requiere Site;
         * - categoría general puede ser clasificada posteriormente
         *   por HelpdeskRoutingWorker;
         * - OpenRouter NO selecciona técnico.
         */
        var routingEvaluation =
            await EvaluateRoutingAsync(
                organizationId,
                requesterId,
                ticket.Category,
                cancellationToken,
                ticket.RequestedTeamId,
                ticket.SiteId,
                ticket.SiteLocationId);

        var routing =
            routingEvaluation.Candidate;

        if (
            routing is not null)
        {
            /*
             * Si el motor pudo inferir una localidad segura,
             * persistimos dicha información en el ticket.
             */
            if (
                !ticket.SiteId.HasValue
                &&
                routingEvaluation.SiteId.HasValue)
            {
                ticket.AssignSite(
                    routingEvaluation.SiteId,
                    routingEvaluation.SiteLocationId);

                _db.HelpdeskTicketEvents
                    .Add(
                        new HelpdeskTicketEvent(
                            organizationId,
                            ticket.Id,
                            actorUserId,
                            "site_inferred",
                            "Localidad inferida automáticamente por el motor de routing."));
            }

            ticket.SelectGroup(
                routing.TeamId);

            ticket.Assign(
                routing.UserId);

            var autoAssignmentSummary =
                "Asignación automática inmediata: " +
                BuildRoutingReason(
                    routingEvaluation.RequesterLocation
                    ??
                    "Sin localidad",
                    ticket.Category,
                    routing);

            _db.HelpdeskTicketEvents
                .Add(
                    new HelpdeskTicketEvent(
                        organizationId,
                        ticket.Id,
                        actorUserId,
                        "auto_assigned",
                        TrimSummary(
                            autoAssignmentSummary)));
        }
        else
        {
            _db.HelpdeskTicketEvents
                .Add(
                    new HelpdeskTicketEvent(
                        organizationId,
                        ticket.Id,
                        actorUserId,
                        "routing_pending",
                        TrimSummary(
                            "Sin asignación automática inmediata: " +
                            routingEvaluation.Reason)));
        }

'@

Replace-RegexOnce `
    -Path $path `
    -Pattern $pattern `
    -Replacement $replacement `
    -Label "HD-D4: routing inmediato al crear ticket"

# ============================================================
# HD-D5
# DIAGNOSTIC BY HD-XX OR GUID
# ============================================================

# ------------------------------------------------------------
# INTERFACE
# ------------------------------------------------------------

$path =
    $files.Interface

$method = @'

    Task<HelpdeskRoutingDiagnosticSnapshot?>
        GetRoutingDiagnosticByReferenceAsync(
            Guid organizationId,
            string ticketReference,
            CancellationToken cancellationToken = default);

'@

Insert-BeforeOnce `
    -Path $path `
    -Marker "    Task<HelpdeskRoutingDiagnosticSnapshot?> GetRoutingDiagnosticAsync(" `
    -Content $method `
    -Guard "GetRoutingDiagnosticByReferenceAsync" `
    -Label "Interface: diagnóstico por HD-XX o GUID"

# ------------------------------------------------------------
# SERVICE
# ------------------------------------------------------------

$path =
    $files.RoutingOperations

$method = @'
    // ============================================================
    // HD-D5
    // DIAGNOSTIC BY BUSINESS NUMBER OR INTERNAL GUID
    // ============================================================

    public async Task<HelpdeskRoutingDiagnosticSnapshot?>
        GetRoutingDiagnosticByReferenceAsync(
            Guid organizationId,
            string ticketReference,
            CancellationToken cancellationToken = default)
    {
        var reference =
            (
                ticketReference
                ??
                string.Empty
            )
            .Trim();

        if (string.IsNullOrWhiteSpace(
                reference))
        {
            return null;
        }

        if (
            Guid.TryParse(
                reference,
                out var ticketId))
        {
            return await GetRoutingDiagnosticAsync(
                organizationId,
                ticketId,
                cancellationToken);
        }

        var normalized =
            reference
                .Trim()
                .ToUpperInvariant();

        /*
         * Permitimos:
         *
         * HD-5
         * hd-5
         * 5
         *
         * pero el identificador comercial almacenado continúa
         * siendo el canónico HD-X.
         */
        if (
            int.TryParse(
                normalized,
                out var numericTicket)
            &&
            numericTicket >
                0)
        {
            normalized =
                $"HD-{numericTicket}";
        }

        var resolvedId =
            await _db.HelpdeskTickets
                .AsNoTracking()
                .Where(
                    x =>
                        x.OrganizationId ==
                            organizationId
                        &&
                        x.Number
                            .ToUpper() ==
                            normalized)
                .Select(
                    x =>
                        (Guid?)
                        x.Id)
                .FirstOrDefaultAsync(
                    cancellationToken);

        if (!resolvedId.HasValue)
        {
            return null;
        }

        return await GetRoutingDiagnosticAsync(
            organizationId,
            resolvedId.Value,
            cancellationToken);
    }

'@

Insert-BeforeOnce `
    -Path $path `
    -Marker "    public async Task<HelpdeskRoutingDiagnosticSnapshot?> GetRoutingDiagnosticAsync(" `
    -Content $method `
    -Guard "GetRoutingDiagnosticByReferenceAsync" `
    -Label "Service: resolver ticket por HD-XX o GUID"

# ------------------------------------------------------------
# ROUTING CONTROLLER
# ------------------------------------------------------------

$path =
    $files.RoutingController

$endpoint = @'
    // ============================================================
    // HD-D5 - TICKET DIAGNOSTIC BY REFERENCE
    //
    // Accepts:
    // - HD-16
    // - 16
    // - internal GUID
    // ============================================================

    [HttpGet("tickets/reference/{ticketReference}/diagnostic")]
    public async Task<ActionResult<HelpdeskRoutingDiagnosticSnapshot>>
        DiagnosticByReference(
            string ticketReference,
            CancellationToken cancellationToken)
    {
        if (!CanView())
        {
            return Forbid();
        }

        if (!TryGetOrganization(
                out var organizationId))
        {
            return Unauthorized();
        }

        var result =
            await _helpdesk
                .GetRoutingDiagnosticByReferenceAsync(
                    organizationId,
                    ticketReference,
                    cancellationToken);

        if (result is null)
        {
            return NotFound(
                new
                {
                    message =
                        $"No se encontró el ticket '{ticketReference}'."
                });
        }

        return Ok(
            result);
    }

'@

Insert-BeforeOnce `
    -Path $path `
    -Marker "    [HttpGet(`"tickets/{ticketId:guid}/diagnostic`")]" `
    -Content $endpoint `
    -Guard "DiagnosticByReference" `
    -Label "Controller: diagnóstico por referencia"

# ------------------------------------------------------------
# FRONTEND API
# ------------------------------------------------------------

$path =
    $files.RoutingApi

$text =
    Get-Text $path

$pattern = @'
export async function getRoutingDiagnostic\(\s*ticketId: string,\s*\):\s*Promise<RoutingDiagnostic> \{.*?\n\}
'@

$replacement = @'
export async function getRoutingDiagnostic(
  ticketReference: string,
):
  Promise<RoutingDiagnostic> {
  const reference =
    encodeURIComponent(
      ticketReference
        .trim(),
    )

  const {
    data,
  } =
    await apiClient
      .get<RoutingDiagnostic>(
        `/helpdesk/routing/tickets/reference/${reference}/diagnostic`,
      )

  return data
}
'@

Replace-RegexOnce `
    -Path $path `
    -Pattern $pattern `
    -Replacement $replacement `
    -Label "Frontend API: diagnóstico HD-XX/GUID"

# ------------------------------------------------------------
# FRONTEND PANEL
# ------------------------------------------------------------

$path =
    $files.RoutingPanel

$text =
    Get-Text $path

$text =
    $text.Replace(
        "'Introduce el ID interno del ticket.'",
        "'Introduce el número del ticket (ej. HD-16) o su GUID interno.'"
    )

$text =
    $text.Replace(
        'placeholder="GUID interno del ticket"',
        'placeholder="HD-16 o GUID interno"'
    )

Save-Text `
    -Path $path `
    -Content $text

Write-Host "[OK] Frontend: diagnóstico acepta HD-XX" -ForegroundColor Green

# ============================================================
# FIX DIAGNOSTIC CANDIDATE FILTER
# General + requested group must not disappear from diagnostics.
# ============================================================

$path =
    $files.RoutingOperations

$text =
    Get-Text $path

$pattern = @'
if \(!TeamContainsCategory\(team\.Categories, ticket\.Category\)\)\s*\{\s*continue;\s*\}
'@

$replacement = @'
var genericCategory =
                string.Equals(
                    ticket.Category,
                    "general",
                    StringComparison.OrdinalIgnoreCase);

            var teamMatches =
                ticket.RequestedTeamId.HasValue
                    ? team.Id ==
                      ticket.RequestedTeamId.Value
                    : TeamContainsCategory(
                        team.Categories,
                        ticket.Category);

            /*
             * Si el motor ya encontró el grupo como candidato,
             * el diagnóstico debe mostrarlo aunque el ticket
             * continúe temporalmente en "general".
             */
            if (
                !teamMatches
                &&
                !(
                    genericCategory
                    &&
                    routing.Candidate?.TeamId ==
                        team.Id
                ))
            {
                continue;
            }
'@

$regex =
    [regex]::new(
        $pattern,
        [System.Text.RegularExpressions.RegexOptions]::Singleline
    )

if ($regex.IsMatch($text)) {
    $text =
        $regex.Replace(
            $text,
            $replacement,
            1
        )

    Save-Text `
        -Path $path `
        -Content $text

    Write-Host "[OK] Diagnóstico alineado con routing real" -ForegroundColor Green
}
else {
    Write-Host "[SKIP] Filtro diagnóstico ya fue actualizado." -ForegroundColor Yellow
}

# ============================================================
# FORMAT / BASIC STATIC CHECK
# ============================================================

Write-Host ""
Write-Host "============================================================" -ForegroundColor Cyan
Write-Host " VALIDACION" -ForegroundColor Cyan
Write-Host "============================================================" -ForegroundColor Cyan
Write-Host ""

Write-Host "Ejecutando dotnet format whitespace..." -ForegroundColor Cyan

dotnet format `
    .\TitanMDM.slnx `
    whitespace `
    --no-restore

if ($LASTEXITCODE -ne 0) {
    throw "dotnet format falló."
}

Write-Host ""
Write-Host "Ejecutando BUILD SOLUTION..." -ForegroundColor Cyan

dotnet build `
    .\TitanMDM.slnx

if ($LASTEXITCODE -ne 0) {
    throw "BUILD BACKEND/SOLUTION FALLIDO."
}

Write-Host ""
Write-Host "Ejecutando UNIT TESTS..." -ForegroundColor Cyan

dotnet test `
    .\tests\TitanMDM.UnitTests\TitanMDM.UnitTests.csproj `
    --no-build

if ($LASTEXITCODE -ne 0) {
    throw "UNIT TESTS FALLARON."
}

Write-Host ""
Write-Host "Ejecutando FRONTEND BUILD..." -ForegroundColor Cyan

npm `
    --prefix `
    .\src\frontend\titanmdm-web `
    run build

if ($LASTEXITCODE -ne 0) {
    throw "FRONTEND BUILD FALLIDO."
}

Write-Host ""
Write-Host "============================================================" -ForegroundColor Green
Write-Host " HD-D4 + HD-D5 COMPLETADOS" -ForegroundColor Green
Write-Host "============================================================" -ForegroundColor Green
Write-Host ""
Write-Host "Implementado:" -ForegroundColor Cyan
Write-Host "  [OK] 0 coberturas = todas las localidades"
Write-Host "  [OK] Todas las sublocalidades incluidas"
Write-Host "  [OK] Múltiples técnicos por grupo"
Write-Host "  [OK] Routing inmediato al crear ticket"
Write-Host "  [OK] Worker mantiene reintentos automáticos"
Write-Host "  [OK] Diagnóstico por HD-XX, número o GUID"
Write-Host "  [OK] General + grupo explícito no bloquea routing"
Write-Host "  [OK] OpenRouter queda fuera de selección de técnico"
Write-Host ""
Write-Host "NO NECESITA MIGRACION." -ForegroundColor Yellow
Write-Host "NO HAGAS PUSH TODAVIA." -ForegroundColor Yellow
Write-Host ""
Write-Host "Backup: $backupRoot" -ForegroundColor DarkGray
Write-Host ""