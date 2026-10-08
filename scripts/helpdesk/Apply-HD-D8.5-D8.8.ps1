$ErrorActionPreference = "Stop"

Set-Location "C:\TitanMDM"

# =====================================================================
# TITANMDM
# HD-D8.5 + HD-D8.6 + HD-D8.7 + HD-D8.8
#
# Objetivos:
#
# HD-D8.5
#   - "general" no bloquea grupos especializados.
#   - "general" no bloquea cobertura territorial existente.
#
# HD-D8.6
#   - calcular candidato dentro de transacción SERIALIZABLE.
#
# HD-D8.7
#   - recalcular carga/capacidad para cada ticket.
#
# HD-D8.8
#   - conservar fairness mediante eventos auto_assigned.
#
# IMPORTANTE:
#   - no crea migraciones.
#   - no hace git add/commit/push.
#   - crea backup local.
#   - aborta si la estructura actual no coincide.
# =====================================================================

Write-Host ""
Write-Host "==============================================================" -ForegroundColor Cyan
Write-Host " TITANMDM - HD-D8.5 + D8.6 + D8.7 + D8.8" -ForegroundColor Cyan
Write-Host " GENERAL ROUTING + TRANSACTION + FAIR DISTRIBUTION" -ForegroundColor Cyan
Write-Host "==============================================================" -ForegroundColor Cyan
Write-Host ""

# =====================================================================
# PATHS
# =====================================================================

$routingFile = Join-Path `
    $PWD `
    "src\backend\TitanMDM.Infrastructure\Helpdesk\HelpdeskService.Routing.cs"

$enterpriseFile = Join-Path `
    $PWD `
    "src\backend\TitanMDM.Infrastructure\Helpdesk\HelpdeskService.RoutingEnterprise.cs"

$infrastructureProject = Join-Path `
    $PWD `
    "src\backend\TitanMDM.Infrastructure\TitanMDM.Infrastructure.csproj"

$apiProject = Join-Path `
    $PWD `
    "src\backend\TitanMDM.Api\TitanMDM.Api.csproj"

$unitTestsProject = Join-Path `
    $PWD `
    "tests\TitanMDM.UnitTests\TitanMDM.UnitTests.csproj"

$frontendFolder = Join-Path `
    $PWD `
    "src\frontend\titanmdm-web"

$backupFolder = Join-Path `
    $PWD `
    ".titan-local\backups\HD-D8.5-D8.8-final"

# =====================================================================
# HELPERS
# =====================================================================

function Write-Step
{
    param(
        [Parameter(Mandatory = $true)]
        [string] $Text
    )

    Write-Host ""
    Write-Host "--------------------------------------------------------------" -ForegroundColor DarkCyan
    Write-Host " $Text" -ForegroundColor Cyan
    Write-Host "--------------------------------------------------------------" -ForegroundColor DarkCyan
}

function Assert-File
{
    param(
        [Parameter(Mandatory = $true)]
        [string] $Path
    )

    if (-not (Test-Path -LiteralPath $Path))
    {
        throw "Archivo requerido no encontrado: $Path"
    }
}

function Read-NormalizedText
{
    param(
        [Parameter(Mandatory = $true)]
        [string] $Path
    )

    $content = Get-Content `
        -LiteralPath $Path `
        -Raw

    if ($null -eq $content)
    {
        return ""
    }

    # IMPORTANTE:
    # No utilizar:
    #
    # (Get-Content ...)
    #     .Replace(...)
    #
    # PowerShell puede interpretar .Replace como un comando separado.
    #
    # Se utiliza -replace deliberadamente.
    return ($content -replace "`r`n", "`n")
}

function Write-Utf8Text
{
    param(
        [Parameter(Mandatory = $true)]
        [string] $Path,

        [Parameter(Mandatory = $true)]
        [string] $Content
    )

    # UTF8 sin BOM.
    $utf8 = New-Object System.Text.UTF8Encoding($false)

    [System.IO.File]::WriteAllText(
        $Path,
        $Content,
        $utf8
    )
}

function Assert-Contains
{
    param(
        [Parameter(Mandatory = $true)]
        [string] $Content,

        [Parameter(Mandatory = $true)]
        [string] $Expected,

        [Parameter(Mandatory = $true)]
        [string] $Description
    )

    if (-not $Content.Contains($Expected))
    {
        throw "VALIDACION FALLIDA: $Description"
    }
}

