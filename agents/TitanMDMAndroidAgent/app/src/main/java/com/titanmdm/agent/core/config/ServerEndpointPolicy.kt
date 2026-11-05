package com.titanmdm.agent.core.config

import java.net.URI

/**
 * Valida la URL del servidor TitanMDM recibida por QR.
 *
 * - HTTPS: siempre permitido.
 * - HTTP: solo hacia hosts de red privada (LAN corporativa) y solo si el
 *   build lo habilita (ALLOW_LAN_HTTP), o el emulador en debug.
 *   Nunca HTTP hacia Internet: el secreto del dispositivo viajaria expuesto.
 */
object ServerEndpointPolicy {

    private val emulatorHosts = setOf("10.0.2.2", "localhost", "127.0.0.1")
    private val privateSuffixes = listOf(".local", ".lan", ".internal", ".corp", ".home.arpa")

    fun normalize(
        raw: String,
        allowLanHttp: Boolean,
        debug: Boolean
    ): Result<String> = runCatching {
        val trimmed = raw.trim()
        require(trimmed.isNotEmpty()) { "Servidor TitanMDM no configurado. Escanee el QR de inscripción." }

        val url = if (trimmed.endsWith('/')) trimmed else "$trimmed/"
        val uri = URI(url)
        val host = uri.host?.lowercase()

        require(!host.isNullOrBlank() && uri.userInfo == null && uri.fragment == null && uri.rawQuery == null) {
            "La URL del servidor TitanMDM no es válida."
        }

        when (uri.scheme?.lowercase()) {
            "https" -> Unit
            "http" -> require(
                (debug && host in emulatorHosts) || (allowLanHttp && isPrivateHost(host))
            ) {
                if (allowLanHttp) {
                    "HTTP solo se permite hacia servidores de la red interna. Use HTTPS."
                } else {
                    "Este agente exige HTTPS. Use un certificado interno o un build con red local habilitada."
                }
            }
            else -> throw IllegalArgumentException("La URL del servidor debe usar https:// o http://.")
        }

        url
    }

    fun isPrivateHost(host: String): Boolean {
        val value = host.lowercase().trim('[', ']')
        parseIpv4(value)?.let { (a, b) ->
            return a == 10 || a == 127 ||
                (a == 172 && b in 16..31) ||
                (a == 192 && b == 168) ||
                (a == 169 && b == 254)
        }
        if (value == "localhost") return true
        // Nombres NetBIOS / DNS interno sin dominio publico.
        if (!value.contains('.') && !value.contains(':')) return true
        return privateSuffixes.any { value.endsWith(it) }
    }

    private fun parseIpv4(host: String): Pair<Int, Int>? {
        val parts = host.split('.')
        if (parts.size != 4) return null
        val numbers = parts.map { it.toIntOrNull() ?: return null }
        if (numbers.any { it !in 0..255 }) return null
        return numbers[0] to numbers[1]
    }
}
