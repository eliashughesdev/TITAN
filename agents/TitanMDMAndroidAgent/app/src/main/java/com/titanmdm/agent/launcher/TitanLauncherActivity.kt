package com.titanmdm.agent.launcher

import android.app.ActivityManager
import android.app.admin.DevicePolicyManager
import android.content.BroadcastReceiver
import android.content.ComponentName
import android.content.Context
import android.content.Intent
import android.content.IntentFilter
import android.content.RestrictionsManager
import android.content.pm.PackageManager
import android.graphics.drawable.BitmapDrawable
import android.os.Bundle
import android.graphics.BitmapFactory
import androidx.core.net.toUri
import android.widget.Toast
import androidx.activity.ComponentActivity
import androidx.activity.compose.setContent
import androidx.compose.runtime.getValue
import androidx.compose.runtime.mutableStateOf
import androidx.compose.runtime.setValue
import androidx.compose.ui.graphics.ImageBitmap
import androidx.compose.ui.graphics.asImageBitmap
import androidx.lifecycle.lifecycleScope
import kotlinx.coroutines.Dispatchers
import kotlinx.coroutines.Job
import kotlinx.coroutines.launch
import kotlinx.coroutines.withContext
import androidx.core.content.ContextCompat
import androidx.core.graphics.drawable.toBitmap
import com.titanmdm.agent.BuildConfig
import com.titanmdm.agent.apps.install.RequiredAppsReconciler
import com.titanmdm.agent.apps.install.TitanServerAppStore
import com.titanmdm.agent.kiosk.KioskController
import com.titanmdm.agent.service.AgentForegroundService
import com.titanmdm.agent.ui.theme.TitanMDMAndroidAgentTheme
import java.io.File

/**
 * Punto de entrada HOME del Titan Launcher.
 *
 * Relee la política administrada sin reiniciar el dispositivo mediante:
 *  - ACTION_APPLICATION_RESTRICTIONS_CHANGED (cambios de managed config);
 *  - TitanServerLauncherStore.ACTION_CHANGED (política del servidor TitanMDM);
 *  - PACKAGE_ADDED / PACKAGE_REMOVED / PACKAGE_CHANGED / PACKAGE_REPLACED.
 *
 * Nunca abre aplicaciones fuera de la lista autorizada ni concede permisos.
 */
class TitanLauncherActivity : ComponentActivity() {

    private var policy by mutableStateOf(TitanLauncherPolicy())
    private var homeStatus by mutableStateOf(TitanLauncherHomeStatus.None)
    private var appStates by mutableStateOf<Map<String, TitanLauncherAppState>>(emptyMap())
    private var wallpaper by mutableStateOf<ImageBitmap?>(null)
    private var logo by mutableStateOf<ImageBitmap?>(null)
    private var syncStatus by mutableStateOf("Sin sincronizar")
    private var resourceJob: Job? = null
    private var installJob: Job? = null
    private var autoInstallJob: Job? = null
    private var policyOrigin = TitanLauncherPolicyOrigin.None
    private var deviceStatus by mutableStateOf(TitanDeviceStatus())

    private val deviceStatusReceiver =
        object : BroadcastReceiver() {
            override fun onReceive(context: Context?, intent: Intent?) {
                deviceStatus = TitanDeviceStatus.read(this@TitanLauncherActivity)
            }
        }

    private val restrictionsReceiver =
        object : BroadcastReceiver() {
            override fun onReceive(
                context: Context?,
                intent: Intent?
            ) {
                when (intent?.action) {
                    Intent.ACTION_APPLICATION_RESTRICTIONS_CHANGED,
                    TitanServerLauncherStore.ACTION_CHANGED -> {
                        // Nueva política: bloqueos y apps permitidas.
                        runCatching { KioskController.apply(this@TitanLauncherActivity) }
                        refresh()
                        autoInstallRequiredApps()
                        syncLockTask()
                    }
                }
            }
        }

    private val packageReceiver =
        object : BroadcastReceiver() {
            override fun onReceive(
                context: Context?,
                intent: Intent?
            ) {
                when (intent?.action) {
                    Intent.ACTION_PACKAGE_ADDED,
                    Intent.ACTION_PACKAGE_REMOVED,
                    Intent.ACTION_PACKAGE_CHANGED,
                    Intent.ACTION_PACKAGE_REPLACED -> refresh()
                }
            }
        }

