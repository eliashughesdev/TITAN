package com.titanmdm.agent.location

import android.annotation.SuppressLint
import android.content.Context
import android.location.Location
import android.location.LocationManager
import com.google.android.gms.location.LocationServices
import com.google.android.gms.location.Priority
import com.google.android.gms.tasks.CancellationTokenSource
import kotlinx.coroutines.suspendCancellableCoroutine
import kotlinx.coroutines.withTimeoutOrNull
import kotlin.coroutines.resume

data class HybridLocationSnapshot(
    val location: Location?,
    val wifi: WifiAccessPointSnapshot
)

/**
 * Doble motor de ubicación: Google (FusedLocation) si existe y el
 * LocationManager nativo (GPS + red) si no, con la caché del servicio
 * ([LocationWatcher]) y la última posición conocida como respaldo.
 *
 * Orden: posición reciente de la caché (sin esperar), Fused, GPS, red y,
 * si nada responde a tiempo, la mejor posición conocida de hasta 7 días
 * con su hora real. Así el heartbeat no sale sin coordenadas mientras el
 * equipo haya tenido alguna posición, sin inventar ninguna.
 */
object HybridLocationProvider {

    private const val FUSED_TIMEOUT_MS = 3_000L
    private const val GPS_TIMEOUT_MS = 2_500L
    private const val NETWORK_TIMEOUT_MS = 1_500L

    /** Posición de la caché que se usa sin consultar a los proveedores. */
    private const val FRESH_CACHE_MS = 2 * 60_000L

    @SuppressLint("MissingPermission")
    suspend fun capture(
        context: Context,
        timeoutMs: Long = FUSED_TIMEOUT_MS + GPS_TIMEOUT_MS
    ): HybridLocationSnapshot {
        val appContext = context.applicationContext
        val wifi = WifiTelemetryProvider.read(appContext)

        if (!NativeLocationProvider.hasPermission(appContext)) {
            return HybridLocationSnapshot(null, wifi)
        }

        val manager = appContext.getSystemService(LocationManager::class.java)
            ?: return HybridLocationSnapshot(null, wifi)

        val now = System.currentTimeMillis()
        LocationWatcher.cached(appContext)
            ?.takeIf { now - it.time in 0..FRESH_CACHE_MS }
            ?.let { return HybridLocationSnapshot(it, wifi) }

        val fresh =
            runCatching {
                withTimeoutOrNull(timeoutMs.coerceAtMost(FUSED_TIMEOUT_MS)) {
                    awaitFusedLocation(appContext)
                }
            }.getOrNull()
                ?: NativeLocationProvider.currentFromProvider(
                    manager,
                    LocationManager.GPS_PROVIDER,
                    GPS_TIMEOUT_MS
                )
                ?: NativeLocationProvider.currentFromProvider(
                    manager,
                    LocationManager.NETWORK_PROVIDER,
                    NETWORK_TIMEOUT_MS
                )

        if (fresh != null) {
            LocationWatcher.offer(fresh, appContext)
            return HybridLocationSnapshot(fresh, wifi)
        }

        // Respaldo: caché del servicio o última conocida de cualquier proveedor.
        val fallback = LocationFixSelector.best(
            listOf(
                LocationWatcher.fix(LocationWatcher.cached(appContext)),
                LocationWatcher.fix(NativeLocationProvider.lastKnown(manager))
            ),
            nowMs = System.currentTimeMillis()
        )?.value

        return HybridLocationSnapshot(fallback, wifi)
    }

    @SuppressLint("MissingPermission")
    private suspend fun awaitFusedLocation(
        context: Context
    ): Location? {
        val client = LocationServices.getFusedLocationProviderClient(context)

        return suspendCancellableCoroutine { continuation ->
            client.getCurrentLocation(
                Priority.PRIORITY_HIGH_ACCURACY,
                CancellationTokenSource().token
            )
                .addOnSuccessListener { location ->
                    if (continuation.isActive) {
                        continuation.resume(location)
                    }
                }
                .addOnFailureListener {
                    if (continuation.isActive) {
                        continuation.resume(null)
                    }
                }
        }
    }
}
