package com.titanmdm.agent.commands

import android.content.Context
import com.titanmdm.agent.commands.handlers.AppInventoryCommandHandler
import com.titanmdm.agent.commands.handlers.CommandHandler
import com.titanmdm.agent.commands.handlers.ComplianceCheckCommandHandler
import com.titanmdm.agent.commands.handlers.DeviceInfoCommandHandler
import com.titanmdm.agent.commands.handlers.LocationCommandHandler
import com.titanmdm.agent.commands.handlers.LostModeCommandHandler
import com.titanmdm.agent.commands.handlers.PingCommandHandler
import com.titanmdm.agent.commands.handlers.SecurityStatusCommandHandler
import com.titanmdm.agent.core.network.DeviceCommandDto

class CommandDispatcher(
    context: Context
) {

    private val applicationContext =
        context.applicationContext

    private val handlers:
            List<CommandHandler> =
        listOf(
            PingCommandHandler(),

            DeviceInfoCommandHandler(
                applicationContext
            ),

            AppInventoryCommandHandler(
                applicationContext
            ),

            SecurityStatusCommandHandler(
                applicationContext
            ),

            ComplianceCheckCommandHandler(
                applicationContext
            ),

            LocationCommandHandler(
                applicationContext
            ),

            LostModeCommandHandler(
                applicationContext
            )
        )

    suspend fun dispatch(
        command: DeviceCommandDto
    ): CommandExecutionResult {

        val commandType =
            command.commandType.trim()

        if (commandType.isBlank()) {
            return CommandExecutionResult.Failure(
                errorCode =
                    "INVALID_COMMAND_TYPE",

                errorMessage =
                    "El comando recibido no contiene CommandType."
            )
        }

        val handler =
            handlers.firstOrNull {
                it.supports(
                    commandType
                )
            }

        if (handler == null) {
            return CommandExecutionResult.Failure(
                errorCode =
                    "COMMAND_NOT_SUPPORTED",

                errorMessage =
                    "El agente Android no soporta el comando '$commandType'."
            )
        }

        return try {
            handler.execute(
                command
            )
        } catch (
            exception: Exception
        ) {
            CommandExecutionResult.Failure(
                errorCode =
                    "COMMAND_EXECUTION_EXCEPTION",

                errorMessage =
                    exception.message
                        ?: "Error inesperado ejecutando '$commandType'."
            )
        }
    }
}