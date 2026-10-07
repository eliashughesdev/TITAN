package com.titanmdm.agent.commands.handlers

import android.content.Context
import com.titanmdm.agent.commands.CommandExecutionResult
import com.titanmdm.agent.core.network.DeviceCommandDto
import kotlinx.serialization.Serializable
import kotlinx.serialization.json.Json

class LostModeCommandHandler(
    context: Context
) : CommandHandler {

    private val preferences =
        context.applicationContext
            .getSharedPreferences(
                "titan_lost_mode",
                Context.MODE_PRIVATE
            )

    override fun supports(
        commandType: String
    ): Boolean {
        return commandType.equals(
            "LOST_MODE_ENABLE",
            ignoreCase = true
        ) ||
                commandType.equals(
                    "LOST_MODE_DISABLE",
                    ignoreCase = true
                )
    }

    override suspend fun execute(
        command: DeviceCommandDto
    ): CommandExecutionResult {

        return if (
            command.commandType.equals(
                "LOST_MODE_ENABLE",
                ignoreCase = true
            )
        ) {
            enable(command)
        } else {
            disable()
        }
    }

    private fun enable(
        command: DeviceCommandDto
    ): CommandExecutionResult {

        val payload =
            try {
                Json.decodeFromString<
                        LostModePayload
                        >(
                    command.payloadJson
                )
            } catch (
                exception: Exception
            ) {
                return CommandExecutionResult.Failure(
                    errorCode =
                        "INVALID_LOST_MODE_PAYLOAD",

                    errorMessage =
                        exception.message
                            ?: "Payload de Lost Mode inválido."
                )
            }

        preferences
            .edit()
            .putBoolean(
                "enabled",
                true
            )
            .putString(
                "message",
                payload.message
            )
            .putString(
                "phoneNumber",
                payload.phoneNumber
            )
            .apply()

        /*
         * Este estado complementa Android Enterprise.
         * No pretende sustituir Device Owner / Android Device Policy.
         */

        return CommandExecutionResult.Success(
            resultJson =
                """{"lostMode":true,"agentState":"enabled"}"""
        )
    }

    private fun disable():
            CommandExecutionResult {

        preferences
            .edit()
            .clear()
            .apply()

        return CommandExecutionResult.Success(
            resultJson =
                """{"lostMode":false,"agentState":"disabled"}"""
        )
    }

    @Serializable
    private data class LostModePayload(
        val message: String,
        val phoneNumber: String? = null
    )
}