package com.titanmdm.agent.launcher

import android.content.Context
import android.content.RestrictionsManager
import android.os.Bundle
import kotlinx.serialization.json.Json
import kotlinx.serialization.json.JsonArray
import kotlinx.serialization.json.JsonObject
import kotlinx.serialization.json.JsonPrimitive
import kotlinx.serialization.json.booleanOrNull
import kotlinx.serialization.json.contentOrNull

/**
 * Aplicación autorizada por la política administrada del Titan Launcher.
 */
data class TitanLauncherApp(
    val packageName: String,
    val label: String,
    val required: Boolean = false
)

/**
 * Estado del perfil de launcher recibido mediante configuraciones
 * administradas de Android Enterprise.
 */
enum class TitanLauncherPolicyState {
    /** El administrador no ha habilitado todavía el Titan Launcher. */
    Absent,

    /** La política es válida y puede aplicarse. */
    Valid,

    /** La política existe pero no es utilizable (fail-closed). */
    Invalid
}

/**
 * Estado de administración real del dispositivo. Nunca se deduce del
 * launcher: se consulta al DevicePolicyManager.
 */
data class TitanLauncherAdminStatus(
    val isDeviceOwner: Boolean = false,
    val isProfileOwner: Boolean = false,
    val isAdminActive: Boolean = false,
    val isFullyManaged: Boolean = false,
    val isDedicated: Boolean = false
) {
    val isManaged: Boolean
        get() = isDeviceOwner || isProfileOwner

    val label: String
        get() = when {
            isDeviceOwner && isDedicated -> "Dispositivo dedicado administrado"
            isDeviceOwner -> "Dispositivo totalmente administrado"
            isProfileOwner -> "Perfil de trabajo administrado"
            isAdminActive -> "Administrador activo sin propiedad"
            else -> "Dispositivo no administrado"
        }

    companion object {
        val None = TitanLauncherAdminStatus()
    }
}

/**
 * Política efectiva del Titan Launcher.
 *
 * [managed] sólo es verdadero cuando el estado es [TitanLauncherPolicyState.Valid],
 * de modo que una configuración ausente o corrupta nunca se interpreta
 * como un launcher administrado.
 */
data class TitanLauncherPolicy(
    val state: TitanLauncherPolicyState = TitanLauncherPolicyState.Absent,
    val error: String? = null,
    val companyName: String = DEFAULT_COMPANY,
    val welcomeMessage: String = DEFAULT_MESSAGE,
    val launcherEnabled: Boolean = false,
    val schemaVersion: Int = DEFAULT_SCHEMA_VERSION,
    val applications: List<TitanLauncherApp> = emptyList()
) {
    val managed: Boolean
        get() = state == TitanLauncherPolicyState.Valid

    companion object {
        const val DEFAULT_COMPANY = "TitanMDM Enterprise"
        const val DEFAULT_MESSAGE = "Dispositivo corporativo"
        const val DEFAULT_SCHEMA_VERSION = 1
        const val SUPPORTED_SCHEMA_VERSION = 1
    }
}

/**
 * Lee y valida la configuración administrada del Titan Launcher.
 *
 * El movimiento está deliberadamente cerrado ante fallos:
 *  - sin lista de aplicaciones => [TitanLauncherPolicyState.Invalid];
 *  - JSON corrupto            => [TitanLauncherPolicyState.Invalid];
 *  - launcher deshabilitado   => [TitanLauncherPolicyState.Absent].
 *
 * La lista resultante nunca incluye aplicaciones fuera de las autorizadas.
 */
object TitanLauncherPolicyReader {

    const val KEY_COMPANY = "titan_company_name"
    const val KEY_MESSAGE = "titan_welcome_message"
    const val KEY_APPS = "titan_allowed_apps"
    const val KEY_ENABLED = "titan_launcher_enabled"
    const val KEY_SCHEMA_VERSION = "titan_launcher_schema_version"

    private const val MAX_APPS = 100
    private const val MAX_LABEL = 60
    private const val MAX_COMPANY = 80
    private const val MAX_MESSAGE = 180

    private val validPackage =
        Regex("^[a-zA-Z][a-zA-Z0-9_]*(\\.[a-zA-Z][a-zA-Z0-9_]*)+$")

    private val json =
        Json {
            ignoreUnknownKeys = true
            isLenient = true
        }

    fun read(context: Context): TitanLauncherPolicy {
        val manager =
            context.getSystemService(Context.RESTRICTIONS_SERVICE)
                as? RestrictionsManager

        if (manager == null) {
            return TitanLauncherPolicy(
                state = TitanLauncherPolicyState.Invalid,
                error = "RestrictionsManager no disponible."
            )
        }

        val restrictions =
            manager.applicationRestrictions ?: Bundle.EMPTY

        return read(toMap(restrictions))
    }

