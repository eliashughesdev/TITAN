package com.titanmdm.agent.commands.handlers

import com.titanmdm.agent.commands.CommandExecutionResult
import com.titanmdm.agent.core.network.DeviceCommandDto

interface CommandHandler {

    fun supports(
        commandType: String
    ): Boolean

    suspend fun execute(
        command: DeviceCommandDto
    ): CommandExecutionResult
}