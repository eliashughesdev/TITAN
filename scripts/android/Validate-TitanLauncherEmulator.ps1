param(
    [string]$Avd = "Titan_API_33"
)

Write-Host "F3-B launcher emulator checklist. Lab JSON is not Android Device Policy."
$adb = Get-Command adb -ErrorAction SilentlyContinue
if (-not $adb) {
    Write-Host "adb no esta en PATH. Instale platform-tools antes de validar el emulador."
    exit 0
}

& adb devices
Write-Host "AVD esperado: $Avd (Android 13 API 33)."
Write-Host "1. Perfil inicial sin restricciones."
Write-Host "2. Laboratorio debug: files/titan_lab_policy.json."
Write-Host "3. Cambiar fondo, logotipo, columnas y apps."
Write-Host "4. Reiniciar TitanLauncherActivity y revisar files/launcher-cache."
Write-Host "5. URL insegura y perfil revocado (enabled=false)."
Write-Host "No marcar RestrictionsManager como verificado si solo se uso el JSON local."
