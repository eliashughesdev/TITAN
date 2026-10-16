package com.titanmdm.agent.launcher

import java.net.IDN
import java.net.InetAddress
import java.net.URI

/**
 * Valida destinos de fondo y logotipo. Rechaza file:, content:, http:,
 * credenciales y direcciones internas. No descarga nada.
 */
object TitanLauncherResourceUrl {

    private val allowedExtensions = setOf("png", "jpg", "jpeg", "webp")
    private val blockedSuffixes = setOf(
        "localhost",
        "local",
        "internal",
        "localdomain",
        "lan",
        "home",
        "home.arpa",
        "intranet"
    )

    fun normalize(raw: String?): String? {
        val value = raw?.trim().orEmpty()
        if (value.isEmpty() || value.length > 2048) return null
        if (value.contains('\\') || value.contains('@') || value.contains(' ')) return null
        if (value.startsWith("data:", ignoreCase = true) ||
            value.contains("base64", ignoreCase = true)
        ) {
            return null
        }

        val uri = try {
            URI(value)
        } catch (_: Exception) {
            return null
        }

        if (!uri.isAbsolute || !uri.scheme.equals("https", ignoreCase = true)) return null
        if (!uri.userInfo.isNullOrEmpty()) return null
        val host = uri.host?.trim()?.trimEnd('.')?.lowercase().orEmpty()
        if (host.isEmpty() || host.all { it.isDigit() } || !isPublicHost(host)) return null

        val path = try {
            java.net.URLDecoder.decode(uri.rawPath ?: uri.path.orEmpty(), Charsets.UTF_8)
        } catch (_: Exception) {
            return null
        }
        val extension = path.substringAfterLast('.', "").lowercase()
        if (extension !in allowedExtensions) return null

        val port = if (uri.port == -1) "" else ":${uri.port}"
        val query = if (uri.rawQuery.isNullOrEmpty()) "" else "?${uri.rawQuery}"
        return "https://$host$port$path$query"
    }

    fun isPublicHost(host: String): Boolean {
        val normalized = IDN.toASCII(host).trim().trimEnd('.').lowercase()
        if (normalized.isEmpty()) return false
        if (normalized == "metadata" || normalized == "metadata.google.internal") return false
        if (normalized.endsWith(".nip.io") || normalized.endsWith(".sslip.io")) return false
        if (blockedSuffixes.any { normalized == it || normalized.endsWith(".$it") }) {
            return false
        }

        val looksLikeIp = normalized.contains(':') ||
            normalized.count { it == '.' } == 3 &&
            normalized.all { it.isDigit() || it == '.' }
        val address = if (looksLikeIp) {
            runCatching { InetAddress.getByName(normalized) }.getOrNull()
        } else {
            null
        }
        if (looksLikeIp) {
            if (address == null ||
                address.isAnyLocalAddress || address.isLoopbackAddress ||
                address.isLinkLocalAddress || address.isSiteLocalAddress ||
                address.isMulticastAddress
            ) {
                return false
            }
            val bytes = address.address
            if (bytes.size == 4 && isPrivateOrReservedIPv4(bytes)) return false
            if (bytes.size == 16) {
                val first = bytes[0].toInt() and 0xFF
                if (first == 0xFC || first == 0xFD || first == 0xFE || first == 0xFF) {
                    return false
                }
            }
        }
        return normalized.contains('.')
    }

    private fun isPrivateOrReservedIPv4(bytes: ByteArray): Boolean {
        val a = bytes[0].toInt() and 0xFF
        val b = bytes[1].toInt() and 0xFF
        val c = bytes[2].toInt() and 0xFF
        return a == 0 || a == 10 || a == 127 ||
            (a == 100 && b in 64..127) ||
            (a == 169 && b == 254) ||
            (a == 172 && b in 16..31) ||
            (a == 192 && b == 168) ||
            (a == 192 && b == 0 && c == 2) ||
            (a == 198 && (b == 18 || b == 19 || b == 51)) ||
            (a == 203 && b == 0 && c == 113) ||
            a >= 224
    }
}
