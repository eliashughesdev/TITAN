package com.titanmdm.agent.launcher

import android.content.Context
import android.content.Intent
import android.content.IntentFilter
import android.net.ConnectivityManager
import android.net.NetworkCapabilities
import android.os.BatteryManager

/** Indicadores de la cabecera del launcher: batería y red. */
data class TitanDeviceStatus(
    val batteryPercent: Int? = null,
    val charging: Boolean = false,
    val network: String = "Sin red",
    val online: Boolean = false
) {
    val batteryLabel: String
        get() = batteryPercent?.let { "Batería $it%${if (charging) " · cargando" else ""}" }
            ?: "Batería N/D"

    val batteryLow: Boolean
        get() = !charging && (batteryPercent ?: 100) <= 15

    companion object {
        fun read(context: Context): TitanDeviceStatus {
            val battery = runCatching {
                context.registerReceiver(null, IntentFilter(Intent.ACTION_BATTERY_CHANGED))
            }.getOrNull()

            val level = battery?.getIntExtra(BatteryManager.EXTRA_LEVEL, -1) ?: -1
            val scale = battery?.getIntExtra(BatteryManager.EXTRA_SCALE, -1) ?: -1
            val plugged = (battery?.getIntExtra(BatteryManager.EXTRA_PLUGGED, 0) ?: 0) != 0
            val percent = if (level >= 0 && scale > 0) level * 100 / scale else null

            val connectivity = context.getSystemService(ConnectivityManager::class.java)
            val capabilities = runCatching {
                connectivity?.getNetworkCapabilities(connectivity.activeNetwork)
            }.getOrNull()

            val network = when {
                capabilities == null -> "Sin red"
                capabilities.hasTransport(NetworkCapabilities.TRANSPORT_WIFI) -> "Wi-Fi"
                capabilities.hasTransport(NetworkCapabilities.TRANSPORT_ETHERNET) -> "Ethernet"
                capabilities.hasTransport(NetworkCapabilities.TRANSPORT_CELLULAR) -> "Datos móviles"
                else -> "Red"
            }

            return TitanDeviceStatus(
                batteryPercent = percent,
                charging = plugged,
                network = network,
                online = capabilities?.hasCapability(NetworkCapabilities.NET_CAPABILITY_INTERNET) == true
            )
        }
    }
}
