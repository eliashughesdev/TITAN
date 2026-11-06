package com.titanmdm.agent.kiosk

import android.Manifest
import android.app.admin.DevicePolicyManager
import android.app.role.RoleManager
import android.content.ComponentName
import android.content.Context
import android.content.Intent
import android.content.IntentFilter
import android.content.pm.PackageManager
import android.os.Build
import android.provider.Settings
import android.util.Log
import androidx.core.content.ContextCompat
import androidx.core.content.edit
import com.titanmdm.agent.admin.TitanDeviceAdminReceiver
import com.titanmdm.agent.launcher.TitanLauncherActivity

/**
 * Modo kiosco on-premise del Titan Launcher (sin Android Enterprise).
 *
 * Los bloqueos (HOME fijo, barra de estado, teclas del sistema) se
 * configuran en la política web del launcher ([KioskSettings]).
 * Android solo permite aplicarlos a la app Device Owner (DevicePolicyManager). Sin Device Owner, el agente
 * solicita el rol HOME al usuario y no puede bloquear la barra: no se
 * usan superposiciones ni servicios de accesibilidad, que Android 12+
 * neutraliza y que no son un control fiable.
 *
 * Device Owner sin Google (equipo sin cuentas):
 *   adb shell dpm set-device-owner com.titanmdm.agent/.admin.TitanDeviceAdminReceiver
 */
object KioskController {

    private const val TAG = "TitanKiosk"
    private const val PREFS = "titan_kiosk"
    private const val KEY_ENABLED = "kiosk_enabled"

    data class Status(
        val deviceOwner: Boolean,
        val adminActive: Boolean,
        val kioskEnabled: Boolean,
        val defaultHome: Boolean,
        val statusBarLocked: Boolean,
        val systemKeysLocked: Boolean,
        val locationGranted: Boolean,
        val backgroundLocationGranted: Boolean
    )

    /** Bloqueos efectivos: política web + liberación del técnico (KIOSK_MODE). */
    private data class Effective(
        val home: Boolean,
        val statusBar: Boolean,
        val lockTask: Boolean,
        val packages: List<String>
    )

    private fun effective(context: Context, owner: Boolean): Effective {
        val settings = KioskSettings.read(context)
        val on = owner && isKioskEnabled(context) && settings.launcherActive
        return Effective(
            home = on && settings.persistentHome,
            statusBar = on && settings.lockStatusBar,
            lockTask = on && settings.lockSystemKeys,
            packages = settings.allowedPackages
        )
    }

    /** El launcher entra/sale de lock task según esto (teclas Home/Recientes). */
    fun shouldLockTask(context: Context): Boolean {
        val app = context.applicationContext
        val dpm = app.getSystemService(DevicePolicyManager::class.java) ?: return false
        val owner = runCatching { dpm.isDeviceOwnerApp(app.packageName) }.getOrDefault(false)
        return owner &&
            isKioskEnabled(app) &&
            dpm.isLockTaskPermitted(app.packageName)
    }

    fun isKioskEnabled(context: Context): Boolean =
        context.getSharedPreferences(PREFS, Context.MODE_PRIVATE).getBoolean(KEY_ENABLED, true)

    /** Comando KIOSK_MODE: el técnico libera o vuelve a bloquear el equipo. */
    fun setKioskEnabled(context: Context, enabled: Boolean): Status {
        context.getSharedPreferences(PREFS, Context.MODE_PRIVATE).edit {
            putBoolean(KEY_ENABLED, enabled)
        }
        return apply(context)
    }

    /**
     * Aplica el estado de kiosco. Idempotente: se llama tras la
     * inscripción, al arrancar el equipo y al abrir el launcher.
     */
    fun apply(context: Context): Status {
        val app = context.applicationContext
        val dpm = app.getSystemService(DevicePolicyManager::class.java)
        val admin = TitanDeviceAdminReceiver.component(app)

        if (dpm != null && dpm.isDeviceOwnerApp(app.packageName)) {
            runCatching { grantLocationPermissions(dpm, admin, app.packageName) }
                .onFailure { Log.w(TAG, "No fue posible conceder permisos de ubicación.", it) }

            val wanted = effective(app, owner = true)
            runCatching { applyHome(dpm, admin, app, wanted.home) }
                .onFailure { Log.w(TAG, "No fue posible fijar el HOME.", it) }
            runCatching { dpm.setStatusBarDisabled(admin, wanted.statusBar) }
                .onFailure { Log.w(TAG, "No fue posible cambiar la barra de estado.", it) }
            runCatching {
                applyLockTask(
                    dpm = dpm,
                    admin = admin,
                    packageName = app.packageName,
                    wanted = wanted,
                    kioskEnabled = isKioskEnabled(app)
                )
            }.onFailure { Log.w(TAG, "No fue posible configurar lock task.", it) }
        }

        return status(app)
    }

