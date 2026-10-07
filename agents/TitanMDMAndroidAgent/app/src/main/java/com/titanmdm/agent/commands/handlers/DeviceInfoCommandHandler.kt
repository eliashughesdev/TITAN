package com.titanmdm.agent.commands.handlers

import android.content.Context
import com.titanmdm.agent.commands.CommandExecutionResult
import com.titanmdm.agent.core.config.AgentConfig
import com.titanmdm.agent.core.network.DeviceCommandDto
import com.titanmdm.agent.inventory.DeviceInventoryProvider
import kotlinx.serialization.encodeToString
import kotlinx.serialization.json.Json
import kotlinx.serialization.json.buildJsonObject
import kotlinx.serialization.json.put

class DeviceInfoCommandHandler(
    context: Context
) : CommandHandler {

    private val inventoryProvider =
        DeviceInventoryProvider(
            context.applicationContext
        )

    override fun supports(
        commandType: String
    ): Boolean {

        return commandType.equals(
            "DEVICE_INFO",
            ignoreCase = true
        )
    }

    override suspend fun execute(
        command: DeviceCommandDto
    ): CommandExecutionResult {

        return try {

            val inventory =
                inventoryProvider.collect()

            val result =
                buildJsonObject {

                    put(
                        "deviceName",
                        inventory.deviceName
                    )

                    put(
                        "serialNumber",
                        inventory.serialNumber
                    )

                    put(
                        "manufacturer",
                        inventory.manufacturer
                    )

                    put(
                        "model",
                        inventory.model
                    )

                    put(
                        "operatingSystem",
                        inventory.operatingSystem
                    )

                    put(
                        "operatingSystemVersion",
                        inventory.operatingSystemVersion
                    )

                    put(
                        "apiLevel",
                        inventory.apiLevel
                    )

                    put(
                        "buildNumber",
                        inventory.buildNumber
                    )

                    put(
                        "hardware",
                        inventory.hardware
                    )

                    put(
                        "bootloader",
                        inventory.bootloader
                    )

                    inventory.batteryLevel?.let {
                        put(
                            "batteryLevel",
                            it
                        )
                    }

                    inventory.ipAddress?.let {
                        put(
                            "ipAddress",
                            it
                        )
                    }

                    put(
                        "agentVersion",
                        AgentConfig.AGENT_VERSION
                    )
                }

            CommandExecutionResult.Success(
                resultJson =
                    Json.encodeToString(result)
            )

        } catch (exception: Exception) {

            CommandExecutionResult.Failure(
                errorCode =
                    "DEVICE_INFO_FAILED",

                errorMessage =
                    exception.message
                        ?: "No fue posible recopilar la información del dispositivo."
            )
        }
    }
}