    override fun onCreate(savedInstanceState: Bundle?) {
        super.onCreate(savedInstanceState)

        refresh()

        setContent {
            TitanMDMAndroidAgentTheme {
                TitanLauncherScreen(
                    policy = policy,
                    homeStatus = homeStatus,
                    appState = { appStates[it] ?: TitanLauncherAppState.Unavailable },
                    iconProvider = ::loadIcon,
                    onLaunch = ::launchApp,
                    onInstall = ::requestInstall,
                    wallpaper = wallpaper,
                    logo = logo,
                    syncStatus = syncStatus,
                    deviceStatus = deviceStatus
                )
            }
        }
    }

    override fun onStart() {
        super.onStart()

        ContextCompat.registerReceiver(
            this,
            restrictionsReceiver,
            IntentFilter().apply {
                addAction(Intent.ACTION_APPLICATION_RESTRICTIONS_CHANGED)
                addAction(TitanServerLauncherStore.ACTION_CHANGED)
            },
            ContextCompat.RECEIVER_NOT_EXPORTED
        )

        val packageFilter =
            IntentFilter().apply {
                addAction(Intent.ACTION_PACKAGE_ADDED)
                addAction(Intent.ACTION_PACKAGE_REMOVED)
                addAction(Intent.ACTION_PACKAGE_CHANGED)
                addAction(Intent.ACTION_PACKAGE_REPLACED)
                addDataScheme("package")
            }

        ContextCompat.registerReceiver(
            this,
            packageReceiver,
            packageFilter,
            ContextCompat.RECEIVER_NOT_EXPORTED
        )

        ContextCompat.registerReceiver(
            this,
            deviceStatusReceiver,
            IntentFilter().apply {
                addAction(Intent.ACTION_BATTERY_CHANGED)
                addAction(Intent.ACTION_POWER_CONNECTED)
                addAction(Intent.ACTION_POWER_DISCONNECTED)
            },
            ContextCompat.RECEIVER_NOT_EXPORTED
        )

        // Kiosco: con Device Owner fija el HOME y bloquea la barra de estado.
        runCatching { KioskController.apply(this) }

        AgentForegroundService.start(this)

        refresh()
        autoInstallRequiredApps()
    }

    override fun onResume() {
        super.onResume()
        syncLockTask()
    }

    /**
     * Teclas del sistema (Recientes / salida al escritorio): con la
     * política "bloquear teclas" y Device Owner, el launcher entra en
     * lock task; sale cuando la política o el técnico lo liberan.
     */
    private fun syncLockTask() {
        val activityManager = getSystemService(ActivityManager::class.java) ?: return
        val locked = activityManager.lockTaskModeState != ActivityManager.LOCK_TASK_MODE_NONE
        val wanted = runCatching { KioskController.shouldLockTask(this) }.getOrDefault(false)

        runCatching {
            when {
                wanted && !locked -> startLockTask()
                !wanted && locked -> stopLockTask()
            }
        }
    }

    override fun onStop() {
        runCatching { unregisterReceiver(restrictionsReceiver) }
        runCatching { unregisterReceiver(packageReceiver) }
        runCatching { unregisterReceiver(deviceStatusReceiver) }
        super.onStop()
    }

    private fun refresh() {
        val officialRestrictions = readOfficialRestrictions()

        /*
         * El perfil local de laboratorio sólo se lee en builds debug.
         * En release `labEnabled` es falso y nunca se toca el archivo.
         */
        val labEnabled = BuildConfig.DEBUG

        val labProfileJson =
            if (labEnabled) readLabProfile() else null

        val deviceManaged = isManagedByDevicePolicy()

        val resolution =
            TitanLauncherPolicyResolver.resolve(
                officialRestrictions = officialRestrictions,
                labProfileJson = labProfileJson,
                labEnabled = labEnabled,
                deviceManaged = deviceManaged,
                serverRestrictions = TitanServerLauncherStore.read(this)
            )

        val newPolicy = resolution.policy
        policyOrigin = resolution.origin
        policy = newPolicy

        if (newPolicy.state != TitanLauncherPolicyState.Valid) {
            wallpaper = null
            logo = null
        }

        val acknowledgement = TitanLauncherAcknowledgementStore.fromPolicy(
            policy = newPolicy,
            origin = resolution.origin,
            resourceFailed = !newPolicy.resourceError.isNullOrBlank()
        )
        TitanLauncherAcknowledgementStore.write(filesDir, acknowledgement)
        syncStatus = syncLabel(acknowledgement, newPolicy)
        loadCorporateResources(newPolicy, resolution.origin)

        appStates = newPolicy.applications.associate {
            it.packageName to resolveAppState(it.packageName)
        }

        homeStatus = readHomeStatus()
        deviceStatus = TitanDeviceStatus.read(this)
    }

