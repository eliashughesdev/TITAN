package com.titanmdm.agent.location

import android.Manifest
import android.annotation.SuppressLint
import android.content.Context
import android.content.pm.PackageManager
import android.location.Location
import android.location.LocationManager
import androidx.core.content.ContextCompat
import androidx.core.location.LocationManagerCompat
import androidx.core.os.CancellationSignal
import kotlinx.coroutines.suspendCancellableCoroutine
import kotlinx.coroutines.withTimeoutOrNull
import java.util.concurrent.Executors
import kotlin.coroutines.resume

/**
 * Ubicación con el LocationManager nativo de Android, sin Google Play
 * Services. Se usa cuando el proveedor fusionado no está disponible o
 * no entrega posición (Honeywell sin GMS, Play Services deshabilitado).
 */
object NativeLocationProvider {

    private const val MAX_LAST_KNOWN_AGE_MS = 10 * 60 * 1000L

    private val executor = Executors.newSingleThreadExecutor()

    fun hasPermission(context: Context): Boolean =
        listOf(Manifest.permission.ACCESS_FINE_LOCATION, Manifest.permission.ACCESS_COARSE_LOCATION)
            .any { ContextCompat.checkSelfPermission(context, it) == PackageManager.PERMISSION_GRANTED }

    /** Posición actual (GPS, luego red) o la última conocida reciente. */
    @SuppressLint("MissingPermission")
    suspend fun current(context: Context, timeoutMs: Long): Location? {
        if (!hasPermission(context)) return null
        val manager = context.getSystemService(LocationManager::class.java) ?: return null

        val providers = listOf(LocationManager.GPS_PROVIDER, LocationManager.NETWORK_PROVIDER)
            .filter { runCatching { manager.isProviderEnabled(it) }.getOrDefault(false) }

        for (provider in providers) {
            val fix = withTimeoutOrNull(timeoutMs / providers.size.coerceAtLeast(1)) {
                requestCurrent(manager, provider)
            }
            if (fix != null) return fix
        }

        return lastKnown(manager)
            ?.takeIf { System.currentTimeMillis() - it.time <= MAX_LAST_KNOWN_AGE_MS }
    }

    @SuppressLint("MissingPermission")
    fun lastKnown(manager: LocationManager): Location? =
        manager.getProviders(true)
            .mapNotNull { runCatching { manager.getLastKnownLocation(it) }.getOrNull() }
            .maxByOrNull { it.time }

    @SuppressLint("MissingPermission")
    private suspend fun requestCurrent(manager: LocationManager, provider: String): Location? =
        suspendCancellableCoroutine { cont ->
            val signal = CancellationSignal()
            cont.invokeOnCancellation { signal.cancel() }
            runCatching {
                LocationManagerCompat.getCurrentLocation(manager, provider, signal, executor) { location ->
                    if (cont.isActive) cont.resume(location)
                }
            }.onFailure {
                if (cont.isActive) cont.resume(null)
            }
        }
}
