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
import java.util.Locale

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
 * Estado de Titan como aplicación HOME. No representa propiedad ni
 * administración del dispositivo; Android Device Policy sigue siendo el DPC.
 */
data class TitanLauncherHomeStatus(
    val isDefaultHome: Boolean = false
) {
    val label: String
        get() = if (isDefaultHome) {
            "Titan es HOME predeterminado"
        } else {
            "Titan no es HOME predeterminado"
        }

    companion object {
        val None = TitanLauncherHomeStatus()
    }
}

enum class TitanLauncherAppState {
    Launchable,
    NotInstalled,
    Disabled,
    NotLaunchable,
    Unavailable
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
    val primaryColor: String = DEFAULT_PRIMARY_COLOR,
    val backgroundColor: String = DEFAULT_BACKGROUND_COLOR,
    val launcherEnabled: Boolean = false,
    val schemaVersion: Int = DEFAULT_SCHEMA_VERSION,
    val applications: List<TitanLauncherApp> = emptyList(),
    val wallpaperUrl: String? = null,
    val logoUrl: String? = null,
    val gridColumns: Int = DEFAULT_GRID_COLUMNS,
    val textColor: String = "#F4F7F8",
    val cardColor: String = "#142831",
    val cardTextColor: String = "#F4F7F8",
    val iconSize: Int = 56,
    val gridGap: Int = 12,
    val imageFit: String = "cover",
    val configVersion: Int? = null,
    val profileId: String? = null,
    val publicationId: String? = null,
    val updatedAt: String? = null,
    val resourceError: String? = null,
    /**
     * Verdadero cuando la política proviene del perfil local de
     * laboratorio (sólo builds debug). Nunca implica administración real
     * del dispositivo por Android Device Policy.
     */
    val isLabProfile: Boolean = false
) {
    /**
     * `managed` representa exclusivamente una política administrada real
     * por Android Device Policy. Un perfil de laboratorio válido NO se
     * considera administrado para no marcar el dispositivo como
     * Fully Managed.
     */
    val managed: Boolean
        get() = state == TitanLauncherPolicyState.Valid && !isLabProfile

    companion object {
        const val DEFAULT_COMPANY = "TitanMDM Enterprise"
        const val DEFAULT_MESSAGE = "Dispositivo corporativo"
        const val DEFAULT_PRIMARY_COLOR = "#16A36A"
        const val DEFAULT_BACKGROUND_COLOR = "#0B1720"
        const val DEFAULT_SCHEMA_VERSION = 1
        const val SUPPORTED_SCHEMA_VERSION = 2
        const val DEFAULT_GRID_COLUMNS = 4
        val SUPPORTED_SCHEMA_VERSIONS = setOf(1, 2)
    }
}

/**
 * Lee y valida la configuración administrada del Titan Launcher.
 *
 * El movimiento está deliberadamente cerrado ante fallos:
 *  - sin clave/lista de aplicaciones => [TitanLauncherPolicyState.Invalid];
 *  - JSON corrupto            => [TitanLauncherPolicyState.Invalid];
 *  - launcher deshabilitado   => [TitanLauncherPolicyState.Absent].
 *
 * Una lista vacía y una lista que sólo contiene entradas inválidas producen
 * la misma política válida sin aplicaciones. La ausencia de versión identifica
 * el formato legado v1: sólo éste infiere enabled por la presencia de apps y
 * reconoce forceInstalled como alias de required.
 *
 * La lista resultante nunca incluye aplicaciones fuera de las autorizadas.
 */
object TitanLauncherPolicyReader {

    const val KEY_COMPANY = "titan_company_name"
    const val KEY_MESSAGE = "titan_welcome_message"
    const val KEY_APPS = "titan_allowed_apps"
    const val KEY_ENABLED = "titan_launcher_enabled"
    const val KEY_SCHEMA_VERSION = "titan_launcher_schema_version"
    const val KEY_PRIMARY_COLOR = "titan_primary_color"
    const val KEY_BACKGROUND_COLOR = "titan_background_color"
    const val KEY_WALLPAPER_URL = "titan_wallpaper_url"
    const val KEY_LOGO_URL = "titan_logo_url"
    const val KEY_GRID_COLUMNS = "titan_grid_columns"
    const val KEY_TEXT_COLOR = "titan_text_color"
    const val KEY_CARD_COLOR = "titan_card_color"
    const val KEY_CARD_TEXT_COLOR = "titan_card_text_color"
    const val KEY_ICON_SIZE = "titan_icon_size"
    const val KEY_GRID_GAP = "titan_grid_gap"
    const val KEY_IMAGE_FIT = "titan_image_fit"
    const val KEY_CONFIG_VERSION = "titan_config_version"
    const val KEY_PROFILE_ID = "titan_profile_id"
    const val KEY_PUBLICATION_ID = "titan_publication_id"
    const val KEY_UPDATED_AT = "titan_updated_at"

    private const val MAX_APPS = 100
    private const val MAX_LABEL = 60
    private const val MAX_COMPANY = 80
    private const val MAX_MESSAGE = 180

