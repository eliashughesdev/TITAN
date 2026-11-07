package com.titanmdm.agent.apps.install

import android.content.Context
import com.titanmdm.agent.core.config.AgentConfig
import com.titanmdm.agent.core.network.TitanApiClient
import com.titanmdm.agent.core.storage.AgentIdentity
import kotlinx.coroutines.Dispatchers
import kotlinx.coroutines.withContext
import okhttp3.Request
import java.io.File
import java.security.MessageDigest

/**
 * Descarga un APK del repositorio de software del servidor
 * (/api/device/software-packages/{id}/download, autenticado con la
 * identidad del dispositivo) y verifica su SHA-256 antes de instalarlo.
 */
class ApkDownloader(context: Context) {

    private val directory = File(context.applicationContext.cacheDir, "policy-apks")

    sealed interface Outcome {
        data class Downloaded(val file: File) : Outcome
        data class Failed(val code: String, val message: String) : Outcome
    }

    suspend fun download(
        identity: AgentIdentity,
        relativeUrl: String,
        expectedSha256: String,
        name: String
    ): Outcome = withContext(Dispatchers.IO) {
        val url = resolve(relativeUrl)
            ?: return@withContext Outcome.Failed("INVALID_URL", "URL de descarga no válida: $relativeUrl")

        directory.mkdirs()
        val target = File(directory, "${name.replace(Regex("[^A-Za-z0-9._-]"), "_")}.apk")
        val partial = File(directory, "${target.name}.part")

        val request = Request.Builder()
            .url(url)
            .header("X-Titan-Device-Id", identity.deviceId)
            .header("X-Titan-Device-Secret", identity.deviceSecret)
            .header("Accept", "application/vnd.android.package-archive")
            .build()

        try {
            TitanApiClient.downloadClient.newCall(request).execute().use { response ->
                if (!response.isSuccessful) {
                    return@withContext Outcome.Failed(
                        "DOWNLOAD_HTTP_${response.code}",
                        "El servidor respondió HTTP ${response.code} al descargar $name."
                    )
                }
                val body = response.body
                    ?: return@withContext Outcome.Failed("DOWNLOAD_EMPTY", "Descarga vacía de $name.")

                val digest = MessageDigest.getInstance("SHA-256")
                body.byteStream().use { input ->
                    partial.outputStream().use { output ->
                        val buffer = ByteArray(64 * 1024)
                        while (true) {
                            val read = input.read(buffer)
                            if (read < 0) break
                            digest.update(buffer, 0, read)
                            output.write(buffer, 0, read)
                        }
                    }
                }

                val actual = digest.digest().joinToString("") { "%02x".format(it) }
                if (!actual.equals(expectedSha256.trim(), ignoreCase = true)) {
                    partial.delete()
                    return@withContext Outcome.Failed(
                        "CHECKSUM_MISMATCH",
                        "El SHA-256 de $name no coincide con el del servidor."
                    )
                }
            }

            target.delete()
            if (!partial.renameTo(target)) {
                return@withContext Outcome.Failed("DOWNLOAD_IO", "No se pudo guardar $name.")
            }
            Outcome.Downloaded(target)
        } catch (exception: Exception) {
            partial.delete()
            Outcome.Failed("DOWNLOAD_ERROR", exception.message ?: exception.javaClass.simpleName)
        }
    }

    /** Ruta relativa del servidor o URL absoluta del mismo servidor. */
    private fun resolve(relativeUrl: String): String? {
        val value = relativeUrl.trim()
        if (value.startsWith("https://") || value.startsWith("http://")) {
            val base = AgentConfig.normalizedBaseUrl()
            return value.takeIf { it.startsWith(base.trimEnd('/')) }
        }
        if (!value.startsWith("/")) return null
        return AgentConfig.normalizedBaseUrl().trimEnd('/') + value
    }
}