function Assert-Build
{
    param(
        [Parameter(Mandatory = $true)]
        [string] $Description
    )

    if ($LASTEXITCODE -ne 0)
    {
        throw "$Description fallo con codigo $LASTEXITCODE."
    }
}

# =====================================================================
# STEP 1
# PRECONDITIONS
# =====================================================================

Write-Step "1. VALIDANDO ESTRUCTURA ACTUAL"

Assert-File $routingFile
Assert-File $enterpriseFile
Assert-File $infrastructureProject
Assert-File $apiProject
Assert-File $unitTestsProject

if (-not (Test-Path -LiteralPath $frontendFolder))
{
    throw "No existe el frontend: $frontendFolder"
}

$routing = Read-NormalizedText $routingFile
$enterprise = Read-NormalizedText $enterpriseFile

Write-Host "[OK] Routing.cs encontrado." -ForegroundColor Green
Write-Host "[OK] RoutingEnterprise.cs encontrado." -ForegroundColor Green

# =====================================================================
# STEP 2
# HD-D8.5 - GENERAL TEAM POLICY
# =====================================================================

Write-Step "2. PREPARANDO HD-D8.5 - GENERAL ROUTING"

$oldTeams = @'
        var teams =
            allTeams
                .Where(
                    x =>
                        x.HandlesCategory(
                            normalizedCategory)
                        ||
                        (
                            normalizedCategory ==
                                "general"
                            &&
                            string.IsNullOrWhiteSpace(
                                x.Categories)
                        ))
                .ToDictionary(
                    x => x.Id);
'@

$newTeams = @'
        var teams =
            allTeams
                .Where(
                    x =>
                        normalizedCategory ==
                            "general"
                        ||
                        string.IsNullOrWhiteSpace(
                            x.Categories)
                        ||
                        x.HandlesCategory(
                            normalizedCategory))
                .ToDictionary(
                    x => x.Id);
'@

$teamsAlreadyUpdated = $routing.Contains($newTeams)

if (-not $teamsAlreadyUpdated)
{
    Assert-Contains `
        -Content $routing `
        -Expected $oldTeams `
        -Description "No se encontro el bloque real de seleccion de grupos en Routing.cs."

    $routing = $routing.Replace(
        $oldTeams,
        $newTeams
    )

    Write-Host "[OK] Politica GENERAL para grupos preparada." -ForegroundColor Green
}
else
{
    Write-Host "[OK] Politica GENERAL para grupos ya existe." -ForegroundColor Green
}

# =====================================================================
# STEP 3
# HD-D8.5 - GENERAL COVERAGE POLICY
# =====================================================================

$oldExactCategory = @'
                        var exactCategory =
                            string.Equals(
                                coverage.Category,
                                normalizedCategory,
                                StringComparison
                                    .OrdinalIgnoreCase);
'@

$newExactCategory = @'
                        var exactCategory =
                            normalizedCategory ==
                                "general"
                            ||
                            string.Equals(
                                coverage.Category,
                                normalizedCategory,
                                StringComparison
                                    .OrdinalIgnoreCase);
'@

$coverageAlreadyUpdated = $routing.Contains($newExactCategory)

if (-not $coverageAlreadyUpdated)
{
    Assert-Contains `
        -Content $routing `
        -Expected $oldExactCategory `
        -Description "No se encontro el bloque exactCategory esperado en Routing.cs."

    $routing = $routing.Replace(
        $oldExactCategory,
        $newExactCategory
    )

    Write-Host "[OK] Politica GENERAL para cobertura preparada." -ForegroundColor Green
}
else
{
    Write-Host "[OK] Politica GENERAL para cobertura ya existe." -ForegroundColor Green
}

# =====================================================================
# STEP 4
# LOCATE ENTERPRISE METHOD SAFELY
# =====================================================================

Write-Step "3. LOCALIZANDO PIPELINE ENTERPRISE"

