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

object HybridLocationProvider {

    private const val FUSED_TIMEOUT_MS = 3_000L
    private const val GPS_TIMEOUT_MS = 2_500L
    private const val NETWORK_TIMEOUT_MS = 1_500L
    private const val LAST_KNOWN_MAX_AGE_MS = 30 * 60 * 1000L

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

        val fused = runCatching {
            withTimeoutOrNull(timeoutMs.coerceAtMost(FUSED_TIMEOUT_MS)) {
                awaitFusedLocation(appContext)
            }
        }.getOrNull()

        if (fused != null) {
            return HybridLocationSnapshot(fused, wifi)
        }

        val gps = NativeLocationProvider.currentFromProvider(
            manager,
            LocationManager.GPS_PROVIDER,
            GPS_TIMEOUT_MS
        )

        if (gps != null) {
            return HybridLocationSnapshot(gps, wifi)
        }

        NativeLocationProvider.bestLastKnown(
            manager,
            LAST_KNOWN_MAX_AGE_MS
        )?.let { fallback ->
            return HybridLocationSnapshot(fallback, wifi)
        }

        val network = NativeLocationProvider.currentFromProvider(
            manager,
            LocationManager.NETWORK_PROVIDER,
            NETWORK_TIMEOUT_MS
        )

        if (network != null) {
            return HybridLocationSnapshot(network, wifi)
        }

        return HybridLocationSnapshot(
            NativeLocationProvider.bestLastKnown(
                manager,
                LAST_KNOWN_MAX_AGE_MS
            ),
            wifi
        )
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
