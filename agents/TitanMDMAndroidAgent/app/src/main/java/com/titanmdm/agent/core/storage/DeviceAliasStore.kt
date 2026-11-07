package com.titanmdm.agent.core.storage

import android.content.Context
import android.content.SharedPreferences
import kotlinx.coroutines.channels.awaitClose
import kotlinx.coroutines.flow.Flow
import kotlinx.coroutines.flow.callbackFlow

/**
 * Alias asignado en la consola (p. ej. HH-ALMACEN-01). Llega en cada
 * respuesta de heartbeat y se muestra en la cabecera del agente y del
 * launcher en lugar del modelo genérico ("Honeywell CK65").
 */
object DeviceAliasStore {

    private const val PREFS = "titan_device_alias"
    private const val KEY_ALIAS = "alias"
    private const val KEY_NAME = "device_name"

    fun save(context: Context, alias: String?, deviceName: String?) {
        context.applicationContext
            .getSharedPreferences(PREFS, Context.MODE_PRIVATE)
            .edit()
            .apply {
                if (alias.isNullOrBlank()) remove(KEY_ALIAS) else putString(KEY_ALIAS, alias.trim())
                if (!deviceName.isNullOrBlank()) putString(KEY_NAME, deviceName.trim())
            }
            .apply()
    }

    /** Alias actual y sus cambios (el heartbeat corre en el servicio, no en la UI). */
    fun observe(context: Context): Flow<String?> = callbackFlow {
        val prefs = context.applicationContext.getSharedPreferences(PREFS, Context.MODE_PRIVATE)
        trySend(prefs.getString(KEY_ALIAS, null))
        val listener = SharedPreferences.OnSharedPreferenceChangeListener { changed, key ->
            if (key == null || key == KEY_ALIAS) trySend(changed.getString(KEY_ALIAS, null))
        }
        prefs.registerOnSharedPreferenceChangeListener(listener)
        awaitClose { prefs.unregisterOnSharedPreferenceChangeListener(listener) }
    }

    fun alias(context: Context): String? =
        context.applicationContext
            .getSharedPreferences(PREFS, Context.MODE_PRIVATE)
            .getString(KEY_ALIAS, null)

    /** Alias si existe; si no, el nombre del servidor o [fallback]. */
    fun displayName(context: Context, fallback: String): String {
        val prefs = context.applicationContext.getSharedPreferences(PREFS, Context.MODE_PRIVATE)
        return label(prefs.getString(KEY_ALIAS, null), prefs.getString(KEY_NAME, null), fallback)
    }

    fun label(alias: String?, deviceName: String?, fallback: String): String =
        alias?.takeIf { it.isNotBlank() }
            ?: deviceName?.takeIf { it.isNotBlank() }
            ?: fallback

    fun clear(context: Context) {
        context.applicationContext
            .getSharedPreferences(PREFS, Context.MODE_PRIVATE)
            .edit()
            .clear()
            .apply()
    }
}
