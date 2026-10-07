package com.titanmdm.agent.commands.handlers

import android.content.Context
import com.titanmdm.agent.commands.CommandExecutionResult
import com.titanmdm.agent.core.network.DeviceCommandDto
import com.titanmdm.agent.security.SecurityPostureProvider
import kotlinx.serialization.encodeToString
import kotlinx.serialization.json.Json

class ComplianceCheckCommandHandler(
    context: Context
) : CommandHandler {

    private val provider =
        SecurityPostureProvider(
            context.applicationContext
        )

    private val json =
        Json {
            encodeDefaults = true
            explicitNulls = true
        }

    override fun supports(
        commandType: String
    ): Boolean {

        return commandType.equals(
            "COMPLIANCE_CHECK",
            ignoreCase = true
        )
    }

    override suspend fun execute(
        command: DeviceCommandDto
    ): CommandExecutionResult {

        val assessment =
            provider.evaluateCompliance()

        return CommandExecutionResult.Success(
            resultJson =
                json.encodeToString(
                    assessment
                )
        )
    }
}