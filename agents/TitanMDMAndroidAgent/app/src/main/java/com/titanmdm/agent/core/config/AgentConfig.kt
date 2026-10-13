package com.titanmdm.agent.core.config

import com.titanmdm.agent.BuildConfig
import java.net.URI

object AgentConfig {
    const val AGENT_NAME = "TitanMDM Android Agent"
    const val AGENT_VERSION = "1.0.0"
    const val AGENT_VERSION_CODE = 1
    const val PLATFORM = "Android"
    const val CONNECT_TIMEOUT_SECONDS = 30L
    const val READ_TIMEOUT_SECONDS = 30L
    const val WRITE_TIMEOUT_SECONDS = 30L
    const val HEARTBEAT_INTERVAL_MINUTES = 15L
    const val HEARTBEAT_WORK_NAME = "titanmdm_periodic_heartbeat"
    const val COMMAND_POLL_INTERVAL_MINUTES = 15L
    const val COMMAND_WORK_NAME = "titanmdm_periodic_commands"
    const val REGISTRATION_MAX_RETRIES = 3
    const val NETWORK_RETRY_DELAY_MS = 2_000L
    const val USER_AGENT = "TitanMDM-Android-Agent/$AGENT_VERSION"
    val agentName get() = AGENT_NAME
    val agentVersion get() = AGENT_VERSION
    val agentVersionCode get() = AGENT_VERSION_CODE
    val platform get() = PLATFORM
    val heartbeatIntervalMinutes get() = HEARTBEAT_INTERVAL_MINUTES
    val commandPollIntervalMinutes get() = COMMAND_POLL_INTERVAL_MINUTES
    fun normalizedBaseUrl(): String {
        val raw = BuildConfig.TITAN_API_BASE_URL.trim()
        require(raw.isNotBlank()) { "TitanMDM API URL not configured" }
        val url = if (raw.endsWith('/')) raw else "$raw/"
        val uri = URI(url)
        require(!uri.host.isNullOrBlank() && uri.userInfo == null && uri.fragment == null) {
            "Invalid TitanMDM API URL"
        }
        val secure = uri.scheme.equals("https", ignoreCase = true)
        val emulatorDebug = BuildConfig.DEBUG && uri.scheme.equals("http", ignoreCase = true) &&
            uri.host in setOf("10.0.2.2", "localhost", "127.0.0.1")
        require(secure || emulatorDebug) { "HTTPS is mandatory outside the local debug emulator" }
        return url
    }
}