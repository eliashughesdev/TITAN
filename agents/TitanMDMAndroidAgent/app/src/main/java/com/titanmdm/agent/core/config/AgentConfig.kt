package com.titanmdm.agent.core.config

import com.titanmdm.agent.BuildConfig

object AgentConfig {
    const val AGENT_NAME = "TitanMDM Android Agent"
    /** Versión real del APK (build.gradle.kts): la que ve la consola. */
    const val AGENT_VERSION = BuildConfig.AGENT_VERSION
    const val AGENT_VERSION_CODE = BuildConfig.VERSION_CODE
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
    /**
     * Servidor activo: el recibido por QR (ServerEndpointStore) o el del build.
     */
    fun normalizedBaseUrl(): String {
        val raw = ServerEndpointStore.current() ?: BuildConfig.TITAN_API_BASE_URL
        return validateServerUrl(raw).getOrThrow()
    }

    fun validateServerUrl(raw: String): Result<String> =
        ServerEndpointPolicy.normalize(
            raw = raw,
            allowLanHttp = BuildConfig.ALLOW_LAN_HTTP,
            debug = BuildConfig.DEBUG
        )
}