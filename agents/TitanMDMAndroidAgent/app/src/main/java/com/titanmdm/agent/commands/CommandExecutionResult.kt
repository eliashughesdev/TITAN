package com.titanmdm.agent.commands

sealed interface CommandExecutionResult {

    data class Success(
        val resultJson: String? = null
    ) : CommandExecutionResult

    data class Failure(
        val errorCode: String,
        val errorMessage: String,
        val resultJson: String? = null
    ) : CommandExecutionResult
}