    fun status(context: Context): Status {
        val app = context.applicationContext
        val dpm = app.getSystemService(DevicePolicyManager::class.java)
        val admin = TitanDeviceAdminReceiver.component(app)
        val owner = runCatching { dpm?.isDeviceOwnerApp(app.packageName) == true }.getOrDefault(false)
        val wanted = effective(app, owner)

        return Status(
            deviceOwner = owner,
            adminActive = runCatching { dpm?.isAdminActive(admin) == true }.getOrDefault(false),
            kioskEnabled = isKioskEnabled(app),
            defaultHome = isDefaultHome(app),
            statusBarLocked = wanted.statusBar,
            systemKeysLocked = wanted.lockTask,
            locationGranted = granted(app, Manifest.permission.ACCESS_FINE_LOCATION) ||
                granted(app, Manifest.permission.ACCESS_COARSE_LOCATION),
            backgroundLocationGranted = Build.VERSION.SDK_INT < Build.VERSION_CODES.Q ||
                granted(app, Manifest.permission.ACCESS_BACKGROUND_LOCATION)
        )
    }

    /** LOCK_DEVICE: requiere administrador activo (o Device Owner). */
    fun lockNow(context: Context): Boolean {
        val app = context.applicationContext
        val dpm = app.getSystemService(DevicePolicyManager::class.java) ?: return false
        if (!dpm.isAdminActive(TitanDeviceAdminReceiver.component(app))) return false
        dpm.lockNow()
        return true
    }

    fun isDefaultHome(context: Context): Boolean {
        val intent = Intent(Intent.ACTION_MAIN).addCategory(Intent.CATEGORY_HOME)
        val resolved = context.packageManager
            .resolveActivity(intent, PackageManager.MATCH_DEFAULT_ONLY)
        return resolved?.activityInfo?.packageName == context.packageName
    }

    /** Sin Device Owner: el usuario debe aceptar Titan Launcher como inicio. */
    fun requestHomeIntent(context: Context): Intent {
        if (Build.VERSION.SDK_INT >= Build.VERSION_CODES.Q) {
            val roles = context.getSystemService(RoleManager::class.java)
            if (roles != null &&
                roles.isRoleAvailable(RoleManager.ROLE_HOME) &&
                !roles.isRoleHeld(RoleManager.ROLE_HOME)
            ) {
                return roles.createRequestRoleIntent(RoleManager.ROLE_HOME)
            }
        }
        return Intent(Settings.ACTION_HOME_SETTINGS)
    }

    fun adminActivationIntent(context: Context): Intent =
        Intent(DevicePolicyManager.ACTION_ADD_DEVICE_ADMIN)
            .putExtra(DevicePolicyManager.EXTRA_DEVICE_ADMIN, TitanDeviceAdminReceiver.component(context))
            .putExtra(
                DevicePolicyManager.EXTRA_ADD_EXPLANATION,
                "TitanMDM necesita bloquear la pantalla cuando el equipo sale de su geocerca o lo ordena el administrador."
            )

    private fun applyHome(
        dpm: DevicePolicyManager,
        admin: ComponentName,
        context: Context,
        enabled: Boolean
    ) {
        dpm.clearPackagePersistentPreferredActivities(admin, context.packageName)
        if (!enabled) return

        val filter = IntentFilter(Intent.ACTION_MAIN).apply {
            addCategory(Intent.CATEGORY_HOME)
            addCategory(Intent.CATEGORY_DEFAULT)
        }
        dpm.addPersistentPreferredActivity(
            admin,
            filter,
            ComponentName(context, TitanLauncherActivity::class.java)
        )
    }

    /**
     * Lock task: bloquea Recientes y la salida al sistema. Solo se
     * permiten el launcher y las apps autorizadas; Home vuelve al
     * launcher y se conserva el menú de apagado.
     */
    private fun applyLockTask(
        dpm: DevicePolicyManager,
        admin: ComponentName,
        packageName: String,
        wanted: Effective,
        kioskEnabled: Boolean
    ) {
        val allowedPackages =
            if (kioskEnabled) {
                (listOf(packageName) + wanted.packages).distinct().toTypedArray()
            } else {
                emptyArray<String>()
            }

        dpm.setLockTaskPackages(admin, allowedPackages)

        if (wanted.lockTask && Build.VERSION.SDK_INT >= Build.VERSION_CODES.P) {
            dpm.setLockTaskFeatures(
                admin,
                DevicePolicyManager.LOCK_TASK_FEATURE_HOME or
                    DevicePolicyManager.LOCK_TASK_FEATURE_GLOBAL_ACTIONS or
                    DevicePolicyManager.LOCK_TASK_FEATURE_KEYGUARD
            )
        }
    }

    private fun grantLocationPermissions(
        dpm: DevicePolicyManager,
        admin: ComponentName,
        packageName: String
    ) {
        val permissions = buildList {
            add(Manifest.permission.ACCESS_FINE_LOCATION)
            add(Manifest.permission.ACCESS_COARSE_LOCATION)
            if (Build.VERSION.SDK_INT >= Build.VERSION_CODES.Q) {
                add(Manifest.permission.ACCESS_BACKGROUND_LOCATION)
            }
            if (Build.VERSION.SDK_INT >= Build.VERSION_CODES.TIRAMISU) {
                add(Manifest.permission.POST_NOTIFICATIONS)
            }
        }
        for (permission in permissions) {
            dpm.setPermissionGrantState(
                admin,
                packageName,
                permission,
                DevicePolicyManager.PERMISSION_GRANT_STATE_GRANTED
            )
        }
    }

    private fun granted(context: Context, permission: String): Boolean =
        ContextCompat.checkSelfPermission(context, permission) == PackageManager.PERMISSION_GRANTED
}