$methodStartNeedle = @'
    public async Task<bool>
        RetryAutomaticAssignmentEnterpriseAsync(
'@

$nextSectionNeedle = @'
    // ============================================================
    // ROUTING WAITING AUDIT
'@

$methodStart = $enterprise.IndexOf(
    $methodStartNeedle,
    [System.StringComparison]::Ordinal
)

if ($methodStart -lt 0)
{
    throw "No se encontro RetryAutomaticAssignmentEnterpriseAsync en RoutingEnterprise.cs."
}

$methodEnd = $enterprise.IndexOf(
    $nextSectionNeedle,
    $methodStart,
    [System.StringComparison]::Ordinal
)

if ($methodEnd -lt 0)
{
    throw "No se encontro la seccion ROUTING WAITING AUDIT posterior al metodo."
}

if ($methodEnd -le $methodStart)
{
    throw "Los limites del metodo Enterprise son invalidos."
}

Write-Host "[OK] Inicio metodo: $methodStart" -ForegroundColor Green
Write-Host "[OK] Fin metodo:    $methodEnd" -ForegroundColor Green

# =====================================================================
# STEP 5
# HD-D8.6 + D8.7 + D8.8
# =====================================================================

Write-Step "4. PREPARANDO PIPELINE TRANSACCIONAL"

$newEnterpriseMethod = @'
    public async Task<bool>
        RetryAutomaticAssignmentEnterpriseAsync(
            Guid organizationId,
            Guid ticketId,
            CancellationToken cancellationToken = default)
    {
        // ========================================================
        // HD-D8.6 / HD-D8.7 / HD-D8.8
        //
        // La decision de routing se calcula dentro de SERIALIZABLE.
        //
        // Consecuencia:
        //
        // Ticket A:
        //     carga tecnicos -> seleccion -> commit
        //
        // Ticket B:
        //     observa la asignacion de A -> nueva seleccion
        //
        // Ticket C:
        //     observa A + B -> nueva seleccion
        //
        // Esto permite repartir carga entre candidatos equivalentes
        // sin depender de un candidato calculado previamente.
        // ========================================================

        var strategy =
            _db
                .Database
                .CreateExecutionStrategy();

        return await strategy.ExecuteAsync(
            async () =>
            {
                await using var transaction =
                    await _db
                        .Database
                        .BeginTransactionAsync(
                            System.Data.IsolationLevel.Serializable,
                            cancellationToken);

                // =================================================
                // FRESH TICKET
                // =================================================

                var ticket =
                    await _db
                        .HelpdeskTickets
                        .AsNoTracking()
                        .FirstOrDefaultAsync(
                            x =>
                                x.OrganizationId ==
                                    organizationId
                                &&
                                x.Id ==
                                    ticketId
                                &&
                                x.AssigneeUserId ==
                                    null
                                &&
                                x.Status !=
                                    "resolved"
                                &&
                                x.Status !=
                                    "closed"
                                &&
                                x.Status !=
                                    "pendinguser",
                            cancellationToken);

                if (ticket is null)
                {
                    await transaction.RollbackAsync(
                        cancellationToken);

                    return false;
                }

                // =================================================
                // EMAIL IDENTITY SAFETY
                // =================================================

                if (
                    string.Equals(
                        ticket.Source,
                        "email",
                        StringComparison.OrdinalIgnoreCase)
                    &&
                    !string.IsNullOrWhiteSpace(
                        ticket.ExternalRequesterEmail))
                {
                    var requester =
                        await _db
                            .Users
                            .AsNoTracking()
                            .Where(
                                x =>
                                    x.OrganizationId ==
                                        organizationId
                                    &&
                                    x.Id ==
                                        ticket.RequesterUserId
                                    &&
                                    x.IsActive)
                            .Select(
                                x =>
                                    new
                                    {
                                        x.Id,
                                        x.Email,
                                        x.SiteId,
                                        x.SiteLocationId
                                    })
                            .FirstOrDefaultAsync(
                                cancellationToken);

                    if (requester is null)
                    {
                        await transaction.RollbackAsync(
                            cancellationToken);

                        await RecordRoutingWaitingEnterpriseAsync(
                            organizationId,
                            ticketId,
                            "El remitente no esta vinculado a un usuario activo de TitanMDM.",
                            cancellationToken);

                        return false;
                    }

                    if (
                        !string.Equals(
                            requester.Email?.Trim(),
                            ticket.ExternalRequesterEmail.Trim(),
                            StringComparison.OrdinalIgnoreCase))
                    {
                        await transaction.RollbackAsync(
                            cancellationToken);

                        await RecordRoutingWaitingEnterpriseAsync(
                            organizationId,
                            ticketId,
                            "La identidad corporativa resuelta no coincide con el remitente del correo.",
                            cancellationToken);

                        return false;
                    }
                }

                // =================================================
                // FRESH ROUTING
                //
                // CRITICO:
                // esta evaluacion ahora ocurre DENTRO
                // de SERIALIZABLE.
                // =================================================

                var routing =
                    await EvaluateRoutingAsync(
                        organizationId,
                        ticket.RequesterUserId,
                        ticket.Category,
                        cancellationToken,
                        ticket.RequestedTeamId,
                        ticket.SiteId,
                        ticket.SiteLocationId);

                if (
                    routing.Candidate
                    is not { } candidate)
                {
                    var reason =
                        string.IsNullOrWhiteSpace(
                            routing.Reason)
                            ? "No existe candidato valido."
                            : routing.Reason;

                    await transaction.RollbackAsync(
                        cancellationToken);

                    await RecordRoutingWaitingEnterpriseAsync(
                        organizationId,
                        ticketId,
                        reason,
                        cancellationToken);

                    return false;
                }

                // =================================================
                // TECHNICIAN ACTIVE
                // =================================================

                var technicianActive =
                    await _db
                        .Users
                        .AsNoTracking()
                        .AnyAsync(
                            x =>
                                x.OrganizationId ==
                                    organizationId
                                &&
                                x.Id ==
                                    candidate.UserId
                                &&
                                x.IsActive,
                            cancellationToken);

                if (!technicianActive)
                {
                    await transaction.RollbackAsync(
                        cancellationToken);

                    await RecordRoutingWaitingEnterpriseAsync(
                        organizationId,
                        ticketId,
                        "El tecnico seleccionado ya no esta activo.",
                        cancellationToken);

                    return false;
                }

                // =================================================
                // MEMBERSHIP REVALIDATION
                // =================================================

                var membership =
                    await _db
                        .HelpdeskTeamMembers
                        .AsNoTracking()
                        .FirstOrDefaultAsync(
                            x =>
                                x.OrganizationId ==
                                    organizationId
                                &&
                                x.TeamId ==
                                    candidate.TeamId
                                &&
                                x.UserId ==
                                    candidate.UserId
                                &&
                                x.IsAvailable
                                &&
                                x.AcceptsAutomaticAssignments
                                &&
                                x.MaxOpenTickets > 0,
                            cancellationToken);

                if (membership is null)
                {
                    await transaction.RollbackAsync(
                        cancellationToken);

                    await RecordRoutingWaitingEnterpriseAsync(
                        organizationId,
                        ticketId,
                        "El tecnico seleccionado ya no acepta autoasignaciones para el grupo.",
                        cancellationToken);

                    return false;
                }

                // =================================================
                // CURRENT LOAD
                // =================================================

                var currentLoad =
                    await _db
                        .HelpdeskTickets
                        .AsNoTracking()
                        .CountAsync(
                            x =>
                                x.OrganizationId ==
                                    organizationId
                                &&
                                x.AssigneeUserId ==
                                    candidate.UserId
                                &&
                                x.Id !=
                                    ticketId
                                &&
                                x.Status !=
                                    "resolved"
                                &&
                                x.Status !=
                                    "closed",
                            cancellationToken);

                if (
                    currentLoad >=
                    membership.MaxOpenTickets)
                {
                    await transaction.RollbackAsync(
                        cancellationToken);

                    await RecordRoutingWaitingEnterpriseAsync(
                        organizationId,
                        ticketId,
                        "El tecnico seleccionado alcanzo su capacidad maxima.",
                        cancellationToken);

                    return false;
                }

                // =================================================
                // SCHEDULE REVALIDATION
                // =================================================

                var schedules =
                    await _db
                        .Set<HelpdeskTechnicianSchedule>()
                        .AsNoTracking()
                        .Where(
                            x =>
                                x.OrganizationId ==
                                    organizationId
                                &&
                                x.TeamId ==
                                    candidate.TeamId
                                &&
                                x.UserId ==
                                    candidate.UserId)
                        .ToListAsync(
                            cancellationToken);

                if (schedules.Count == 0)
                {
                    await transaction.RollbackAsync(
                        cancellationToken);

                    await RecordRoutingWaitingEnterpriseAsync(
                        organizationId,
                        ticketId,
                        "El tecnico seleccionado no posee horario configurado.",
                        cancellationToken);

                    return false;
                }

                var now =
                    DateTime.UtcNow;

                var activeSchedule =
                    schedules
                        .Where(
                            x =>
                                x.IsOnDuty(
                                    now))
                        .OrderBy(
                            x =>
                                x.Priority)
                        .FirstOrDefault();

                if (activeSchedule is null)
                {
                    await transaction.RollbackAsync(
                        cancellationToken);

                    await RecordRoutingWaitingEnterpriseAsync(
                        organizationId,
                        ticketId,
                        "El tecnico seleccionado esta fuera de turno.",
                        cancellationToken);

                    return false;
                }

                // =================================================
                // FINAL LOAD CHECK
                //
                // Mantenerlo inmediatamente antes del UPDATE.
                // =================================================

                currentLoad =
                    await _db
                        .HelpdeskTickets
                        .AsNoTracking()
                        .CountAsync(
                            x =>
                                x.OrganizationId ==
                                    organizationId
                                &&
                                x.AssigneeUserId ==
                                    candidate.UserId
                                &&
                                x.Id !=
                                    ticketId
                                &&
                                x.Status !=
                                    "resolved"
                                &&
                                x.Status !=
                                    "closed",
                            cancellationToken);

                if (
                    currentLoad >=
                    membership.MaxOpenTickets)
                {
                    await transaction.RollbackAsync(
                        cancellationToken);

                    await RecordRoutingWaitingEnterpriseAsync(
                        organizationId,
                        ticketId,
                        "La capacidad del tecnico cambio antes de confirmar la asignacion.",
                        cancellationToken);

                    return false;
                }

                // =================================================
                // ATOMIC UPDATE
                // =================================================

                var changed =
                    await _db
                        .HelpdeskTickets
                        .Where(
                            x =>
                                x.OrganizationId ==
                                    organizationId
                                &&
                                x.Id ==
                                    ticketId
                                &&
                                x.AssigneeUserId ==
                                    null
                                &&
                                x.Status !=
                                    "resolved"
                                &&
                                x.Status !=
                                    "closed"
                                &&
                                x.Status !=
                                    "pendinguser")
                        .ExecuteUpdateAsync(
                            setters =>
                                setters
                                    .SetProperty(
                                        x =>
                                            x.AssigneeUserId,
                                        (Guid?)candidate.UserId)
                                    .SetProperty(
                                        x =>
                                            x.RequestedTeamId,
                                        (Guid?)candidate.TeamId)
                                    .SetProperty(
                                        x =>
                                            x.Status,
                                        x =>
                                            x.Status == "new"
                                                ? "open"
                                                : x.Status)
                                    .SetProperty(
                                        x =>
                                            x.UpdatedAtUtc,
                                        now),
                            cancellationToken);

                if (changed != 1)
                {
                    await transaction.RollbackAsync(
                        cancellationToken);

                    return false;
                }

                // =================================================
                // FAIRNESS / AUDIT
                //
                // EvaluateRoutingAsync utiliza auto_assigned
                // para saber quien recibio tickets recientemente.
                // =================================================

                var occupancy =
                    membership.MaxOpenTickets <= 0
                        ? 100d
                        : Math.Round(
                            (
                                (double)currentLoad
                                /
                                membership.MaxOpenTickets
                            )
                            *
                            100d,
                            2);

                var summary =
                    "Autoasignacion enterprise: " +
                    BuildRoutingReason(
                        routing.RequesterLocation
                            ??
                            "Localidad no disponible",
                        ticket.Category,
                        candidate)
                    +
                    $" Ocupacion previa {occupancy:0.##}%.";

                _db
                    .HelpdeskTicketEvents
                    .Add(
                        new HelpdeskTicketEvent(
                            organizationId,
                            ticketId,
                            null,
                            "auto_assigned",
                            TrimSummary(
                                summary)));

                await _db.SaveChangesAsync(
                    cancellationToken);

                await transaction.CommitAsync(
                    cancellationToken);

                /*
                 * ExecuteUpdateAsync no sincroniza entidades
                 * previamente rastreadas.
                 */
                _db.ChangeTracker.Clear();

                return true;
            });
    }

'@

# =====================================================================
# STEP 6
# BUILD ENTERPRISE FILE IN MEMORY
# =====================================================================

$prefix = $enterprise.Substring(
    0,
    $methodStart
)

$suffix = $enterprise.Substring(
    $methodEnd
)

$newEnterprise = (
    $prefix +
    $newEnterpriseMethod +
    $suffix
)

# =====================================================================
# STEP 7
# VALIDATE MODIFICATIONS BEFORE WRITING
# =====================================================================

Write-Step "5. VALIDANDO CAMBIOS EN MEMORIA"

if (
    -not $routing.Contains(
        'normalizedCategory ==' + "`n" + '                            "general"'
    )
)
{
    throw "HD-D8.5 no quedo aplicado en memoria."
}

if (
    -not $newEnterprise.Contains(
        "Candidate calculation occurs INSIDE"
    )
)
{
    # El comentario usado ahora esta en español.
    # Se hace una segunda validacion funcional.
    if (
        -not $newEnterprise.Contains(
            "esta evaluacion ahora ocurre DENTRO"
        )
    )
    {
        throw "No se genero el nuevo pipeline Enterprise."
    }
}

if (
    -not $newEnterprise.Contains(
        "IsolationLevel.Serializable"
    )
)
{
    throw "SERIALIZABLE no esta presente en el nuevo pipeline."
}

if (
    -not $newEnterprise.Contains(
        "await EvaluateRoutingAsync("
    )
)
{
    throw "EvaluateRoutingAsync no esta presente en el pipeline."
}

if (
    -not $newEnterprise.Contains(
        '"auto_assigned"'
    )
)
{
    throw "No se encontro el evento auto_assigned."
}

Write-Host "[OK] GENERAL routing." -ForegroundColor Green
Write-Host "[OK] Enterprise transaction." -ForegroundColor Green
Write-Host "[OK] Dynamic candidate." -ForegroundColor Green
Write-Host "[OK] Fairness audit." -ForegroundColor Green

# =====================================================================
# STEP 8
# BACKUP
#
# El backup ocurre DESPUES de validar que conocemos la estructura.
# =====================================================================

Write-Step "6. CREANDO BACKUP"

New-Item `
    -ItemType Directory `
    -Path $backupFolder `
    -Force `
    | Out-Null

Copy-Item `
    -LiteralPath $routingFile `
    -Destination (
        Join-Path `
            $backupFolder `
            "HelpdeskService.Routing.before.cs"
    ) `
    -Force

Copy-Item `
    -LiteralPath $enterpriseFile `
    -Destination (
        Join-Path `
            $backupFolder `
            "HelpdeskService.RoutingEnterprise.before.cs"
    ) `
    -Force

Write-Host "[OK] Backup creado en $backupFolder" -ForegroundColor Green

# =====================================================================
# STEP 9
# WRITE
# =====================================================================

Write-Step "7. ESCRIBIENDO CAMBIOS"

Write-Utf8Text `
    -Path $routingFile `
    -Content $routing

Write-Utf8Text `
    -Path $enterpriseFile `
    -Content $newEnterprise

Write-Host "[OK] Routing.cs escrito." -ForegroundColor Green
Write-Host "[OK] RoutingEnterprise.cs escrito." -ForegroundColor Green

# =====================================================================
# STEP 10
# POST-WRITE VALIDATION
# =====================================================================

Write-Step "8. POST-WRITE VALIDATION"

$routingCheck = Read-NormalizedText $routingFile
$enterpriseCheck = Read-NormalizedText $enterpriseFile

if (
    -not $routingCheck.Contains(
        $newTeams
    )
)
{
    throw "El bloque GENERAL de Teams no fue persistido."
}

if (
    -not $routingCheck.Contains(
        $newExactCategory
    )
)
{
    throw "El bloque GENERAL de coverage no fue persistido."
}

if (
    -not $enterpriseCheck.Contains(
        "System.Data.IsolationLevel.Serializable"
    )
)
{
    throw "SERIALIZABLE no fue persistido."
}

if (
    -not $enterpriseCheck.Contains(
        "await EvaluateRoutingAsync("
    )
)
{
    throw "EvaluateRoutingAsync no fue persistido."
}

Write-Host "[OK] Archivos persistidos correctamente." -ForegroundColor Green

# =====================================================================
# STEP 11
# INFRASTRUCTURE BUILD
# =====================================================================

Write-Step "9. BUILD INFRASTRUCTURE"

dotnet build `
    $infrastructureProject `
    --nologo

Assert-Build "BUILD INFRASTRUCTURE"

Write-Host "[OK] Infrastructure build." -ForegroundColor Green

# =====================================================================
# STEP 12
# API BUILD
# =====================================================================

Write-Step "10. BUILD API"

dotnet build `
    $apiProject `
    --nologo

Assert-Build "BUILD API"

Write-Host "[OK] API build." -ForegroundColor Green

# =====================================================================
# STEP 13
# COMPLETE SOLUTION
# =====================================================================

Write-Step "11. BUILD SOLUTION"

$slnx = Join-Path $PWD "TitanMDM.slnx"
$sln = Join-Path $PWD "TitanMDM.sln"

if (Test-Path -LiteralPath $slnx)
{
    dotnet build `
        $slnx `
        --nologo
}
elseif (Test-Path -LiteralPath $sln)
{
    dotnet build `
        $sln `
        --nologo
}
else
{
    throw "No se encontro TitanMDM.slnx ni TitanMDM.sln."
}

Assert-Build "BUILD SOLUTION"

Write-Host "[OK] Solution build." -ForegroundColor Green

# =====================================================================
# STEP 14
# UNIT TESTS
# =====================================================================

Write-Step "12. UNIT TESTS"

dotnet test `
    $unitTestsProject `
    --no-restore `
    --nologo

Assert-Build "UNIT TESTS"

Write-Host "[OK] Unit tests." -ForegroundColor Green

# =====================================================================
# STEP 15
# FRONTEND
# =====================================================================

Write-Step "13. FRONTEND BUILD"

npm `
    --prefix `
    $frontendFolder `
    run build

Assert-Build "FRONTEND BUILD"

Write-Host "[OK] Frontend build." -ForegroundColor Green

# =====================================================================
# STEP 16
# SUMMARY
# =====================================================================

Write-Host ""
Write-Host "==============================================================" -ForegroundColor Green
Write-Host " HD-D8.5 + D8.6 + D8.7 + D8.8 COMPLETADOS" -ForegroundColor Green
Write-Host "==============================================================" -ForegroundColor Green
Write-Host ""

Write-Host "[OK] Categoria GENERAL no elimina grupos especializados." -ForegroundColor Green
Write-Host "[OK] Categoria GENERAL no invalida cobertura existente." -ForegroundColor Green
Write-Host "[OK] Candidate calculado dentro de SERIALIZABLE." -ForegroundColor Green
Write-Host "[OK] Workload recalculado por ticket." -ForegroundColor Green
Write-Host "[OK] Membership revalidado." -ForegroundColor Green
Write-Host "[OK] Capacidad revalidada." -ForegroundColor Green
Write-Host "[OK] Turno revalidado." -ForegroundColor Green
Write-Host "[OK] UPDATE atomico." -ForegroundColor Green
Write-Host "[OK] Evento auto_assigned conservado." -ForegroundColor Green
Write-Host "[OK] Infrastructure compilado." -ForegroundColor Green
Write-Host "[OK] API compilada." -ForegroundColor Green
Write-Host "[OK] Solution compilada." -ForegroundColor Green
Write-Host "[OK] Unit Tests completados." -ForegroundColor Green
Write-Host "[OK] Frontend compilado." -ForegroundColor Green

Write-Host ""
Write-Host "NO NECESITA MIGRACION." -ForegroundColor Yellow
Write-Host "NO HAGAS PUSH TODAVIA." -ForegroundColor Yellow

Write-Host ""
Write-Host "==============================================================" -ForegroundColor Cyan
Write-Host " SIGUIENTE: PRUEBA FUNCIONAL REAL DE AUTOASIGNACION" -ForegroundColor Cyan
Write-Host "==============================================================" -ForegroundColor Cyan

Write-Host ""
Write-Host "1. Reiniciar backend." -ForegroundColor White
Write-Host "2. NO pulsar Procesar cola." -ForegroundColor White
Write-Host "3. Crear/enviar ticket nuevo." -ForegroundColor White
Write-Host "4. Esperar el ciclo automatico." -ForegroundColor White
Write-Host "5. Verificar tecnico asignado automaticamente." -ForegroundColor White
Write-Host "6. Crear 4-5 tickets equivalentes." -ForegroundColor White
Write-Host "7. Verificar distribucion entre tecnicos elegibles." -ForegroundColor White

Write-Host ""
Write-Host "Git status:" -ForegroundColor Cyan

git status --short