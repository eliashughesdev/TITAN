package com.titanmdm.agent.core.network

import com.titanmdm.agent.core.config.AgentConfig
import kotlinx.serialization.json.Json
import okhttp3.MediaType.Companion.toMediaType
import okhttp3.OkHttpClient
import okhttp3.logging.HttpLoggingInterceptor
import retrofit2.Retrofit
import retrofit2.converter.kotlinx.serialization.asConverterFactory
import java.util.concurrent.TimeUnit

object TitanApiClient {

    private val json = Json {
        ignoreUnknownKeys = true
        isLenient = false
        encodeDefaults = true
        explicitNulls = false
        coerceInputValues = true
    }

    private val loggingInterceptor =
        HttpLoggingInterceptor().apply {
            /*
             * BASIC evita registrar cuerpos que posteriormente
             * podrían contener secretos del dispositivo.
             */
            level = HttpLoggingInterceptor.Level.BASIC
        }

    private val httpClient: OkHttpClient =
        OkHttpClient.Builder()
            .connectTimeout(
                AgentConfig.CONNECT_TIMEOUT_SECONDS,
                TimeUnit.SECONDS
            )
            .readTimeout(
                AgentConfig.READ_TIMEOUT_SECONDS,
                TimeUnit.SECONDS
            )
            .writeTimeout(
                AgentConfig.WRITE_TIMEOUT_SECONDS,
                TimeUnit.SECONDS
            )
            .addInterceptor { chain ->

                val original = chain.request()

                val request = original
                    .newBuilder()
                    .header(
                        "User-Agent",
                        AgentConfig.USER_AGENT
                    )
                    .header(
                        "Accept",
                        "application/json"
                    )
                    .build()

                chain.proceed(request)
            }
            .addInterceptor(loggingInterceptor)
            .retryOnConnectionFailure(true)
            .build()

    /** Descargas grandes (APK de la política): mismo cliente, más tiempo de lectura. */
    val downloadClient: OkHttpClient by lazy {
        httpClient.newBuilder()
            .readTimeout(5, TimeUnit.MINUTES)
            .build()
    }

    /*
     * El servidor puede cambiar al inscribir por QR: el cliente se
     * reconstruye cuando la URL activa difiere de la cacheada.
     */
    @Volatile
    private var cached: Pair<String, TitanApiService>? = null

    val service: TitanApiService
        get() {
            val baseUrl = AgentConfig.normalizedBaseUrl()
            cached?.let { (url, api) -> if (url == baseUrl) return api }
            return synchronized(this) {
                cached?.takeIf { it.first == baseUrl }?.second
                    ?: build(baseUrl).also { cached = baseUrl to it }
            }
        }

    private fun build(baseUrl: String): TitanApiService {

        val contentType =
            "application/json".toMediaType()

        return Retrofit.Builder()
            .baseUrl(
                baseUrl
            )
            .client(httpClient)
            .addConverterFactory(
                json.asConverterFactory(contentType)
            )
            .build()
            .create(TitanApiService::class.java)
    }
}