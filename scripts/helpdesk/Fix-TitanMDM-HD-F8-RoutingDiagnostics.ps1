$ErrorActionPreference = 'Stop'
Set-Location 'C:\TitanMDM'
$file = 'src\backend\TitanMDM.Infrastructure\Helpdesk\HelpdeskService.RoutingOperations.cs'
if (!(Test-Path $file)) { throw "Archivo no encontrado: $file" }
$branch = (git branch --show-current).Trim()
if ($LASTEXITCODE -ne 0 -or $branch -in @('main','master')) { throw 'Utilice una rama de trabajo.' }
$original = [System.IO.File]::ReadAllText((Resolve-Path $file).Path)
$updated = $original
$anchor = '        var candidates =' + "`n" + '            new List<HelpdeskRoutingCandidatePreview>();'
$normalized = $updated.Replace("`r`n", "`n")
if (!$normalized.Contains($anchor)) { throw 'El codigo cambio: no se encuentra el ancla de candidatos. No se modifico nada.' }
$insertion = @'
        // Comparte las reglas RBAC, equipos y cobertura con el motor de asignacion.
        var eligibleIds = (await EligibleTechnicians(organizationId)
                .ToListAsync(cancellationToken))
            .ToHashSet();

        var routingTeams = await LoadRoutingTeamsAsync(
            organizationId,
            NormalizeRoutingCategory(ticket.Category),
            ticket.RequestedTeamId,
            cancellationToken);

        var validCoverages = routing.SiteId.HasValue
            ? await LoadRankedCoveragesAsync(
                organizationId,
                routing.SiteId.Value,
                routing.SiteLocationId,
                NormalizeRoutingCategory(ticket.Category),
                routingTeams.Keys.ToArray(),
                cancellationToken)
            : [];

        var coveredTeamIds = validCoverages
            .Select(x => x.Coverage.TeamId)
            .ToHashSet();

'@
$updated = $normalized.Replace($anchor, $insertion + $anchor)
$startMarker = '        foreach (var team in teams)'
$endMarker = '        candidates =' + "`n"
$start = $updated.IndexOf($startMarker)
$end = $updated.IndexOf($endMarker, $start + $startMarker.Length)
if ($start -lt 0 -or $end -lt 0) { throw 'La estructura del diagnostico cambio. No se modifico nada.' }
$replacement = @'
        foreach (var team in teams)
        {
            var teamMembers = members.Where(x => x.TeamId == team.Id).ToList();

            foreach (var member in teamMembers)
            {
                var user = users.FirstOrDefault(x => x.Id == member.UserId);
                if (user is null)
                {
                    continue;
                }

                var userSchedules = schedules
                    .Where(x => x.TeamId == team.Id && x.UserId == member.UserId)
                    .ToList();

                var hasSchedule = userSchedules.Count > 0;
                // No schedule: membership availability governs.
                // With schedule: a slot must be active.
                var onDutyNow = !hasSchedule || userSchedules.Any(x => x.IsOnDuty(now));
                var openTickets = loadMap.GetValueOrDefault(member.UserId);
                var reasons = new List<string>();

                if (!routingTeams.ContainsKey(team.Id))
                    reasons.Add("Grupo incompatible, inactivo o no solicitado");

                if (!coveredTeamIds.Contains(team.Id))
                    reasons.Add("Grupo sin cobertura para localidad y categoria");

                if (!eligibleIds.Contains(member.UserId))
                    reasons.Add("Tecnico sin permisos RBAC de Helpdesk");

                if (!member.IsAvailable)
                    reasons.Add("Miembro no disponible");

                if (!member.AcceptsAutomaticAssignments)
                    reasons.Add("No acepta autoasignacion");

                if (member.MaxOpenTickets <= 0)
                    reasons.Add("Capacidad no configurada");
                else if (openTickets >= member.MaxOpenTickets)
                    reasons.Add("Capacidad agotada");

                if (!onDutyNow)
                    reasons.Add("Fuera de turno");

                if (ticket.AssigneeUserId.HasValue)
                    reasons.Add("Ticket ya asignado");

                if (ticket.Status is "pendinguser" or "closed" or "resolved")
                    reasons.Add("Estado incompatible con autoasignacion");

                var canAssign = reasons.Count == 0;

                candidates.Add(new HelpdeskRoutingCandidatePreview(
                    team.Id,
                    team.Name,
                    member.UserId,
                    $"{user.FirstName} {user.LastName}".Trim(),
                    user.Email,
                    member.IsAvailable,
                    member.AcceptsAutomaticAssignments,
                    member.MaxOpenTickets,
                    openTickets,
                    hasSchedule,
                    onDutyNow,
                    canAssign ? "assignable" : "blocked",
                    canAssign ? "Candidato operativo" : string.Join("; ", reasons)));
            }
        }

'@
$updated = $updated.Substring(0, $start) + $replacement + $updated.Substring($end)
if ($original.Contains("`r`n")) { $updated = $updated.Replace("`n", "`r`n") }
if ($updated -eq $original) { throw 'Ninguna modificacion aplicada.' }
$backup = "$file.hd-f8.bak"
Copy-Item -LiteralPath $file -Destination $backup -Force
[System.IO.File]::WriteAllText((Resolve-Path $file).Path, $updated, [System.Text.UTF8Encoding]::new($false))
Write-Host "Diagnostico actualizado. Backup: $backup" -ForegroundColor Green
Write-Host 'Revisar diff y compilar antes de commit:' -ForegroundColor Cyan
git diff -- $file
git diff --check
if ($LASTEXITCODE -ne 0) { throw 'git diff --check detecto errores; revise antes del commit.' }
Write-Host 'Ahora ejecute dotnet build sobre la solucion y pruebe el diagnostico.'
