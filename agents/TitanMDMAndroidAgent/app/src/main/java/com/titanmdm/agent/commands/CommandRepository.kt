package com.titanmdm.agent.commands

import android.content.Context
import com.titanmdm.agent.core.network.CommandFailedRequest
import com.titanmdm.agent.core.network.CommandSuccessRequest
import com.titanmdm.agent.core.network.DeviceCommandDto
import com.titanmdm.agent.core.network.TitanApiClient
import com.titanmdm.agent.core.storage.AgentIdentity
import com.titanmdm.agent.core.storage.AgentIdentityStore

sealed interface CommandPollingResult {

    data class Success(
        val processedCommands: Int
    ) : CommandPollingResult

    data object NotEnrolled :
        CommandPollingResult

    data class Failure(
        val message: String,
        val retryable: Boolean
    ) : CommandPollingResult
}

class CommandRepository(
    context: Context
) {

    private val appContext =
        context.applicationContext

    private val identityStore =
        AgentIdentityStore(appContext)

    private val dispatcher =
        CommandDispatcher(appContext)

    suspend fun pollAndExecute():
            CommandPollingResult {

        val identity =
            identityStore.get()
                ?: return CommandPollingResult
                    .NotEnrolled

        return try {

            val response =
                TitanApiClient.service
                    .getPendingCommands(
                        deviceId =
                            identity.deviceId,

                        deviceSecret =
                            identity.deviceSecret
                    )

            if (!response.isSuccessful) {

                return httpFailure(
                    operation =
                        "consultar comandos",

                    statusCode =
                        response.code()
                )
            }

            val commands =
                response.body()
                    ?: emptyList()

            var processedCommands = 0

            for (command in commands) {

                processCommand(
                    identity = identity,
                    command = command
                )

                processedCommands++
            }

            CommandPollingResult.Success(
                processedCommands =
                    processedCommands
            )

        } catch (exception: Exception) {

            CommandPollingResult.Failure(
                message =
                    exception.message
                        ?: "Error de comunicación con el motor de comandos TitanMDM.",

                retryable = true
            )
        }
    }

    private suspend fun processCommand(
        identity: AgentIdentity,
        command: DeviceCommandDto
    ) {

        /*
         * ----------------------------------------------------
         * DELIVERED
         * ----------------------------------------------------
         */

        val delivered =
            TitanApiClient.service
                .markCommandDelivered(
                    commandId =
                        command.commandId,

                    deviceId =
                        identity.deviceId,

                    deviceSecret =
                        identity.deviceSecret
                )

        if (!delivered.isSuccessful) {

            throw CommandTransportException(
                message =
                    "No fue posible marcar ${command.commandId} como Delivered.",

                retryable =
                    isRetryable(
                        delivered.code()
                    )
            )
        }

        /*
         * ----------------------------------------------------
         * EXECUTING
         * ----------------------------------------------------
         */

        val executing =
            TitanApiClient.service
                .markCommandExecuting(
                    commandId =
                        command.commandId,

                    deviceId =
                        identity.deviceId,

                    deviceSecret =
                        identity.deviceSecret
                )

        if (!executing.isSuccessful) {

            throw CommandTransportException(
                message =
                    "No fue posible marcar ${command.commandId} como Executing.",

                retryable =
                    isRetryable(
                        executing.code()
                    )
            )
        }

        /*
         * ----------------------------------------------------
         * LOCAL EXECUTION
         * ----------------------------------------------------
         */

        when (
            val executionResult =
                dispatcher.dispatch(command)
        ) {

            is CommandExecutionResult.Success -> {

                markSuccess(
                    identity =
                        identity,

                    command =
                        command,

                    result =
                        executionResult
                )
            }

            is CommandExecutionResult.Failure -> {

                markFailed(
                    identity =
                        identity,

                    command =
                        command,

                    result =
                        executionResult
                )
            }
        }
    }

    private suspend fun markSuccess(
        identity: AgentIdentity,
        command: DeviceCommandDto,
        result:
        CommandExecutionResult.Success
    ) {

        val response =
            TitanApiClient.service
                .markCommandSuccess(
                    commandId =
                        command.commandId,

                    deviceId =
                        identity.deviceId,

                    deviceSecret =
                        identity.deviceSecret,

                    request =
                        CommandSuccessRequest(
                            resultJson =
                                result.resultJson
                        )
                )

        if (!response.isSuccessful) {

            throw CommandTransportException(
                message =
                    "El comando ${command.commandId} se ejecutó, pero TitanMDM no confirmó Success.",

                retryable =
                    isRetryable(
                        response.code()
                    )
            )
        }
    }

    private suspend fun markFailed(
        identity: AgentIdentity,
        command: DeviceCommandDto,
        result:
        CommandExecutionResult.Failure
    ) {

        val response =
            TitanApiClient.service
                .markCommandFailed(
                    commandId =
                        command.commandId,

                    deviceId =
                        identity.deviceId,

                    deviceSecret =
                        identity.deviceSecret,

                    request =
                        CommandFailedRequest(
                            errorCode =
                                result.errorCode,

                            errorMessage =
                                result.errorMessage,

                            resultJson =
                                result.resultJson
                        )
                )

        if (!response.isSuccessful) {

            throw CommandTransportException(
                message =
                    "TitanMDM no pudo registrar el fallo del comando ${command.commandId}.",

                retryable =
                    isRetryable(
                        response.code()
                    )
            )
        }
    }

    private fun httpFailure(
        operation: String,
        statusCode: Int
    ): CommandPollingResult.Failure {

        return CommandPollingResult.Failure(
            message =
                "No fue posible $operation. HTTP $statusCode.",

            retryable =
                isRetryable(statusCode)
        )
    }

    private fun isRetryable(
        statusCode: Int
    ): Boolean {

        return statusCode == 408 ||
                statusCode == 429 ||
                statusCode in 500..599
    }

    private class CommandTransportException(
        override val message: String,
        val retryable: Boolean
    ) : Exception(message)
}