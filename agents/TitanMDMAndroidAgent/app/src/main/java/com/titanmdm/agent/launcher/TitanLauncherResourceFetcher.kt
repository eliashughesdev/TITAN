package com.titanmdm.agent.launcher

data class TitanLauncherHttpResponse(
    val statusCode: Int,
    val location: String? = null,
    val bytes: ByteArray = ByteArray(0)
)

fun interface TitanLauncherHttpTransport {
    fun get(url: String): TitanLauncherHttpResponse
}

data class TitanLauncherFetchResult(
    val bytes: ByteArray? = null,
    val error: String? = null,
    val fromCache: Boolean = false
)

/**
 * Descarga fuera del hilo que la invoca. El llamador debe usar un
 * dispatcher de fondo. No sigue redirecciones inseguras.
 */
class TitanLauncherResourceFetcher(
    private val transport: TitanLauncherHttpTransport,
    private val cache: TitanLauncherResourceCache,
    private val maxAttempts: Int = 3,
    private val sleep: (Long) -> Unit = { millis ->
        if (millis > 0) Thread.sleep(millis)
    }
) {
    fun fetch(url: String, kind: String, maxBytes: Int): TitanLauncherFetchResult {
        val normalized = TitanLauncherResourceUrl.normalize(url)
            ?: return TitanLauncherFetchResult(error = "URL de recurso no autorizada.")

        var attempt = 0
        var lastError = "No fue posible descargar el recurso."
        while (attempt < maxAttempts) {
            attempt += 1
            val outcome = tryOnce(normalized, maxBytes)
            if (outcome.bytes != null) {
                val stored = cache.write(normalized, kind, outcome.bytes)
                    ?: return TitanLauncherFetchResult(error = "El contenido no es PNG, JPEG o WebP.")
                return TitanLauncherFetchResult(bytes = stored.bytes)
            }
            lastError = outcome.error ?: lastError
            if (!isRetryable(lastError) || attempt == maxAttempts) {
                break
            }
            sleep(250L * attempt)
        }

        val cached = cache.read(normalized, kind)
        return if (cached != null) {
            TitanLauncherFetchResult(
                bytes = cached.bytes,
                error = lastError,
                fromCache = true
            )
        } else {
            TitanLauncherFetchResult(error = lastError)
        }
    }

    private fun tryOnce(url: String, maxBytes: Int): TitanLauncherFetchResult {
        val first = runCatching { transport.get(url) }.getOrElse {
            return TitanLauncherFetchResult(error = "Error de red al descargar el recurso.")
        }
        val response = followIfSafe(first) ?: return TitanLauncherFetchResult(
            error = "Redirección insegura rechazada."
        )
        if (response.statusCode !in 200..299) {
            return TitanLauncherFetchResult(
                error = "HTTP ${response.statusCode} al descargar el recurso."
            )
        }
        if (response.bytes.size > maxBytes) {
            return TitanLauncherFetchResult(error = "El recurso supera el tamaño permitido.")
        }
        if (TitanLauncherImageSignatures.detect(response.bytes) == null) {
            return TitanLauncherFetchResult(error = "El contenido no es PNG, JPEG o WebP.")
        }
        return TitanLauncherFetchResult(bytes = response.bytes)
    }

    private fun followIfSafe(response: TitanLauncherHttpResponse): TitanLauncherHttpResponse? {
        if (response.statusCode !in 300..399) return response
        val next = TitanLauncherResourceUrl.normalize(response.location) ?: return null
        val followed = runCatching { transport.get(next) }.getOrNull() ?: return null
        return if (followed.statusCode in 300..399) null else followed
    }

    private fun isRetryable(error: String): Boolean {
        return error.startsWith("HTTP 408") ||
            error.startsWith("HTTP 429") ||
            error.startsWith("HTTP 5") ||
            error.startsWith("Error de red")
    }
}
