package com.titanmdm.agent.commands.handlers

import com.titanmdm.agent.commands.CommandExecutionResult
import com.titanmdm.agent.core.config.AgentConfig
import com.titanmdm.agent.core.network.DeviceCommandDto
import kotlinx.serialization.encodeToString
import kotlinx.serialization.json.Json
import kotlinx.serialization.json.buildJsonObject
import kotlinx.serialization.json.put

class PingCommandHandler :
    CommandHandler {

    override fun supports(
        commandType: String
    ): Boolean {

        return commandType.equals(
            "PING",
            ignoreCase = true
        )
    }

    override suspend fun execute(
        command: DeviceCommandDto
    ): CommandExecutionResult {

        val result =
            buildJsonObject {

                put(
                    "commandId",
                    command.commandId
                )

                put(
                    "commandType",
                    "PING"
                )

                put(
                    "response",
                    "PONG"
                )

                put(
                    "agent",
                    AgentConfig.AGENT_NAME
                )

                put(
                    "agentVersion",
                    AgentConfig.AGENT_VERSION
                )

                put(
                    "platform",
                    AgentConfig.PLATFORM
                )

                put(
                    "timestampUtc",
                    System.currentTimeMillis()
                )
            }

        return CommandExecutionResult.Success(
            resultJson =
                Json.encodeToString(result)
        )
    }
}