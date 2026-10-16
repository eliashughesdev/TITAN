package com.titanmdm.agent.launcher

import android.os.Looper
import okhttp3.OkHttpClient
import okhttp3.Request
import java.util.concurrent.TimeUnit

class TitanLauncherOkHttpTransport : TitanLauncherHttpTransport {
    private val client = OkHttpClient.Builder()
        .followRedirects(false)
        .followSslRedirects(false)
        .callTimeout(20, TimeUnit.SECONDS)
        .build()

    override fun get(url: String): TitanLauncherHttpResponse {
        if (Looper.myLooper() == Looper.getMainLooper()) {
            throw IllegalStateException(
                "La descarga del launcher no puede ejecutarse en el hilo principal."
            )
        }

        val request = Request.Builder().url(url).get().build()
        client.newCall(request).execute().use { response ->
            val bytes = if (response.isSuccessful) {
                response.body?.bytes() ?: ByteArray(0)
            } else {
                ByteArray(0)
            }
            return TitanLauncherHttpResponse(
                statusCode = response.code,
                location = response.header("Location"),
                bytes = bytes
            )
        }
    }
}
