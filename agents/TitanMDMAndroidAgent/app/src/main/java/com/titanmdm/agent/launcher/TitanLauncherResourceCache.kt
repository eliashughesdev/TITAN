package com.titanmdm.agent.launcher

import java.io.File
import java.security.MessageDigest

data class TitanLauncherCachedResource(
    val url: String,
    val bytes: ByteArray,
    val format: TitanLauncherImageFormat,
    val sha256: String
)

/**
 * Caché privada del launcher. No es el fondo de pantalla del sistema.
 */
class TitanLauncherResourceCache(
    private val directory: File
) {
    fun read(url: String, kind: String): TitanLauncherCachedResource? {
        val key = digest("$kind|$url")
        val payload = File(directory, "$key.bin")
        val meta = File(directory, "$key.meta")
        if (!payload.isFile || !meta.isFile) return null

        val bytes = runCatching { payload.readBytes() }.getOrNull() ?: return null
        val lines = runCatching { meta.readLines() }.getOrNull() ?: return null
        if (lines.size < 3 || lines[0] != url) return null
        val format = runCatching {
            TitanLauncherImageFormat.valueOf(lines[1])
        }.getOrNull() ?: return null
        val expected = lines[2]
        val actual = digestBytes(bytes)
        if (!expected.equals(actual, ignoreCase = true)) return null
        if (TitanLauncherImageSignatures.detect(bytes) != format) return null
        return TitanLauncherCachedResource(url, bytes, format, actual)
    }

    fun write(url: String, kind: String, bytes: ByteArray): TitanLauncherCachedResource? {
        val format = TitanLauncherImageSignatures.detect(bytes) ?: return null
        if (!directory.exists() && !directory.mkdirs()) return null
        val key = digest("$kind|$url")
        val payload = File(directory, "$key.bin")
        val meta = File(directory, "$key.meta")
        val hash = digestBytes(bytes)
        return runCatching {
            payload.writeBytes(bytes)
            meta.writeText("$url\n$format\n$hash\n")
            TitanLauncherCachedResource(url, bytes, format, hash)
        }.getOrNull()
    }

    private fun digest(value: String): String =
        digestBytes(value.toByteArray(Charsets.UTF_8))

    private fun digestBytes(bytes: ByteArray): String {
        val digest = MessageDigest.getInstance("SHA-256").digest(bytes)
        return digest.joinToString("") { "%02x".format(it) }
    }
}
