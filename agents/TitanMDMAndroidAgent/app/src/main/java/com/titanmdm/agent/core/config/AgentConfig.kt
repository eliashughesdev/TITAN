package com.titanmdm.agent.core.config

object AgentConfig {

    const val AGENT_NAME =
        "TitanMDM Android Agent"

    const val AGENT_VERSION =
        "1.0.0"

    const val AGENT_VERSION_CODE = 1

    const val PLATFORM =
        "Android"

    /*
     * ========================================================
     * API
     * ========================================================
     *
     * Android Emulator:
     *
     * 10.0.2.2 apunta al localhost del host Windows.
     *
     * Producción:
     * esta dirección será sustituida posteriormente por
     * configuración segura/administrada.
     */

    const val API_BASE_URL =
        "http://10.0.2.2:8020/"

    const val CONNECT_TIMEOUT_SECONDS =
        30L

    const val READ_TIMEOUT_SECONDS =
        30L

    const val WRITE_TIMEOUT_SECONDS =
        30L


    /*
     * ========================================================
     * HEARTBEAT
     * ========================================================
     */

    const val HEARTBEAT_INTERVAL_MINUTES =
        15L

    const val HEARTBEAT_WORK_NAME =
        "titanmdm_periodic_heartbeat"


    /*
     * ========================================================
     * COMMAND ENGINE
     * ========================================================
     *
     * WorkManager impone un mínimo de 15 minutos para
     * PeriodicWorkRequest.
     *
     * El canal rápido de comandos se incorporará posteriormente
     * sin utilizar polling agresivo.
     */

    const val COMMAND_POLL_INTERVAL_MINUTES =
        15L

    const val COMMAND_WORK_NAME =
        "titanmdm_periodic_commands"


    /*
     * ========================================================
     * RETRY
     * ========================================================
     */

    const val REGISTRATION_MAX_RETRIES =
        3

    const val NETWORK_RETRY_DELAY_MS =
        2_000L


    /*
     * ========================================================
     * HTTP CLIENT
     * ========================================================
     */

    const val USER_AGENT =
        "TitanMDM-Android-Agent/$AGENT_VERSION"


    /*
     * ========================================================
     * COMPATIBILITY PROPERTIES
     * ========================================================
     */

    const val agentName =
        AGENT_NAME

    const val agentVersion =
        AGENT_VERSION

    const val agentVersionCode =
        AGENT_VERSION_CODE

    const val platform =
        PLATFORM

    const val heartbeatIntervalMinutes =
        HEARTBEAT_INTERVAL_MINUTES

    const val commandPollIntervalMinutes =
        COMMAND_POLL_INTERVAL_MINUTES


    fun normalizedBaseUrl(): String {

        return if (
            API_BASE_URL.endsWith("/")
        ) {

            API_BASE_URL

        } else {

            "$API_BASE_URL/"
        }
    }
}