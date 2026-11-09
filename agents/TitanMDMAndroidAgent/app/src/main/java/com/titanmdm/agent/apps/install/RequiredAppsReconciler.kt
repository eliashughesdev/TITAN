package com.titanmdm.agent.apps.install

import android.content.Context
import android.util.Log
import androidx.core.content.edit
import kotlinx.coroutines.Dispatchers
import kotlinx.coroutines.sync.Mutex
import kotlinx.coroutines.sync.withLock
import kotlinx.coroutines.withContext

/**
 * Reintento desatendido de las apps obligatorias de la última política
 * (TitanServerAppStore). APPLY_LAUNCHER_POLICY instala al recibir la
 * política; si en ese momento falló la red o la descarga, el servicio en
 * primer plano vuelve a intentarlo tras cada sincronización, con espera
 * mínima entre intentos para no descargar en bucle.
 */
object RequiredAppsReconciler {

    private const val TAG = "TitanRequiredApps"
    private const val PREFS = "titan_required_apps"
    private const val KEY_LAST_ATTEMPT = "last_auto_install_at"
    const val COOLDOWN_MS = 10 * 60 * 1000L

    private val mutex = Mutex()

    /** Apps obligatorias que faltan o están desactualizadas. */
    fun pending(context: Context): List<PolicyApp> {
        val installer = SilentPackageInstaller(context.applicationContext)
        return TitanServerAppStore.read(context)
            .filter { it.required }
            .filter { AppInstallPlanner.plan(it, installer.installedVersionCode(it.packageName)) == AppAction.Install }
    }

    /**
     * APPLY_LAUNCHER_POLICY: instala las apps de la política recién recibida.
     * Comparte el candado con los reintentos para no instalar dos veces.
     */
    suspend fun install(context: Context, apps: List<PolicyApp>): PolicyAppInstaller.Report {
        val app = context.applicationContext
        return mutex.withLock {
            markAttempt(app)
            withContext(Dispatchers.IO) { PolicyAppInstaller(app).apply(apps) }
        }
    }

    /**
     * Reintento en segundo plano (tras cada sincronización del servicio).
     * @param force ignora la espera mínima (p. ej. nueva política en el launcher).
     * @return informe de la instalación, o null si no había nada que hacer.
     */
    suspend fun reconcile(context: Context, force: Boolean = false): PolicyAppInstaller.Report? {
        val app = context.applicationContext
        if (mutex.isLocked) return null

        return mutex.withLock {
            withContext(Dispatchers.IO) {
                val pending = pending(app)
                if (pending.isEmpty()) return@withContext null

                val last = prefs(app).getLong(KEY_LAST_ATTEMPT, 0L)
                if (!force && System.currentTimeMillis() - last < COOLDOWN_MS) return@withContext null
                markAttempt(app)

                Log.i(TAG, "Instalando ${pending.size} app(s) obligatoria(s) pendiente(s).")
                PolicyAppInstaller(app).apply(pending).also { report ->
                    report.failures().forEach { Log.w(TAG, "${it.packageName}: ${it.code} ${it.message}") }
                }
            }
        }
    }

    /** Instala una app concreta de la política (toque del operador en el launcher). */
    suspend fun installOne(context: Context, packageName: String): PolicyAppInstaller.AppResult? {
        val app = context.applicationContext
        val policyApp = TitanServerAppStore.read(app).firstOrNull { it.packageName == packageName && it.available }
            ?: return null
        return install(app, listOf(policyApp)).results.firstOrNull()
    }

    private fun markAttempt(context: Context) =
        prefs(context).edit { putLong(KEY_LAST_ATTEMPT, System.currentTimeMillis()) }

    private fun prefs(context: Context) =
        context.applicationContext.getSharedPreferences(PREFS, Context.MODE_PRIVATE)
}
