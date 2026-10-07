package com.titanmdm.agent.commands.handlers

import android.content.Context
import com.titanmdm.agent.apps.AppInventoryProvider
import com.titanmdm.agent.commands.CommandExecutionResult
import com.titanmdm.agent.core.network.DeviceCommandDto
import kotlinx.serialization.encodeToString
import kotlinx.serialization.json.Json
import kotlinx.serialization.json.JsonArray
import kotlinx.serialization.json.JsonObject
import kotlinx.serialization.json.JsonPrimitive
import kotlinx.serialization.json.encodeToJsonElement

class AppInventoryCommandHandler(
    context: Context
) : CommandHandler {

    private val applicationContext =
        context.applicationContext

    private val inventoryProvider =
        AppInventoryProvider(
            applicationContext
        )

    private val json =
        Json {
            encodeDefaults = true
            explicitNulls = false
            ignoreUnknownKeys = true
        }

    override fun supports(
        commandType: String
    ): Boolean {

        return commandType.equals(
            "APP_INVENTORY",
            ignoreCase = true
        )
    }

    override suspend fun execute(
        command: DeviceCommandDto
    ): CommandExecutionResult {

        return try {

            val applications =
                inventoryProvider.collect()

            val applicationsElement =
                json.encodeToJsonElement(
                    applications
                )

            val result =
                JsonObject(
                    mapOf(
                        "commandId" to
                                JsonPrimitive(
                                    command.commandId
                                ),

                        "commandType" to
                                JsonPrimitive(
                                    command.commandType
                                ),

                        "platform" to
                                JsonPrimitive(
                                    "Android"
                                ),

                        "applicationCount" to
                                JsonPrimitive(
                                    applications.size
                                ),

                        "applications" to
                                applicationsElement
                    )
                )

            CommandExecutionResult.Success(
                resultJson =
                    result.toString()
            )

        } catch (exception: Exception) {

            CommandExecutionResult.Failure(
                errorCode =
                    "APP_INVENTORY_FAILED",

                errorMessage =
                    exception.message
                        ?: "No fue posible obtener el inventario de aplicaciones Android."
            )
        }
    }
}