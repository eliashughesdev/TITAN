# Titan Launcher — Perfil de Laboratorio Local

## Uso con emulador (Android Studio)

1. Inicie el emulador Android 13 (API 33+ recomendado).
2. Instale el APK debug:
   `powershell
   adb install -r "C:\TitanMDM\agents\TitanMDMAndroidAgent\app\build\outputs\apk\debug\app-debug.apk"
   `
3. Establezca Titan como launcher predeterminado:
   `powershell
   adb shell cmd package set-home-activity com.titanmdm.agent/.launcher.TitanLauncherActivity
   `
4. Cargue el perfil de laboratorio (opción A: archivo persistente):
   `powershell
   adb shell run-as com.titanmdm.agent mkdir -p /data/data/com.titanmdm.agent/files
   adb push "C:\TitanMDM\agents\TitanMDMAndroidAgent\lab-profile\titan_lab_policy.json" /data/local/tmp/titan_lab_policy.json
   adb shell run-as com.titanmdm.agent cp /data/local/tmp/titan_lab_policy.json /data/data/com.titanmdm.agent/files/titan_lab_policy.json
   adb shell run-as com.titanmdm.agent chmod 600 /data/data/com.titanmdm.agent/files/titan_lab_policy.json
   `
5. Abra la app (o presione Home) y verifique:
   - Aparece Modo laboratorio — política local en la cabecera.
   - El chip indica Modo laboratorio.
   - Se muestran Microsoft Teams, Microsoft Outlook y Google Chrome (con su estado según estén instalados).

6. Alternativa B: reiniciar la app para recargar (el archivo persiste tras reiniciar el emulador).

## Notas
- El modo laboratorio **no** se activa si el dispositivo está gestionado (device owner/profile owner).
- En builds release, el proveedor local está completamente inhabilitado.
- Prioridad: restricciones administradas reales siempre sobrescriben el perfil local.
- Si el JSON de laboratorio es inválido, la pantalla mostrará el error correspondiente.
