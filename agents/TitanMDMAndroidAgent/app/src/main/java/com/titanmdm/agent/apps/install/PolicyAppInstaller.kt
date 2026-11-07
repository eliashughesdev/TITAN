package com.titanmdm.agent.apps.install

import android.content.Context
import com.titanmdm.agent.core.storage.AgentIdentity
import com.titanmdm.agent.core.storage.AgentIdentityStore
import kotlinx.serialization.json.JsonArray
import kotlinx.serialization.json.JsonObject
import kotlinx.serialization.json.buildJsonArray
import kotlinx.serialization.json.buildJsonObject
import kotlinx.serialization.json.put

/**
 * Cumple la lista de apps de la política: descarga (SHA-256 verificado) e
 * instala en silencio las que faltan o están desactualizadas.
 */
class PolicyAppInstaller(context: Context) {

    private val context = context.applicationContext
    private val installer = SilentPackageInstaller(this.context)
    private val downloader = ApkDownloader(this.context)

    data class AppResult(
        val packageName: String,
        val action: AppAction,
        val success: Boolean,
        val code: String? = null,
        val message: String? = null,
        val installedVersionCode: Long? = null
    )

    data class Report(val results: List<AppResult>) {
        val allSatisfied: Boolean get() = results.all { it.success }

        fun failures(): List<AppResult> = results.filterNot { it.success }

        fun toJson(): JsonArray = buildJsonArray {
            results.forEach { result ->
                add(buildJsonObject {
                    put("packageName", result.packageName)
                    put("action", result.action.name)
                    put("success", result.success)
                    result.code?.let { put("code", it) }
                    result.message?.let { put("message", it) }
                    result.installedVersionCode?.let { put("installedVersionCode", it) }
                })
            }
        }
    }

    suspend fun apply(apps: List<PolicyApp>): Report {
        if (apps.isEmpty()) return Report(emptyList())

        val identity = AgentIdentityStore(context).get()
        return Report(apps.map { app -> applyOne(app, identity) })
    }

    private suspend fun applyOne(app: PolicyApp, identity: AgentIdentity?): AppResult {
        val installed = installer.installedVersionCode(app.packageName)
        val action = AppInstallPlanner.plan(app, installed)

        if (action != AppAction.Install) {
            return AppResult(
                packageName = app.packageName,
                action = action,
                success = AppInstallPlanner.satisfied(action),
                code = if (action == AppAction.Missing) "APK_NOT_IN_REPOSITORY" else null,
                message = if (action == AppAction.Missing) {
                    "Suba el APK de ${app.packageName} al repositorio de software de TitanMDM."
                } else {
                    null
                },
                installedVersionCode = installed
            )
        }

        if (identity == null) {
            return AppResult(app.packageName, action, false, "NOT_ENROLLED", "El equipo no está inscrito.")
        }

        val downloaded = downloader.download(
            identity = identity,
            relativeUrl = app.downloadUrl.orEmpty(),
            expectedSha256 = app.sha256.orEmpty(),
            name = app.packageName
        )
        val file = when (downloaded) {
            is ApkDownloader.Outcome.Downloaded -> downloaded.file
            is ApkDownloader.Outcome.Failed -> return AppResult(app.packageName, action, false, downloaded.code, downloaded.message)
        }

        try {
            // El APK descargado debe ser realmente el paquete de la política.
            val archive = installer.readArchive(file)
            if (archive == null || archive.packageName != app.packageName) {
                return AppResult(
                    app.packageName, action, false, "PACKAGE_MISMATCH",
                    "El APK descargado es ${archive?.packageName ?: "ilegible"}, no ${app.packageName}."
                )
            }

            return when (val outcome = installer.install(file, app.packageName)) {
                SilentPackageInstaller.Outcome.Installed -> AppResult(
                    app.packageName, action, true,
                    installedVersionCode = installer.installedVersionCode(app.packageName)
                )
                is SilentPackageInstaller.Outcome.Failed -> AppResult(app.packageName, action, false, outcome.code, outcome.message)
            }
        } finally {
            file.delete()
        }
    }

    companion object {
        fun summary(report: Report): JsonObject = buildJsonObject {
            put("total", report.results.size)
            put("failed", report.failures().size)
        }
    }
}