    private val validPackage =
        Regex("^[a-zA-Z][a-zA-Z0-9_]*(\\.[a-zA-Z][a-zA-Z0-9_]*)+$")

    private val validColor = Regex("^#[0-9A-Fa-f]{6}$")
    private val whitespace = Regex("\\s+")

    private val json = Json

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
        val company = validatedText(
            values[KEY_COMPANY],
            MAX_COMPANY,
            TitanLauncherPolicy.DEFAULT_COMPANY
        )
        val message = validatedText(
            values[KEY_MESSAGE],
            MAX_MESSAGE,
            TitanLauncherPolicy.DEFAULT_MESSAGE
        )
        val primaryColor = validatedColor(
            values[KEY_PRIMARY_COLOR],
            TitanLauncherPolicy.DEFAULT_PRIMARY_COLOR
        )
        val backgroundColor = validatedColor(
            values[KEY_BACKGROUND_COLOR],
            TitanLauncherPolicy.DEFAULT_BACKGROUND_COLOR
        )

        val rawSchemaVersion = values[KEY_SCHEMA_VERSION]
        val isLegacyV1 = rawSchemaVersion == null
        val parsedSchemaVersion = rawSchemaVersion?.trim()?.toIntOrNull()
        val schemaVersion = parsedSchemaVersion
            ?: TitanLauncherPolicy.DEFAULT_SCHEMA_VERSION

        val wallpaper = validatedResourceUrl(values[KEY_WALLPAPER_URL])
        val logo = validatedResourceUrl(values[KEY_LOGO_URL])
        val gridColumns = validatedGridColumns(values[KEY_GRID_COLUMNS])
            ?: TitanLauncherPolicy.DEFAULT_GRID_COLUMNS
        val textColor = validatedColor(values[KEY_TEXT_COLOR], "#F4F7F8")
        val cardColor = validatedColor(values[KEY_CARD_COLOR], "#142831")
        val cardTextColor = validatedColor(values[KEY_CARD_TEXT_COLOR], "#F4F7F8")
        val iconSize = values[KEY_ICON_SIZE]?.trim()?.toIntOrNull()?.takeIf { it in 40..96 } ?: 56
        val gridGap = values[KEY_GRID_GAP]?.trim()?.toIntOrNull()?.takeIf { it in 4..32 } ?: 12
        val imageFit = values[KEY_IMAGE_FIT]?.trim()?.lowercase()?.takeIf { it == "cover" || it == "contain" || it == "center" } ?: "cover"
        val columnsInvalid = values.containsKey(KEY_GRID_COLUMNS) &&
            validatedGridColumns(values[KEY_GRID_COLUMNS]) == null
        val configVersion = validatedConfigVersion(values[KEY_CONFIG_VERSION])
        val profileId = validatedGuid(values[KEY_PROFILE_ID])
        val publicationId = validatedGuid(values[KEY_PUBLICATION_ID])
        val updatedAt = validatedTimestamp(values[KEY_UPDATED_AT])
        val resourceError = listOfNotNull(
            wallpaper.second,
            logo.second
        ).firstOrNull()

        fun invalid(error: String, enabled: Boolean = false) =
            TitanLauncherPolicy(
                state = TitanLauncherPolicyState.Invalid,
                error = error,
                companyName = company,
                welcomeMessage = message,
                primaryColor = primaryColor,
                backgroundColor = backgroundColor,
                launcherEnabled = enabled,
                schemaVersion = schemaVersion,
                wallpaperUrl = wallpaper.first,
                logoUrl = logo.first,
                gridColumns = gridColumns,
                textColor = textColor,
                cardColor = cardColor,
                cardTextColor = cardTextColor,
                iconSize = iconSize,
                gridGap = gridGap,
                imageFit = imageFit,
                configVersion = configVersion,
                profileId = profileId,
                publicationId = publicationId,
                updatedAt = updatedAt,
                resourceError = resourceError
            )

        if (!isLegacyV1 &&
            parsedSchemaVersion !in TitanLauncherPolicy.SUPPORTED_SCHEMA_VERSIONS
        ) {
            return invalid(
                "Versión de esquema del launcher no válida o no soportada."
            )
        }

        if (columnsInvalid) {
            return invalid("El número de columnas del launcher no es válido.")
        }

        val hasAppsKey = values.containsKey(KEY_APPS)
        val rawEnabled = values[KEY_ENABLED]
        val enabled = when {
            rawEnabled != null -> parseBoolean(rawEnabled.trim())
                ?: return invalid("El valor de habilitación del launcher no es válido.")
            isLegacyV1 -> hasAppsKey
            else -> return invalid("La configuración no indica si el launcher está habilitado.")
        }

        if (!enabled) {
            return TitanLauncherPolicy(
                state = TitanLauncherPolicyState.Absent,
                companyName = company,
                welcomeMessage = message,
                primaryColor = primaryColor,
                backgroundColor = backgroundColor,
                launcherEnabled = false,
                schemaVersion = schemaVersion,
                wallpaperUrl = null,
                logoUrl = null,
                gridColumns = gridColumns,
                textColor = textColor,
                cardColor = cardColor,
                cardTextColor = cardTextColor,
                iconSize = iconSize,
                gridGap = gridGap,
                imageFit = imageFit,
                configVersion = null,
                profileId = null,
                publicationId = null,
                updatedAt = null
            )
        }

