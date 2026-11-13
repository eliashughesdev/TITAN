package com.titanmdm.agent.apps.install

import android.app.PendingIntent
import android.app.admin.DevicePolicyManager
import android.content.BroadcastReceiver
import android.content.Context
import android.content.Intent
import android.content.IntentFilter
import android.content.pm.PackageInstaller
import android.content.pm.PackageManager
import android.os.Build
import android.provider.Settings
import androidx.core.content.ContextCompat
import kotlinx.coroutines.CompletableDeferred
import kotlinx.coroutines.Dispatchers
import kotlinx.coroutines.withContext
import kotlinx.coroutines.withTimeoutOrNull
import java.io.File

/**
 * Instalación con PackageInstaller. Siendo Device Owner, Android instala
 * sin pedir confirmación al usuario (aprovisionamiento por QR). Sin Device
 * Owner la sesión queda en STATUS_PENDING_USER_ACTION y se informa.
 */
class SilentPackageInstaller(context: Context) {

    private val context = context.applicationContext

    sealed interface Outcome {
        data object Installed : Outcome
        data class Failed(val code: String, val message: String) : Outcome
    }

    data class ArchiveInfo(val packageName: String, val versionCode: Long)

    /** Paquete y versión del APK descargado (antes de instalar). */
    fun readArchive(apk: File): ArchiveInfo? {
        val info = if (Build.VERSION.SDK_INT >= Build.VERSION_CODES.TIRAMISU) {
            context.packageManager.getPackageArchiveInfo(apk.absolutePath, PackageManager.PackageInfoFlags.of(0))
        } else {
            @Suppress("DEPRECATION")
            context.packageManager.getPackageArchiveInfo(apk.absolutePath, 0)
        } ?: return null

        val versionCode = if (Build.VERSION.SDK_INT >= Build.VERSION_CODES.P) {
            info.longVersionCode
        } else {
            @Suppress("DEPRECATION")
            info.versionCode.toLong()
        }
        return ArchiveInfo(info.packageName, versionCode)
    }

    /** Versión instalada de [packageName], o null si no está instalada. */
    fun installedVersionCode(packageName: String): Long? = try {
        val info = if (Build.VERSION.SDK_INT >= Build.VERSION_CODES.TIRAMISU) {
            context.packageManager.getPackageInfo(packageName, PackageManager.PackageInfoFlags.of(0))
        } else {
            @Suppress("DEPRECATION")
            context.packageManager.getPackageInfo(packageName, 0)
        }
        if (Build.VERSION.SDK_INT >= Build.VERSION_CODES.P) {
            info.longVersionCode
        } else {
            @Suppress("DEPRECATION")
            info.versionCode.toLong()
        }
    } catch (_: PackageManager.NameNotFoundException) {
        null
    }

    suspend fun install(apk: File, packageName: String): Outcome {
        val installer = context.packageManager.packageInstaller
        val action = "$ACTION_PREFIX.${System.nanoTime()}"
        val result = CompletableDeferred<Outcome>()

        val receiver = object : BroadcastReceiver() {
            override fun onReceive(ctx: Context, intent: Intent) {
                val status = intent.getIntExtra(PackageInstaller.EXTRA_STATUS, PackageInstaller.STATUS_FAILURE)
                val message = intent.getStringExtra(PackageInstaller.EXTRA_STATUS_MESSAGE).orEmpty()
                when (status) {
                    PackageInstaller.STATUS_SUCCESS -> result.complete(Outcome.Installed)
                    PackageInstaller.STATUS_PENDING_USER_ACTION -> {
                        if (!canConfirmWithoutTouch()) {
                            result.complete(
                                Outcome.Failed(
                                    "INSTALL_REQUIRES_USER",
                                    "Android pidió confirmación y no hay Device Owner ni accesibilidad."
                                )
                            )
                        } else {
                            confirmIntent(intent)?.let { confirm ->
                                confirm.addFlags(Intent.FLAG_ACTIVITY_NEW_TASK)
                                runCatching { context.startActivity(confirm) }
                            }
                        }
                    }
                    else -> result.complete(Outcome.Failed("INSTALL_FAILED_$status", message.ifBlank { "Android rechazó la instalación." }))
                }
            }
        }

        ContextCompat.registerReceiver(context, receiver, IntentFilter(action), ContextCompat.RECEIVER_NOT_EXPORTED)
        try {
            val sessionId = withContext(Dispatchers.IO) {
                // APK completo: MODE_INHERIT_EXISTING conservaría splits de la
                // versión anterior y puede romper actualizaciones.
                val params = PackageInstaller.SessionParams(PackageInstaller.SessionParams.MODE_FULL_INSTALL).apply {
                    setAppPackageName(packageName)
                    setInstallReason(PackageManager.INSTALL_REASON_POLICY)
                    if (Build.VERSION.SDK_INT >= Build.VERSION_CODES.S) {
                        setRequireUserAction(PackageInstaller.SessionParams.USER_ACTION_NOT_REQUIRED)
                    }
                }
                val id = installer.createSession(params)
                installer.openSession(id).use { session ->
                    apk.inputStream().use { input ->
                        session.openWrite("base.apk", 0, apk.length()).use { output ->
                            input.copyTo(output)
                            session.fsync(output)
                        }
                    }
                }
                id
            }

            val pending = PendingIntent.getBroadcast(
                context,
                sessionId,
                Intent(action).setPackage(context.packageName),
                PendingIntent.FLAG_UPDATE_CURRENT or
                    (if (Build.VERSION.SDK_INT >= Build.VERSION_CODES.S) PendingIntent.FLAG_MUTABLE else 0)
            )
            InstallSessionGuard.allow(packageName)
            installer.openSession(sessionId).use { it.commit(pending.intentSender) }

            return withTimeoutOrNull(INSTALL_TIMEOUT_MS) { result.await() }
                ?: Outcome.Failed("INSTALL_TIMEOUT", "Android no respondió a la instalación de $packageName.")
        } catch (exception: Exception) {
            return Outcome.Failed("INSTALL_ERROR", exception.message ?: exception.javaClass.simpleName)
        } finally {
            InstallSessionGuard.clear(packageName)
            runCatching { context.unregisterReceiver(receiver) }
        }
    }

    private fun canConfirmWithoutTouch(): Boolean {
        val owner = context.getSystemService(DevicePolicyManager::class.java)
            ?.isDeviceOwnerApp(context.packageName) == true
        if (owner) return true
        val enabled = Settings.Secure.getString(
            context.contentResolver,
            Settings.Secure.ENABLED_ACCESSIBILITY_SERVICES
        ).orEmpty()
        return enabled.contains(context.packageName + "/" + InstallConfirmationService::class.java.name)
    }

    private fun confirmIntent(intent: Intent): Intent? =
        if (Build.VERSION.SDK_INT >= Build.VERSION_CODES.TIRAMISU) {
            intent.getParcelableExtra(Intent.EXTRA_INTENT, Intent::class.java)
        } else {
            @Suppress("DEPRECATION")
            intent.getParcelableExtra(Intent.EXTRA_INTENT)
        }

    private companion object {
        const val ACTION_PREFIX = "com.titanmdm.agent.INSTALL_RESULT"
        const val INSTALL_TIMEOUT_MS = 5 * 60 * 1000L
    }
}
