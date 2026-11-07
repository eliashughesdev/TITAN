package com.titanmdm.agent.commands.handlers

import android.content.Context
import com.titanmdm.agent.apps.install.PolicyApp
import com.titanmdm.agent.apps.install.PolicyAppInstaller
import com.titanmdm.agent.commands.CommandExecutionResult
import com.titanmdm.agent.core.network.DeviceCommandDto
import com.titanmdm.agent.kiosk.KioskController
import com.titanmdm.agent.launcher.TitanLauncherPolicyReader
import com.titanmdm.agent.launcher.TitanLauncherPolicyState
import com.titanmdm.agent.launcher.TitanServerLauncherStore
import kotlinx.serialization.json.Json
import kotlinx.serialization.json.JsonArray
import kotlinx.serialization.json.JsonElement
import kotlinx.serialization.json.JsonNull
import kotlinx.serialization.json.JsonObject
import kotlinx.serialization.json.JsonPrimitive
import kotlinx.serialization.json.buildJsonObject
import kotlinx.serialization.json.contentOrNull
import kotlinx.serialization.json.intOrNull
import kotlinx.serialization.json.jsonPrimitive
import kotlinx.serialization.json.put

/**
 * APPLY_LAUNCHER_POLICY: política Android asignada desde la consola a
 * un equipo inscrito por el agente (sin Android Enterprise).
 *
 * Payload: { assignmentId, policyId, policyName, policyVersion,
 *            publicationId, launcher: { titan_*: ... } | null,
 *            applications: [{ packageName, installType, available,
 *                             downloadUrl, sha256, versionCode }] }
 *
 * Las apps obligatorias se descargan del repositorio de software del
 * servidor y se instalan en silencio (Device Owner).
 *
 * "launcher" usa las mismas claves que la managed configuration de
 * Google; null significa que la política no incluye Titan Launcher y
 * se retira la configuración anterior.
 */
class ApplyLauncherPolicyCommandHandler(
    private val context: Context
) : CommandHandler {

    private val json = Json { ignoreUnknownKeys = true }

    override fun supports(commandType: String): Boolean =
        commandType.equals(COMMAND_TYPE, ignoreCase = true)

    override suspend fun execute(command: DeviceCommandDto): CommandExecutionResult {
        val payload = runCatching {
            json.parseToJsonElement(command.payloadJson) as? JsonObject
        }.getOrNull()
            ?: return failure("INVALID_PAYLOAD", "El payload de la política no es un objeto JSON.")

        val policyVersion = payload["policyVersion"]?.jsonPrimitive?.intOrNull
        val restrictions = when (val launcher = payload["launcher"]) {
            null, JsonNull -> emptyMap()
            is JsonObject -> toRestrictions(launcher)
            else -> return failure("INVALID_PAYLOAD", "La configuración del launcher no es válida.")
        }

        // Se valida antes de guardar: una configuración inválida no
        // reemplaza a la que el equipo ya tiene aplicada.
        val policy = TitanLauncherPolicyReader.read(restrictions)
        if (restrictions.isNotEmpty() && policy.state == TitanLauncherPolicyState.Invalid) {
            return failure(
                "INVALID_LAUNCHER_POLICY",
                policy.error ?: "La configuración del Titan Launcher no es válida."
            )
        }

        TitanServerLauncherStore.save(context, restrictions)

        // Bloqueos de la política (HOME, barra de estado, teclas).
        val kiosk = runCatching { KioskController.apply(context) }.getOrNull()

        val apps = PolicyAppInstaller(context).apply(PolicyApp.parseList(payload["applications"]))

        val result = buildJsonObject {
            put("commandId", command.commandId)
            put("commandType", COMMAND_TYPE)
            payload["policyId"]?.jsonPrimitive?.contentOrNull?.let { put("policyId", it) }
            policyVersion?.let { put("policyVersion", it) }
            put("launcherConfigured", restrictions.isNotEmpty())
            put("launcherState", policy.state.name)
            policy.configVersion?.let { put("launcherConfigVersion", it) }
            kiosk?.let {
                put("deviceOwner", it.deviceOwner)
                put("statusBarLocked", it.statusBarLocked)
                put("systemKeysLocked", it.systemKeysLocked)
                put("defaultHome", it.defaultHome)
            }
            put("applications", apps.toJson())
            put("appliedAtUtc", System.currentTimeMillis())
        }

        if (!apps.allSatisfied) {
            val detail = apps.failures().joinToString("; ") { "${it.packageName}: ${it.message ?: it.code}" }
            return CommandExecutionResult.Failure(
                errorCode = "POLICY_APPS_FAILED",
                errorMessage = "Política aplicada, pero faltan apps: $detail",
                resultJson = result.toString()
            )
        }

        return CommandExecutionResult.Success(resultJson = result.toString())
    }

    private fun toRestrictions(launcher: JsonObject): Map<String, String> =
        launcher.mapNotNull { (key, value) -> flatten(value)?.let { key to it } }.toMap()

    /** Mismo aplanado que la managed configuration: listas/objetos como JSON. */
    private fun flatten(value: JsonElement): String? = when (value) {
        JsonNull -> null
        is JsonPrimitive -> value.contentOrNull
        is JsonObject, is JsonArray -> value.toString()
    }

    private fun failure(code: String, message: String) =
        CommandExecutionResult.Failure(errorCode = code, errorMessage = message)

    companion object {
        const val COMMAND_TYPE = "APPLY_LAUNCHER_POLICY"
    }
}
