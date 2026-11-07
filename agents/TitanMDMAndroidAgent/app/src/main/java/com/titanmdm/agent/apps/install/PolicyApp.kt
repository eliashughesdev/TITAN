package com.titanmdm.agent.apps.install

import kotlinx.serialization.json.JsonArray
import kotlinx.serialization.json.JsonObject
import kotlinx.serialization.json.JsonPrimitive
import kotlinx.serialization.json.booleanOrNull
import kotlinx.serialization.json.contentOrNull
import kotlinx.serialization.json.longOrNull

/**
 * App obligatoria de la política (payload "applications" de
 * APPLY_LAUNCHER_POLICY). available = false: la consola no tiene el APK
 * en el repositorio de software.
 */
data class PolicyApp(
    val packageName: String,
    val installType: String,
    /** false = AVAILABLE: se instala si hay APK, su ausencia no incumple. */
    val required: Boolean,
    val available: Boolean,
    val packageId: String?,
    val downloadUrl: String?,
    val sha256: String?,
    val versionCode: Long?,
    val versionName: String?,
    val sizeBytes: Long?
) {
    companion object {
        private val packagePattern = Regex("^[A-Za-z][A-Za-z0-9_]*(\\.[A-Za-z][A-Za-z0-9_]*)+$")

        fun parseList(element: kotlinx.serialization.json.JsonElement?): List<PolicyApp> =
            (element as? JsonArray)
                ?.mapNotNull { (it as? JsonObject)?.let(::parse) }
                ?.distinctBy { it.packageName }
                .orEmpty()

        fun parse(json: JsonObject): PolicyApp? {
            fun text(name: String) = (json[name] as? JsonPrimitive)?.contentOrNull?.trim()?.takeIf { it.isNotEmpty() }
            fun number(name: String) = (json[name] as? JsonPrimitive)?.longOrNull

            val packageName = text("packageName")?.takeIf { packagePattern.matches(it) } ?: return null
            val downloadUrl = text("downloadUrl")
            val sha256 = text("sha256")
            val available = (json["available"] as? JsonPrimitive)?.booleanOrNull != false &&
                downloadUrl != null && sha256 != null

            return PolicyApp(
                packageName = packageName,
                installType = text("installType") ?: "FORCE_INSTALLED",
                required = (json["required"] as? JsonPrimitive)?.booleanOrNull
                    ?: !text("installType").equals("AVAILABLE", ignoreCase = true),
                available = available,
                packageId = text("packageId"),
                downloadUrl = downloadUrl,
                sha256 = sha256,
                versionCode = number("versionCode"),
                versionName = text("versionName"),
                sizeBytes = number("sizeBytes")
            )
        }
    }
}

/** Qué hacer con cada app según lo que ya hay instalado. */
enum class AppAction {
    /** Instalar o actualizar desde el repositorio. */
    Install,

    /** Ya instalada con la versión pedida (o superior). */
    UpToDate,

    /** Sin APK en el repositorio pero ya presente en el equipo (p. ej. app del sistema). */
    InstalledWithoutPackage,

    /** Sin APK en el repositorio y no instalada: no se puede cumplir la política. */
    Missing,

    /** App opcional (AVAILABLE) sin APK y no instalada: se informa, no incumple. */
    SkippedOptional
}

object AppInstallPlanner {

    /** [installedVersionCode]: versión instalada, null si la app no existe. */
    fun plan(app: PolicyApp, installedVersionCode: Long?): AppAction = when {
        !app.available -> when {
            installedVersionCode != null -> AppAction.InstalledWithoutPackage
            app.required -> AppAction.Missing
            else -> AppAction.SkippedOptional
        }
        installedVersionCode == null -> AppAction.Install
        app.versionCode != null && installedVersionCode < app.versionCode -> AppAction.Install
        else -> AppAction.UpToDate
    }

    fun satisfied(action: AppAction): Boolean = action != AppAction.Missing
}