    /**
     * Restricciones administradas reales publicadas por Android Device
     * Policy a través de `RestrictionsManager`.
     */
    private fun readOfficialRestrictions(): Map<String, String> {
        val manager =
            getSystemService(Context.RESTRICTIONS_SERVICE)
                as? RestrictionsManager
                ?: return emptyMap()

        val bundle = manager.applicationRestrictions ?: return emptyMap()

        val result = mutableMapOf<String, String>()
        for (key in bundle.keySet()) {
            // Restricciones heterogeneas (String/Boolean/Int): Bundle.get(key)
            // no tiene reemplazo generico; solo se usa toString().
            @Suppress("DEPRECATION")
            val value = bundle.get(key) ?: continue
            result[key] = value.toString()
        }
        return result
    }

    /**
     * Perfil de laboratorio local (sólo debug).
     *
     * Prioriza el archivo persistente en almacenamiento interno
     * (`filesDir/titan_lab_policy.json`), que sobrevive al reinicio del
     * emulador, y recurre al activo empaquetado en `src/debug/assets`
     * como perfil inicial.
     */
    private fun readLabProfile(): String? {
        val file = File(
            filesDir,
            TitanLauncherPolicyResolver.LAB_PROFILE_FILE_NAME
        )

        if (file.isFile) {
            return runCatching { file.readText() }.getOrNull()
        }

        return runCatching {
            assets.open(TitanLauncherPolicyResolver.LAB_PROFILE_FILE_NAME)
                .bufferedReader()
                .use { it.readText() }
        }.getOrNull()
    }

    /**
     * Verdadero cuando Titan es la app de administración (device/profile
     * owner). En ese caso el modo laboratorio nunca se activa.
     */
    private fun isManagedByDevicePolicy(): Boolean {
        val manager =
            getSystemService(Context.DEVICE_POLICY_SERVICE)
                as? DevicePolicyManager
                ?: return false

        return try {
            manager.isDeviceOwnerApp(packageName) ||
                manager.isProfileOwnerApp(packageName)
        } catch (_: Exception) {
            false
        }
    }

    private fun resolveAppState(packageName: String): TitanLauncherAppState {
        val applicationInfo = try {
            packageManager.getApplicationInfo(
                packageName,
                PackageManager.MATCH_DISABLED_COMPONENTS
            )
        } catch (_: PackageManager.NameNotFoundException) {
            return TitanLauncherAppState.NotInstalled
        } catch (_: Exception) {
            return TitanLauncherAppState.Unavailable
        }

        if (!applicationInfo.enabled) {
            return TitanLauncherAppState.Disabled
        }

        return try {
            if (packageManager.getLaunchIntentForPackage(packageName) == null) {
                TitanLauncherAppState.NotLaunchable
            } else {
                TitanLauncherAppState.Launchable
            }
        } catch (_: Exception) {
            TitanLauncherAppState.Unavailable
        }
    }

    private fun loadIcon(packageName: String): ImageBitmap? {
        return try {
            val info = packageManager.getApplicationInfo(packageName, 0)
            val drawable = info.loadIcon(packageManager)

            val bitmap = if (drawable is BitmapDrawable && drawable.bitmap != null) {
                drawable.bitmap
            } else {
                drawable.toBitmap(width = 108, height = 108)
            }

            bitmap.asImageBitmap()
        } catch (_: Exception) {
            null
        }
    }

    private fun launchApp(packageName: String) {
        // Doble verificación: nunca abrir una app fuera de la lista autorizada.
        if (policy.applications.none { it.packageName == packageName }) {
            return
        }

        if (resolveAppState(packageName) != TitanLauncherAppState.Launchable) {
            refresh()
            return
        }

        val intent = try {
            packageManager.getLaunchIntentForPackage(packageName) ?: return
        } catch (_: Exception) {
            refresh()
            return
        }

        try {
            startActivity(intent)
        } catch (_: Exception) {
            refresh()
        }
    }

    /**
     * App autorizada pero no instalada: instala en silencio el APK de la
     * política si el agente es Device Owner. Si no hay APK en el
     * repositorio, abre la ficha de la tienda como respaldo.
     */
    private fun requestInstall(packageName: String) {
        if (policy.applications.none { it.packageName == packageName }) return

        val app = TitanServerAppStore.read(this)
            .firstOrNull { it.packageName == packageName && it.available }

        if (app != null) {
            installJob?.cancel()
            installJob = lifecycleScope.launch {
                Toast.makeText(
                    this@TitanLauncherActivity,
                    "Instalando $packageName...",
                    Toast.LENGTH_SHORT
                ).show()

                val result = RequiredAppsReconciler.installOne(this@TitanLauncherActivity, packageName)
                refresh()

                val message = if (result?.success == true) {
                    "$packageName instalada correctamente."
                } else {
                    "No se pudo instalar $packageName: " +
                        (result?.message ?: result?.code ?: "error desconocido")
                }
                Toast.makeText(this@TitanLauncherActivity, message, Toast.LENGTH_LONG).show()
            }
            return
        }

        val intents = listOf(
            Intent(Intent.ACTION_VIEW, "market://details?id=$packageName".toUri()),
            Intent(
                Intent.ACTION_VIEW,
                "https://play.google.com/store/apps/details?id=$packageName".toUri()
            )
        )

        for (intent in intents) {
            try {
                startActivity(intent.addFlags(Intent.FLAG_ACTIVITY_NEW_TASK))
                return
            } catch (_: Exception) {
                // Siguiente alternativa.
            }
        }

        Toast.makeText(
            this,
            "Solicite a TI la instalación de $packageName.",
            Toast.LENGTH_LONG
        ).show()
    }

