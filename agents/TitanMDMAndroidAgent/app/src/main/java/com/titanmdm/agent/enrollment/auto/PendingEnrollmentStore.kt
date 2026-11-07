package com.titanmdm.agent.enrollment.auto

import android.content.Context

/**
 * Token de inscripción pendiente (privado de la app). Se borra en cuanto
 * el registro termina: el secreto permanente del dispositivo lo guarda
 * AgentIdentityStore cifrado con el Keystore.
 */
class PendingEnrollmentStore(context: Context) {

    private val prefs = context.applicationContext
        .getSharedPreferences(PREFS, Context.MODE_PRIVATE)

    data class Pending(
        val serverUrl: String,
        val token: String,
        val attempts: Int,
        val lastError: String?,
        val failedPermanently: Boolean
    )

    fun save(serverUrl: String, token: String) {
        prefs.edit()
            .putString(KEY_SERVER, serverUrl)
            .putString(KEY_TOKEN, token)
            .putInt(KEY_ATTEMPTS, 0)
            .remove(KEY_ERROR)
            .putBoolean(KEY_PERMANENT, false)
            .apply()
    }

    fun get(): Pending? {
        val server = prefs.getString(KEY_SERVER, null) ?: return null
        val token = prefs.getString(KEY_TOKEN, null) ?: return null
        return Pending(
            serverUrl = server,
            token = token,
            attempts = prefs.getInt(KEY_ATTEMPTS, 0),
            lastError = prefs.getString(KEY_ERROR, null),
            failedPermanently = prefs.getBoolean(KEY_PERMANENT, false)
        )
    }

    fun recordFailure(message: String, permanent: Boolean) {
        prefs.edit()
            .putInt(KEY_ATTEMPTS, prefs.getInt(KEY_ATTEMPTS, 0) + 1)
            .putString(KEY_ERROR, message)
            .putBoolean(KEY_PERMANENT, permanent)
            .apply()
    }

    fun clear() {
        prefs.edit().clear().apply()
    }

    private companion object {
        const val PREFS = "titan_pending_enrollment"
        const val KEY_SERVER = "server_url"
        const val KEY_TOKEN = "enrollment_token"
        const val KEY_ATTEMPTS = "attempts"
        const val KEY_ERROR = "last_error"
        const val KEY_PERMANENT = "failed_permanently"
    }
}