        val rawApps = values[KEY_APPS]

        if (rawApps.isNullOrBlank()) {
            return invalid(
                "El launcher está habilitado pero no contiene aplicaciones autorizadas.",
                enabled = true
            )
        }

        val apps = try {
            parseApplications(rawApps, allowLegacyForceInstalled = isLegacyV1)
        } catch (_: Exception) {
            return invalid(
                "La lista de aplicaciones autorizadas no es válida.",
                enabled = true
            )
        }

        return TitanLauncherPolicy(
            state = TitanLauncherPolicyState.Valid,
            companyName = company,
            welcomeMessage = message,
            primaryColor = primaryColor,
            backgroundColor = backgroundColor,
            launcherEnabled = true,
            schemaVersion = schemaVersion,
            applications = apps,
            wallpaperUrl = wallpaper.first,
            logoUrl = logo.first,
            gridColumns = gridColumns,
            configVersion = configVersion,
            profileId = profileId,
            publicationId = publicationId,
            updatedAt = updatedAt,
            resourceError = resourceError
        )
    }

    private fun parseApplications(
        rawApps: String,
        allowLegacyForceInstalled: Boolean
    ): List<TitanLauncherApp> {
        val array = json.parseToJsonElement(rawApps) as? JsonArray
            ?: throw IllegalArgumentException("La lista de aplicaciones no es un arreglo JSON.")

        val result = mutableListOf<TitanLauncherApp>()
        val seenPackages = mutableSetOf<String>()

        for (node in array) {
            val item = node as? JsonObject ?: continue

            val packagePrimitive = item["packageName"] as? JsonPrimitive
            val packageName = packagePrimitive
                ?.takeIf { it.isString }
                ?.contentOrNull
                ?.trim()

            if (packageName.isNullOrEmpty() || !validPackage.matches(packageName)) {
                continue
            }

            if (!seenPackages.add(packageName.lowercase(Locale.ROOT))) {
                continue
            }

            val labelPrimitive = item["label"] as? JsonPrimitive
            val label = validatedText(
                labelPrimitive?.takeIf { it.isString }?.contentOrNull,
                MAX_LABEL,
                packageName
            )

            val required =
                (item["required"] as? JsonPrimitive)?.booleanOrNull
                    ?: if (allowLegacyForceInstalled) {
                        (item["forceInstalled"] as? JsonPrimitive)?.booleanOrNull
                    } else {
                        null
                    }
                    ?: false

            result.add(
                TitanLauncherApp(
                    packageName = packageName,
                    label = label,
                    required = required
                )
            )

            if (result.size == MAX_APPS) {
                break
            }
        }

        return result
    }

    private fun parseBoolean(value: String): Boolean? {
        return when (value.lowercase()) {
            "true" -> true
            "false" -> false
            else -> null
        }
    }

    private fun validatedText(value: String?, maxLength: Int, default: String): String {
        val normalized = value
            ?.map { if (it.isISOControl()) ' ' else it }
            ?.joinToString("")
            ?.trim()
            ?.replace(whitespace, " ")
            ?.take(maxLength)
            ?.trim()

        return normalized?.takeIf { it.isNotEmpty() } ?: default
    }

    private fun validatedResourceUrl(value: String?): Pair<String?, String?> {
        val raw = value?.trim().orEmpty()
        if (raw.isEmpty()) {
            return null to null
        }
        val normalized = TitanLauncherResourceUrl.normalize(raw)
        return if (normalized == null) {
            null to "Recurso HTTPS rechazado."
        } else {
            normalized to null
        }
    }

    private fun validatedGridColumns(value: String?): Int? {
        if (value.isNullOrBlank()) {
            return TitanLauncherPolicy.DEFAULT_GRID_COLUMNS
        }
        val parsed = value.trim().toIntOrNull() ?: return null
        return parsed.takeIf { it in 3..5 }
    }

    private fun validatedConfigVersion(value: String?): Int? {
        val parsed = value?.trim()?.toIntOrNull() ?: return null
        return parsed.takeIf { it >= 1 }
    }

    private fun validatedGuid(value: String?): String? {
        val raw = value?.trim().orEmpty()
        if (raw.isEmpty()) return null
        return raw.takeIf {
            Regex("^[0-9a-fA-F]{8}-[0-9a-fA-F]{4}-[0-9a-fA-F]{4}-[0-9a-fA-F]{4}-[0-9a-fA-F]{12}$")
                .matches(it)
        }?.lowercase(Locale.ROOT)
    }

    private fun validatedTimestamp(value: String?): String? {
        val raw = value?.trim().orEmpty()
        if (raw.isEmpty() || raw.length > 40) return null
        return raw.takeIf { it.contains('T') && it.endsWith('Z') }
    }

    private fun validatedColor(value: String?, default: String): String {
        val normalized = value?.trim()
        return if (normalized != null && validColor.matches(normalized)) {
            normalized.uppercase(Locale.ROOT)
        } else {
            default
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
