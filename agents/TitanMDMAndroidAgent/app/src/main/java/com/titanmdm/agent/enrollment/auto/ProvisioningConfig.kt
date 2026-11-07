package com.titanmdm.agent.enrollment.auto

/**
 * Datos de inscripción automática recibidos en
 * PROVISIONING_ADMIN_EXTRAS_BUNDLE del QR de fábrica (6 toques):
 *
 *   "server_url":       "http://172.21.20.14:8020"
 *   "enrollment_token": "<token de la consola>"
 *
 * Con ambos, el agente se registra solo al terminar el asistente, sin
 * que nadie abra la app ni escanee un segundo QR.
 */
data class ProvisioningConfig(
    val serverUrl: String?,
    val enrollmentToken: String?
) {
    val canAutoEnroll: Boolean
        get() = !serverUrl.isNullOrBlank() && !enrollmentToken.isNullOrBlank()

    companion object {
        const val KEY_SERVER_URL = "server_url"
        const val KEY_ENROLLMENT_TOKEN = "enrollment_token"

        private const val MIN_TOKEN_LENGTH = 16
        private const val MAX_TOKEN_LENGTH = 512
        private val tokenPattern = Regex("^[A-Za-z0-9._~+/=-]+$")

        /** [values]: claves del bundle como texto (null si faltan). */
        fun from(values: Map<String, String?>): ProvisioningConfig =
            ProvisioningConfig(
                serverUrl = values[KEY_SERVER_URL]?.trim()?.takeIf { it.isNotEmpty() },
                enrollmentToken = values[KEY_ENROLLMENT_TOKEN]?.trim()?.takeIf(::isValidToken)
            )

        fun isValidToken(token: String): Boolean =
            token.length in MIN_TOKEN_LENGTH..MAX_TOKEN_LENGTH && tokenPattern.matches(token)
    }
}
