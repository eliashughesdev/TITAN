package com.titanmdm.agent.lock

import kotlinx.serialization.json.Json
import kotlinx.serialization.json.JsonObject
import kotlinx.serialization.json.JsonPrimitive
import kotlinx.serialization.json.contentOrNull

/**
 * Payload de LOCK_DEVICE: { "pin": "1234", "message": "...", "phone": "..." },
 * todos opcionales. Sin PIN ni mensaje es un bloqueo de pantalla simple.
 * Lógica pura, probada en unit tests.
 */
data class LockRequest(
    val pin: String?,
    val message: String?,
    val phone: String?
) {
    val isLostMode: Boolean get() = pin != null || message != null

    /** Texto para la pantalla de bloqueo del sistema (mensaje + teléfono). */
    fun lockScreenText(): String? {
        if (message == null && phone == null) return null
        return listOfNotNull(message, phone?.let { "Tel. $it" }).joinToString("\n")
    }

    sealed interface Parsed {
        data class Valid(val request: LockRequest) : Parsed
        data class Invalid(val code: String, val message: String) : Parsed
    }

    companion object {
        const val MIN_PIN = 4
        const val MAX_PIN = 16
        const val MAX_MESSAGE = 240
        const val MAX_PHONE = 32

        fun parse(payloadJson: String?): Parsed {
            val payload = runCatching {
                payloadJson?.takeIf { it.isNotBlank() }?.let { Json.parseToJsonElement(it) as? JsonObject }
            }.getOrNull()

            fun text(name: String): String? =
                (payload?.get(name) as? JsonPrimitive)
                    ?.takeIf { it.isString }
                    ?.contentOrNull
                    ?.trim()
                    ?.takeIf { it.isNotEmpty() }

            val pin = text("pin")
            if (pin != null && (pin.length !in MIN_PIN..MAX_PIN || pin.any { it !in '0'..'9' })) {
                return Parsed.Invalid("INVALID_PIN", "El PIN temporal debe tener entre $MIN_PIN y $MAX_PIN dígitos.")
            }

            val message = text("message")?.filterNot { it.isISOControl() && it != '\n' }?.take(MAX_MESSAGE)
            val phone = text("phone")?.filter { it.isDigit() || it in "+-() " }?.trim()?.take(MAX_PHONE)
                ?.takeIf { it.isNotEmpty() }

            return Parsed.Valid(LockRequest(pin, message, phone))
        }
    }
}