    /**
     * Variante pura (sin framework) para pruebas unitarias JVM.
     */
    fun read(values: Map<String, String>): TitanLauncherPolicy {
        val company = values[KEY_COMPANY]
            ?.trim()
            ?.take(MAX_COMPANY)
            ?.takeIf { it.isNotEmpty() }
            ?: TitanLauncherPolicy.DEFAULT_COMPANY

        val message = values[KEY_MESSAGE]
            ?.trim()
            ?.take(MAX_MESSAGE)
            ?.takeIf { it.isNotEmpty() }
            ?: TitanLauncherPolicy.DEFAULT_MESSAGE

        val schemaVersion = values[KEY_SCHEMA_VERSION]
            ?.trim()
            ?.toIntOrNull()
            ?: TitanLauncherPolicy.DEFAULT_SCHEMA_VERSION

        val hasAppsKey = values.containsKey(KEY_APPS)

        val enabled = values[KEY_ENABLED]
            ?.trim()
            ?.let { parseBoolean(it) }
            ?: hasAppsKey

        if (!enabled) {
            return TitanLauncherPolicy(
                state = TitanLauncherPolicyState.Absent,
                companyName = company,
                welcomeMessage = message,
                launcherEnabled = false,
                schemaVersion = schemaVersion
            )
        }

        if (schemaVersion > TitanLauncherPolicy.SUPPORTED_SCHEMA_VERSION) {
            return TitanLauncherPolicy(
                state = TitanLauncherPolicyState.Invalid,
                error = "Versión de esquema del launcher no soportada ($schemaVersion).",
                companyName = company,
                welcomeMessage = message,
                launcherEnabled = true,
                schemaVersion = schemaVersion
            )
        }

        val rawApps = values[KEY_APPS]

        if (rawApps.isNullOrBlank()) {
            return TitanLauncherPolicy(
                state = TitanLauncherPolicyState.Invalid,
                error = "El launcher está habilitado pero no contiene aplicaciones autorizadas.",
                companyName = company,
                welcomeMessage = message,
                launcherEnabled = true,
                schemaVersion = schemaVersion
            )
        }

        val apps = try {
            parseApplications(rawApps)
        } catch (_: Exception) {
            return TitanLauncherPolicy(
                state = TitanLauncherPolicyState.Invalid,
                error = "La lista de aplicaciones autorizadas no es válida.",
                companyName = company,
                welcomeMessage = message,
                launcherEnabled = true,
                schemaVersion = schemaVersion
            )
        }

        return TitanLauncherPolicy(
            state = TitanLauncherPolicyState.Valid,
            companyName = company,
            welcomeMessage = message,
            launcherEnabled = true,
            schemaVersion = schemaVersion,
            applications = apps
        )
    }

    private fun parseApplications(rawApps: String): List<TitanLauncherApp> {
        val array = json.parseToJsonElement(rawApps) as? JsonArray
            ?: throw IllegalArgumentException("La lista de aplicaciones no es un arreglo JSON.")

        val result = mutableListOf<TitanLauncherApp>()

        for (node in array.take(MAX_APPS)) {
            val item = node as? JsonObject ?: continue

            val packageName =
                (item["packageName"] as? JsonPrimitive)?.contentOrNull?.trim()

            if (packageName.isNullOrEmpty() || !validPackage.matches(packageName)) {
                continue
            }

            if (result.any { it.packageName == packageName }) {
                continue
            }

            val label = (item["label"] as? JsonPrimitive)
                ?.contentOrNull
                ?.trim()
                ?.take(MAX_LABEL)
                ?.ifBlank { packageName }
                ?: packageName

            val required =
                (item["required"] as? JsonPrimitive)?.booleanOrNull
                    ?: (item["forceInstalled"] as? JsonPrimitive)?.booleanOrNull
                    ?: false

            result.add(
                TitanLauncherApp(
                    packageName = packageName,
                    label = label,
                    required = required
                )
            )
        }

        return result
    }

    private fun parseBoolean(value: String): Boolean {
        return when (value.lowercase()) {
            "true", "1", "yes", "y", "on" -> true
            "false", "0", "no", "n", "off" -> false
            else -> value.isNotEmpty()
        }
    }

    @Suppress("DEPRECATION")
    private fun toMap(bundle: Bundle): Map<String, String> {
        val result = mutableMapOf<String, String>()

        for (key in bundle.keySet()) {
            val raw = bundle.get(key) ?: continue
            result[key] = raw.toString()
        }

        return result
    }
}
