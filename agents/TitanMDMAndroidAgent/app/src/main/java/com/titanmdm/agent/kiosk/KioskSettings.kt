package com.titanmdm.agent.kiosk

import android.content.Context
import android.content.RestrictionsManager
import com.titanmdm.agent.launcher.TitanLauncherPolicyReader
import com.titanmdm.agent.launcher.TitanLauncherPolicyState
import com.titanmdm.agent.launcher.TitanServerLauncherStore

/**
 * Bloqueos configurados en la política web del Titan Launcher
 * (claves titan_* de la managed configuration / APPLY_LAUNCHER_POLICY).
 *
 * Sin política del launcher no se bloquea nada: el equipo nunca queda
 * encerrado por una configuración ausente o inválida.
 */
data class KioskSettings(
    val launcherActive: Boolean = false,
    val persistentHome: Boolean = false,
    val lockStatusBar: Boolean = false,
    val lockSystemKeys: Boolean = false,
    val allowedPackages: List<String> = emptyList()
) {
    companion object {
        const val KEY_PERSISTENT_HOME = "titan_persistent_home"
        const val KEY_LOCK_STATUS_BAR = "titan_lock_status_bar"
        const val KEY_LOCK_SYSTEM_KEYS = "titan_lock_system_keys"

        val None = KioskSettings()

        /** Variante pura para pruebas JVM. */
        fun from(values: Map<String, String>): KioskSettings {
            if (values.isEmpty()) return None
            val policy = TitanLauncherPolicyReader.read(values)
            if (policy.state != TitanLauncherPolicyState.Valid) return None

            return KioskSettings(
                launcherActive = true,
                // El HOME fijo es el comportamiento por defecto del launcher.
                persistentHome = values[KEY_PERSISTENT_HOME]?.toBooleanStrictOrNull() ?: true,
                lockStatusBar = values[KEY_LOCK_STATUS_BAR]?.toBooleanStrictOrNull() ?: false,
                lockSystemKeys = values[KEY_LOCK_SYSTEM_KEYS]?.toBooleanStrictOrNull() ?: false,
                allowedPackages = policy.applications.map { it.packageName }.distinct()
            )
        }

        /** Managed config de Google si existe; si no, la del servidor TitanMDM. */
        fun read(context: Context): KioskSettings {
            val official = runCatching { officialRestrictions(context) }.getOrDefault(emptyMap())
            return from(official.ifEmpty { TitanServerLauncherStore.read(context) })
        }

        private fun officialRestrictions(context: Context): Map<String, String> {
            val manager = context.getSystemService(RestrictionsManager::class.java) ?: return emptyMap()
            val bundle = manager.applicationRestrictions ?: return emptyMap()
            return bundle.keySet().mapNotNull { key ->
                // Valores heterogéneos (String/Boolean/Int): solo se usa toString().
                @Suppress("DEPRECATION")
                bundle.get(key)?.let { key to it.toString() }
            }.toMap()
        }
    }
}
