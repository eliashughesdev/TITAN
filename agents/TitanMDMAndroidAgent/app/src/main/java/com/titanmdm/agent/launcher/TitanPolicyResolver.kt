package com.titanmdm.agent.launcher

import kotlinx.serialization.json.Json
import kotlinx.serialization.json.JsonArray
import kotlinx.serialization.json.JsonObject
import kotlinx.serialization.json.JsonPrimitive
import kotlinx.serialization.json.contentOrNull

/**
 * Origen efectivo de la política del Titan Launcher.
 */
enum class TitanLauncherPolicyOrigin {
    /** Configuración administrada real (Android Device Policy). */
    AndroidDevicePolicy,

    /** Perfil local de laboratorio (sólo builds debug). */
    LabLocal,

    /** Sin política utilizable. */
    None
}

/**
 * Resultado de resolver la política efectiva del launcher.
 */
data class TitanLauncherPolicyResolution(
    val policy: TitanLauncherPolicy,
    val origin: TitanLauncherPolicyOrigin
)

/**
 * Traduce el JSON "amigable" del perfil de laboratorio al mapa de
 * restricciones que entiende [TitanLauncherPolicyReader].
 *
 * El perfil de laboratorio se escribe de forma legible (con arreglos y
 * booleanos reales); aquí se aplanan a cadenas para reutilizar el mismo
 * lector y las mismas validaciones que la configuración administrada.
 *
 * Devuelve `null` cuando el contenido no es un objeto JSON válido.
 */
object TitanLabPolicyJson {

    private val json = Json

    fun toRestrictions(jsonText: String): Map<String, String>? {
        val root = try {
            json.parseToJsonElement(jsonText) as? JsonObject
        } catch (_: Exception) {
            null
        } ?: return null

        val result = mutableMapOf<String, String>()

        for ((key, value) in root) {
            result[key] = when (value) {
                is JsonObject, is JsonArray -> value.toString()
                is JsonPrimitive -> value.contentOrNull ?: value.toString()
                else -> return null
            }
        }

        return result
    }
}

/**
 * Resuelve la política efectiva del Titan Launcher respetando el orden
 * de prioridad y el aislamiento debug/release.
 *
 * Reglas (fail-closed):
 *  1. Si existen restricciones administradas reales, SIEMPRE ganan y el
 *     perfil de laboratorio se ignora por completo.
 *  2. El perfil de laboratorio sólo se aplica cuando `labEnabled` es
 *     verdadero (build debug) y la app NO es device/profile owner.
 *  3. Sin política administrada ni laboratorio, el resultado es la
 *     política vacía ([TitanLauncherPolicyState.Absent]).
 *
 * Este resolver es puro (sin framework) y por tanto verificable en JVM.
 */
object TitanLauncherPolicyResolver {

    const val LAB_PROFILE_FILE_NAME = "titan_lab_policy.json"

    fun resolve(
        officialRestrictions: Map<String, String>,
        labProfileJson: String?,
        labEnabled: Boolean,
        deviceManaged: Boolean
    ): TitanLauncherPolicyResolution {

        // 1. La configuración administrada siempre tiene prioridad.
        if (officialRestrictions.isNotEmpty()) {
            return TitanLauncherPolicyResolution(
                policy = TitanLauncherPolicyReader.read(officialRestrictions),
                origin = TitanLauncherPolicyOrigin.AndroidDevicePolicy
            )
        }

        // 2. Modo laboratorio sólo en builds debug y fuera de DPC.
        if (labEnabled && !deviceManaged) {
            return TitanLauncherPolicyResolution(
                policy = resolveLabProfile(labProfileJson),
                origin = TitanLauncherPolicyOrigin.LabLocal
            )
        }

        // 3. Sin política.
        return TitanLauncherPolicyResolution(
            policy = TitanLauncherPolicyReader.read(officialRestrictions),
            origin = TitanLauncherPolicyOrigin.None
        )
    }

    private fun resolveLabProfile(labProfileJson: String?): TitanLauncherPolicy {
        val trimmed = labProfileJson?.trim()

        if (trimmed.isNullOrEmpty()) {
            return TitanLauncherPolicy(
                state = TitanLauncherPolicyState.Absent,
                isLabProfile = true
            )
        }

        val values = TitanLabPolicyJson.toRestrictions(trimmed)

        if (values == null) {
            return TitanLauncherPolicy(
                state = TitanLauncherPolicyState.Invalid,
                error = "El perfil de laboratorio no es un objeto JSON válido.",
                isLabProfile = true
            )
        }

        return TitanLauncherPolicyReader
            .read(values)
            .copy(isLabProfile = true)
    }
}