    /** Apps obligatorias pendientes: mismo reintento que el servicio, sin esperar al ciclo. */
    private fun autoInstallRequiredApps() {
        if (RequiredAppsReconciler.pending(this).isEmpty()) return

        autoInstallJob?.cancel()
        autoInstallJob = lifecycleScope.launch {
            RequiredAppsReconciler.reconcile(this@TitanLauncherActivity)
            refresh()
        }
    }

    private fun readHomeStatus(): TitanLauncherHomeStatus {
        val homeIntent = Intent(Intent.ACTION_MAIN).addCategory(Intent.CATEGORY_HOME)
        val resolved = try {
            packageManager.resolveActivity(homeIntent, PackageManager.MATCH_DEFAULT_ONLY)
        } catch (_: Exception) {
            null
        }
        val activityInfo = resolved?.activityInfo
        val resolvedComponent = activityInfo?.let {
            ComponentName(it.packageName, it.name)
        }
        val launcherComponent = ComponentName(this, TitanLauncherActivity::class.java)

        return TitanLauncherHomeStatus(
            isDefaultHome = resolvedComponent == launcherComponent
        )
    }

    private fun loadCorporateResources(
        current: TitanLauncherPolicy,
        origin: TitanLauncherPolicyOrigin
    ) {
        resourceJob?.cancel()
        if (current.state != TitanLauncherPolicyState.Valid) {
            return
        }

        resourceJob = lifecycleScope.launch {
            val cache = TitanLauncherResourceCache(File(filesDir, "launcher-cache"))
            val fetcher = TitanLauncherResourceFetcher(
                TitanLauncherOkHttpTransport(),
                cache
            )
            val wallpaperResult = current.wallpaperUrl?.let { url ->
                withContext(Dispatchers.IO) {
                    fetcher.fetch(
                        url,
                        "wallpaper",
                        TitanLauncherImageSignatures.MAX_WALLPAPER_BYTES
                    )
                }
            }
            val logoResult = current.logoUrl?.let { url ->
                withContext(Dispatchers.IO) {
                    fetcher.fetch(
                        url,
                        "logo",
                        TitanLauncherImageSignatures.MAX_LOGO_BYTES
                    )
                }
            }

            if (policy.state != TitanLauncherPolicyState.Valid ||
                policy.wallpaperUrl != current.wallpaperUrl ||
                policy.logoUrl != current.logoUrl ||
                policyOrigin != origin
            ) {
                return@launch
            }

            wallpaper = wallpaperResult?.bytes?.let(::decodeResource)
            logo = logoResult?.bytes?.let(::decodeResource)
            val failed = wallpaperResult?.error != null || logoResult?.error != null
            val acknowledgement = TitanLauncherAcknowledgementStore.fromPolicy(
                policy = current,
                origin = origin,
                resourceFailed = failed || !current.resourceError.isNullOrBlank()
            )
            TitanLauncherAcknowledgementStore.write(filesDir, acknowledgement)
            syncStatus = syncLabel(acknowledgement, current)
        }
    }

    private fun decodeResource(bytes: ByteArray): ImageBitmap? {
        if (TitanLauncherImageSignatures.detect(bytes) == null) return null
        return BitmapFactory.decodeByteArray(bytes, 0, bytes.size)?.asImageBitmap()
    }

    private fun syncLabel(
        acknowledgement: com.titanmdm.agent.launcher.TitanLauncherAcknowledgement,
        current: TitanLauncherPolicy
    ): String {
        return when (acknowledgement.syncStatus) {
            "Applied" -> "Aplicado v${current.configVersion ?: "-"}"
            "ResourceError" -> "Aplicado con error de recurso"
            "Revoked" -> "Configuración revocada"
            "Invalid" -> "Configuración no válida"
            "Absent" -> if (current.isLabProfile) "Laboratorio" else "Sin política administrada"
            else -> "Sin sincronizar"
        }
    }
}
