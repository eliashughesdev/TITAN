package com.titanmdm.agent.commands.handlers

import android.content.Context
import com.titanmdm.agent.alarm.DeviceAlarmController
import com.titanmdm.agent.commands.CommandExecutionResult
import com.titanmdm.agent.core.network.DeviceCommandDto
import kotlinx.serialization.json.Json
import kotlinx.serialization.json.JsonObject
import kotlinx.serialization.json.intOrNull
import kotlinx.serialization.json.jsonPrimitive
import kotlinx.serialization.json.buildJsonObject
import kotlinx.serialization.json.put

class AlarmCommandHandler(
    private val context: Context
) : CommandHandler {

    override fun supports(commandType: String): Boolean =
        commandType.equals(PLAY_ALARM, ignoreCase = true) ||
            commandType.equals(RING_DEVICE, ignoreCase = true)

    override suspend fun execute(
        command: DeviceCommandDto
    ): CommandExecutionResult {
        val duration =
            runCatching {
                (Json.parseToJsonElement(command.payloadJson) as? JsonObject)
                    ?.get("durationSeconds")
                    ?.jsonPrimitive
                    ?.intOrNull
            }.getOrNull()
                ?: DeviceAlarmController.DEFAULT_DURATION_SECONDS

        val started = DeviceAlarmController.play(
            context,
            duration
        )

        if (!started) {
            return CommandExecutionResult.Failure(
                errorCode = "ALARM_UNAVAILABLE",
                errorMessage = "El dispositivo no tiene un tono de alarma disponible."
            )
        }

        return CommandExecutionResult.Success(
            resultJson = buildJsonObject {
                put("commandId", command.commandId)
                put("commandType", PLAY_ALARM)
                put("ringing", true)
                put("durationSeconds", duration)
            }.toString()
        )
    }

    companion object {
        const val PLAY_ALARM = "PLAY_ALARM"
        const val RING_DEVICE = "RING_DEVICE"
    }
}
