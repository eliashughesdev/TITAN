package com.titanmdm.agent.enrollment

import kotlinx.serialization.json.Json
import kotlinx.serialization.json.JsonObject
import kotlinx.serialization.json.contentOrNull
import kotlinx.serialization.json.jsonPrimitive
import java.net.URI
import java.net.URLDecoder
import java.nio.charset.StandardCharsets

/**
 * Datos de inscripcion interna (sin Android Enterprise / DUNS).
 *
 * Formatos aceptados (QR de la consola, lector Honeywell o teclado):
 *  - titanmdm://enroll?v=1&server=<url>&token=<token>
 *  - {"server":"<url>","token":"<token>"}
 *  - <token> solo (usa el servidor ya configurado en el agente)
 */
data class EnrollmentPayload(
    val serverUrl: String?,
    val token: String
)

object EnrollmentPayloadParser {

    const val SCHEME = "titanmdm"
    const val HOST = "enroll"

    private const val MIN_TOKEN_LENGTH = 16
    private const val MAX_TOKEN_LENGTH = 512

    private val json = Json { ignoreUnknownKeys = true }

    // La consola emite 64 hex; se toleran caracteres URL-safe/base64.
    private val tokenPattern = Regex("^[A-Za-z0-9._~+/=-]+$")

    fun parse(raw: String?): EnrollmentPayload? {
        // Los lectores en modo teclado suelen agregar \r, \n o \t al final.
        val text = raw?.trim()?.trim('\u0000') ?: return null
        if (text.isEmpty()) return null

        return when {
            text.startsWith("$SCHEME://", ignoreCase = true) -> parseUri(text)
            text.startsWith("{") -> parseJson(text)
            else -> plainToken(text)
        }
    }

    private fun parseUri(text: String): EnrollmentPayload? {
        val uri = runCatching { URI(text) }.getOrNull() ?: return null
        if (!uri.host.equals(HOST, ignoreCase = true)) return null

        val query = parseQuery(uri.rawQuery)
        val token = query["token"]?.let(::validToken) ?: return null
        return EnrollmentPayload(query["server"]?.takeIf { it.isNotBlank() }, token)
    }

    private fun parseJson(text: String): EnrollmentPayload? {
        val obj = runCatching { json.parseToJsonElement(text) as? JsonObject }.getOrNull() ?: return null
        fun field(name: String) = runCatching { obj[name]?.jsonPrimitive?.contentOrNull }.getOrNull()

        val token = field("token")?.let(::validToken) ?: return null
        return EnrollmentPayload(field("server")?.trim()?.takeIf { it.isNotEmpty() }, token)
    }

    private fun plainToken(text: String): EnrollmentPayload? =
        validToken(text)?.let { EnrollmentPayload(null, it) }

    private fun validToken(value: String): String? {
        val token = value.trim()
        val ok = token.length in MIN_TOKEN_LENGTH..MAX_TOKEN_LENGTH && tokenPattern.matches(token)
        return token.takeIf { ok }
    }

    private fun parseQuery(rawQuery: String?): Map<String, String> {
        if (rawQuery.isNullOrEmpty()) return emptyMap()
        return rawQuery.split('&')
            .mapNotNull { part ->
                val index = part.indexOf('=')
                if (index <= 0) return@mapNotNull null
                val key = decode(part.substring(0, index)).lowercase()
                key to decode(part.substring(index + 1))
            }
            .toMap()
    }

    private fun decode(value: String): String =
        URLDecoder.decode(value, StandardCharsets.UTF_8.name())
}
