package com.titanmdm.agent.location

import android.Manifest
import android.annotation.SuppressLint
import android.content.Context
import android.content.pm.PackageManager
import android.net.wifi.WifiManager
import androidx.core.content.ContextCompat

data class WifiAccessPointSnapshot(
    val bssid: String?,
    val rssi: Int?
)

object WifiTelemetryProvider {

    private const val UNKNOWN_RSSI = -127
    private const val REDACTED_BSSID = "02:00:00:00:00:00"

    @SuppressLint("MissingPermission")
    fun read(context: Context): WifiAccessPointSnapshot {
        val appContext = context.applicationContext

        if (!hasLocationPermission(appContext)) {
            return WifiAccessPointSnapshot(null, null)
        }

        val manager = runCatching {
            appContext.getSystemService(Context.WIFI_SERVICE) as? WifiManager
        }.getOrNull() ?: return WifiAccessPointSnapshot(null, null)

        val info = runCatching { manager.connectionInfo }.getOrNull()
            ?: return WifiAccessPointSnapshot(null, null)

        val bssid = runCatching { info.bssid }
            .getOrNull()
            ?.trim()
            ?.takeIf {
                it.isNotEmpty() &&
                    !it.equals(REDACTED_BSSID, ignoreCase = true)
            }

        val rssi = info.rssi
            .takeIf { it != UNKNOWN_RSSI && it < 0 }

        return WifiAccessPointSnapshot(
            bssid = bssid,
            rssi = rssi
        )
    }

    private fun hasLocationPermission(context: Context): Boolean =
        ContextCompat.checkSelfPermission(
            context,
            Manifest.permission.ACCESS_FINE_LOCATION
        ) == PackageManager.PERMISSION_GRANTED ||
            ContextCompat.checkSelfPermission(
                context,
                Manifest.permission.ACCESS_COARSE_LOCATION
            ) == PackageManager.PERMISSION_GRANTED
}
