package com.titanmdm.agent.commands.handlers

import android.content.Context
import com.titanmdm.agent.commands.CommandExecutionResult
import com.titanmdm.agent.core.network.DeviceCommandDto
import com.titanmdm.agent.kiosk.KioskController
import com.titanmdm.agent.lock.LockRequest
import com.titanmdm.agent.lock.LockScreenController
import com.titanmdm.agent.workers.GeofenceSyncWorker
import kotlinx.serialization.json.Json
import kotlinx.serialization.json.JsonObject
import kotlinx.serialization.json.booleanOrNull
import kotlinx.serialization.json.buildJsonObject
import kotlinx.serialization.json.jsonPrimitive
import kotlinx.serialization.json.put

/**
 * Control del equipo desde la consola / automatizaciones:
 *  - LOCK_DEVICE: bloquea la pantalla (acción "Bloquear equipo", p. ej.
 *    en una regla GeofenceExit). Con {"pin", "message", "phone"} activa el
 *    modo extravío: PIN temporal y mensaje corporativo persistentes.
 *  - UNLOCK_DEVICE: retira el modo extravío (mensaje y PIN temporal).
 *  - KIOSK_MODE {"enabled": true|false}: el técnico libera el equipo
 *    (barra de estado y HOME) y lo vuelve a bloquear.
 */
class DeviceControlCommandHandler(
    private val context: Context
) : CommandHandler {

    override fun supports(commandType: String): Boolean =
        commandType.equals(LOCK_DEVICE, ignoreCase = true) ||
            commandType.equals(UNLOCK_DEVICE, ignoreCase = true) ||
            commandType.equals(KIOSK_MODE, ignoreCase = true) ||
            commandType.equals(GEOFENCE_SYNC, ignoreCase = true)

    override suspend fun execute(command: DeviceCommandDto): CommandExecutionResult =
        when {
            command.commandType.equals(LOCK_DEVICE, ignoreCase = true) -> lock(command)
            command.commandType.equals(UNLOCK_DEVICE, ignoreCase = true) -> unlock(command)
            command.commandType.equals(GEOFENCE_SYNC, ignoreCase = true) -> geofences(command)
            else -> kiosk(command)
        }

    /** GEOFENCE_SYNC: vuelve a descargar y registrar las geocercas asignadas. */
    private suspend fun geofences(command: DeviceCommandDto): CommandExecutionResult {
        val synced = runCatching {
            GeofenceSyncWorker.syncGeofences(context, force = true)
        }.getOrNull()

        if (synced != true) {
            return CommandExecutionResult.Failure(
                errorCode = "GEOFENCE_SYNC_FAILED",
                errorMessage = "No fue posible actualizar las geocercas (permiso de ubicación o red)."
            )
        }
        return CommandExecutionResult.Success(
            resultJson = buildJsonObject {
                put("commandId", command.commandId)
                put("commandType", GEOFENCE_SYNC)
                put("synced", true)
            }.toString()
        )
    }

    private fun lock(command: DeviceCommandDto): CommandExecutionResult {
        val request = when (val parsed = LockRequest.parse(command.payloadJson)) {
            is LockRequest.Parsed.Invalid -> return CommandExecutionResult.Failure(
                errorCode = parsed.code,
                errorMessage = parsed.message
            )
            is LockRequest.Parsed.Valid -> parsed.request
        }

        val outcome = runCatching { LockScreenController.lock(context, request) }
            .getOrElse {
                LockScreenController.Outcome(false, "LOCK_FAILED", it.message ?: "No fue posible bloquear el equipo.")
            }

        val result = buildJsonObject {
            put("commandId", command.commandId)
            put("commandType", LOCK_DEVICE)
            put("locked", outcome.locked)
            put("lostMode", request.isLostMode)
            put("pinApplied", outcome.pinApplied)
            put("messageShown", outcome.messageShown)
            put("lockedAtUtc", System.currentTimeMillis())
        }.toString()

        if (!outcome.success) {
            return CommandExecutionResult.Failure(
                errorCode = outcome.errorCode ?: "LOCK_FAILED",
                errorMessage = outcome.errorMessage ?: "No fue posible bloquear el equipo.",
                resultJson = result
            )
        }
        return CommandExecutionResult.Success(resultJson = result)
    }

    /** UNLOCK_DEVICE: retira el modo extravío (mensaje y PIN temporal). */
    private fun unlock(command: DeviceCommandDto): CommandExecutionResult {
        val outcome = runCatching { LockScreenController.clear(context) }
            .getOrElse {
                LockScreenController.Outcome(false, "UNLOCK_FAILED", it.message ?: "No fue posible quitar el bloqueo.")
            }
        if (!outcome.success) {
            return CommandExecutionResult.Failure(
                errorCode = outcome.errorCode ?: "UNLOCK_FAILED",
                errorMessage = outcome.errorMessage ?: "No fue posible quitar el bloqueo."
            )
        }
        return CommandExecutionResult.Success(
            resultJson = buildJsonObject {
                put("commandId", command.commandId)
                put("commandType", UNLOCK_DEVICE)
                put("lostMode", false)
                put("unlockedAtUtc", System.currentTimeMillis())
            }.toString()
        )
    }

    private fun kiosk(command: DeviceCommandDto): CommandExecutionResult {
        val enabled = runCatching {
            (Json.parseToJsonElement(command.payloadJson) as? JsonObject)
                ?.get("enabled")?.jsonPrimitive?.booleanOrNull
        }.getOrNull()
            ?: return CommandExecutionResult.Failure(
                errorCode = "INVALID_PAYLOAD",
                errorMessage = "KIOSK_MODE requiere {\"enabled\": true|false}."
            )

        val status = KioskController.setKioskEnabled(context, enabled)
        if (!status.deviceOwner) {
            return CommandExecutionResult.Failure(
                errorCode = "DEVICE_OWNER_REQUIRED",
                errorMessage = "El modo kiosco requiere que TitanMDM Agent sea Device Owner."
            )
        }

        return CommandExecutionResult.Success(
            resultJson = buildJsonObject {
                put("commandId", command.commandId)
                put("commandType", KIOSK_MODE)
                put("kioskEnabled", status.kioskEnabled)
                put("statusBarLocked", status.statusBarLocked)
                put("defaultHome", status.defaultHome)
            }.toString()
        )
    }

    companion object {
        const val LOCK_DEVICE = "LOCK_DEVICE"
        const val UNLOCK_DEVICE = "UNLOCK_DEVICE"
        const val KIOSK_MODE = "KIOSK_MODE"
        const val GEOFENCE_SYNC = "GEOFENCE_SYNC"
    }
}
