#Requires -Version 5.1
<#
.SYNOPSIS
    Sincroniza ITAM MongoDB (cisa_itam, app anterior) -> SQL Server (cisa_ti_itam).

.DESCRIPTION
    Mientras la aplicacion anterior (C:\inventario Cedi) siga registrando
    entregas/devoluciones en MongoDB, ejecute este script (manual o con una
    tarea programada) para que TitanMDM vea el 100% de los datos.

    - Por defecto SIMULA y muestra que cambiaria.
    - Con -Apply: respaldo COPY_ONLY de cisa_ti_itam, luego sincroniza
      (sin borrados) y deja respaldo JSON de filas modificadas.

    Tras el corte definitivo (app anterior apagada) deje de ejecutarlo.

.EXAMPLE
    .\scripts\dev\Sync-ItamFromMongo.ps1
    .\scripts\dev\Sync-ItamFromMongo.ps1 -Apply
#>
[CmdletBinding()]
param(
    [switch]$Apply,
    [string]$Python = "C:\inventario Cedi\backend\venv\Scripts\python.exe",
    [string]$SqlServer = "localhost\SQLEXPRESS"
)

$ErrorActionPreference = "Stop"
$root = [IO.Path]::GetFullPath((Join-Path $PSScriptRoot "..\.."))
$script = Join-Path $root "db\sync_inventory_from_mongo.py"

if (-not (Test-Path $Python)) {
    throw "Python con pymongo + pyodbc no encontrado: $Python (use -Python)."
}

if ($Apply) {
    $stamp = Get-Date -Format "yyyyMMdd-HHmmss"
    $query = "DECLARE @f nvarchar(4000) = CAST(SERVERPROPERTY('InstanceDefaultBackupPath') AS nvarchar(4000)) + '\cisa_ti_itam-pre-sync-$stamp.bak'; BACKUP DATABASE [cisa_ti_itam] TO DISK = @f WITH COPY_ONLY, INIT, CHECKSUM; SELECT @f;"
    & sqlcmd -S $SqlServer -E -C -W -b -Q $query
    if ($LASTEXITCODE -ne 0) { throw "El respaldo de cisa_ti_itam fallo; no se sincronizo." }
    & $Python -I $script --apply
}
else {
    & $Python -I $script
}

if ($LASTEXITCODE -ne 0) { throw "La sincronizacion termino con error ($LASTEXITCODE)." }
