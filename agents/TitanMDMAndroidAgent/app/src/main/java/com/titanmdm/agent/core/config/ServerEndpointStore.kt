package com.titanmdm.agent.core.config

import android.content.Context
import android.content.SharedPreferences

/**
 * URL del servidor TitanMDM recibida en la inscripcion por QR.
 * No es secreta (el secreto del dispositivo va cifrado en AgentIdentityStore).
 * Si no hay valor se usa BuildConfig.TITAN_API_BASE_URL.
 */
object ServerEndpointStore {

    private const val PREFS = "titanmdm_server_endpoint"
    private const val KEY_URL = "server_url"

    @Volatile
    private var prefs: SharedPreferences? = null

    fun init(context: Context) {
        if (prefs == null) {
            prefs = context.applicationContext.getSharedPreferences(PREFS, Context.MODE_PRIVATE)
        }
    }

    fun current(): String? = prefs?.getString(KEY_URL, null)?.takeIf { it.isNotBlank() }

    /** Guarda sincronicamente: el siguiente request ya usa el nuevo servidor. */
    fun save(url: String?) {
        val editor = prefs?.edit() ?: return
        if (url.isNullOrBlank()) editor.remove(KEY_URL) else editor.putString(KEY_URL, url)
        editor.commit()
    }
}
