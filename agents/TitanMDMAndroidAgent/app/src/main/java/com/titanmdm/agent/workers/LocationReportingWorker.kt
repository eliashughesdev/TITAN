package com.titanmdm.agent.workers

import android.Manifest
import android.annotation.SuppressLint
import android.content.Context
import android.content.pm.PackageManager
import android.os.BatteryManager
import android.os.PowerManager
import androidx.core.content.ContextCompat
import androidx.work.CoroutineWorker
import androidx.work.WorkerParameters
import com.google.android.gms.location.LocationServices
import com.google.android.gms.location.Priority
import com.google.android.gms.tasks.CancellationTokenSource
import com.titanmdm.agent.core.network.LocationReportRequest
import com.titanmdm.agent.core.network.TitanApiClient
import com.titanmdm.agent.core.storage.AgentIdentityStore
import com.titanmdm.agent.location.LocationCaptureDecision
import com.titanmdm.agent.location.LocationCapturePayload
import com.titanmdm.agent.location.LocationCapturePolicy
import kotlinx.coroutines.suspendCancellableCoroutine
import kotlinx.coroutines.withTimeoutOrNull
import kotlinx.serialization.encodeToString
import kotlinx.serialization.json.Json
import java.time.Instant
import kotlin.coroutines.resume

/*
 * ANDROID-GEO-01
 *
 * Captura proactiva periodica de ubicacion.
 *
 * - Consulta la politica con autenticacion de
 *   dispositivo.
 * - Respeta permisos Android: sin permiso reporta
 *   solo estado PERMISSION_DENIED.
 * - No captura con ahorro de bateria o bateria
 *   baja.
 * - No inventa posiciones: si no hay fix reporta
 *   UNAVAILABLE.
 */
class LocationReportingWorker(
    appContext: Context,
    workerParams: WorkerParameters
) : CoroutineWorker(
    appContext,
    workerParams
) {

    private val json = Json {
        encodeDefaults = true
        explicitNulls = false
    }

    @SuppressLint("MissingPermission")
    override suspend fun doWork(): Result {
        val identityStore =
            AgentIdentityStore(applicationContext)

        return try {
            val identity =
                identityStore.get()
                    ?: return Result.success()

            val policyResponse =
                TitanApiClient.service
                    .getReportingPolicy(
                        identity.deviceId,
                        identity.deviceSecret
                    )

            if (!policyResponse.isSuccessful) {
                return Result.retry()
            }

            val policy =
                policyResponse.body()
                    ?: return Result.success()

            val permissionGranted =
                hasLocationPermission()

            val batteryPercent =
                readBatteryPercent()

            val batterySaverActive =
                isBatterySaverActive()

            val lastReportAt =
                readLastReportAtEpochMillis()

            val fix =
                if (permissionGranted) {
                    captureFix()
                } else {
                    null
                }

            val decision =
                LocationCapturePolicy.decide(
                    reportingEnabled =
                        policy.isEnabled,
                    permissionGranted =
                        permissionGranted,
                    batteryPercent =
                        batteryPercent,
                    batterySaverActive =
                        batterySaverActive,
                    lastReportAtEpochMillis =
                        lastReportAt,
                    nowEpochMillis =
                        System.currentTimeMillis(),
                    capturedFix = fix,
                    reportingIntervalMinutes =
                        policy
                            .reportingIntervalMinutes
                )

            when (decision) {
                is LocationCaptureDecision.Skip -> {
                    Result.success()
                }

                is LocationCaptureDecision.ReportStatusOnly -> {
                    val payloadJson =
                        json.encodeToString(
                            StatusOnlyPayload(
                                source =
                                    decision.source,
                                status =
                                    decision.status
                            )
                        )

                    upload(
                        identity.deviceId,
                        identity.deviceSecret,
                        payloadJson
                    )
                }

                is LocationCaptureDecision.ReportCoordinates -> {
                    val payloadJson =
                        json.encodeToString(
                            CoordinatesPayload(
                                latitude =
                                    decision.payload
                                        .latitude,
                                longitude =
                                    decision.payload
                                        .longitude,
                                accuracyMeters =
                                    decision.payload
                                        .accuracyMeters,
                                altitudeMeters =
                                    decision.payload
                                        .altitudeMeters,
                                speedMetersPerSecond =
                                    decision.payload
                                        .speedMetersPerSecond,
                                source =
                                    decision.payload
                                        .source,
                                capturedAtUtc =
                                    decision.payload
                                        .capturedAtUtc
                            )
                        )

                    val uploaded =
                        upload(
                            identity.deviceId,
                            identity.deviceSecret,
                            payloadJson
                        )

                    if (uploaded is
                        Result.Success
                    ) {
                        writeLastReportAtEpochMillis(
                            System.currentTimeMillis()
                        )
                    }

                    uploaded
                }
            }
        } catch (e: Exception) {
            Result.retry()
        }
    }

    private suspend fun upload(
        deviceId: String,
        deviceSecret: String,
        payloadJson: String
    ): Result {
        val response =
            TitanApiClient.service.reportLocation(
                deviceId,
                deviceSecret,
                LocationReportRequest(payloadJson)
            )

        return if (response.isSuccessful) {
            Result.success()
        } else {
            Result.retry()
        }
    }

    @SuppressLint("MissingPermission")
    private suspend fun captureFix():
            LocationCapturePayload? {
        return try {
            val fusedLocationClient =
                LocationServices
                    .getFusedLocationProviderClient(
                        applicationContext
                    )

            val location =
                withTimeoutOrNull(
                    LOCATION_FIX_TIMEOUT_MS
                ) {
                    awaitCurrentLocation(
                        fusedLocationClient
                    )
                }

            location?.let {
                LocationCapturePayload(
                    latitude = it.latitude,
                    longitude = it.longitude,
                    accuracyMeters =
                        if (it.hasAccuracy())
                            it.accuracy.toDouble()
                        else null,
                    altitudeMeters =
                        if (it.hasAltitude())
                            it.altitude
                        else null,
                    speedMetersPerSecond =
                        if (it.hasSpeed())
                            it.speed.toDouble()
                        else null,
                    source =
                        LocationCapturePolicy
                            .PROACTIVE_SOURCE,
                    capturedAtUtc =
                        Instant
                            .ofEpochMilli(it.time)
                            .toString()
                )
            }
        } catch (e: Exception) {
            null
        }
    }

    /*
     * Play Services Task -> suspend sin depender del
     * artefacto kotlinx-coroutines-play-services (no
     * esta en el classpath del proyecto).
     */
    @SuppressLint("MissingPermission")
    private suspend fun awaitCurrentLocation(
        fusedLocationClient:
        com.google.android.gms.location.FusedLocationProviderClient
    ): android.location.Location? {
        return suspendCancellableCoroutine { cont ->
            fusedLocationClient
                .getCurrentLocation(
                    Priority
                        .PRIORITY_BALANCED_POWER_ACCURACY,
                    CancellationTokenSource()
                        .token
                )
                .addOnSuccessListener { fix ->
                    if (cont.isActive) {
                        cont.resume(fix)
                    }
                }
                .addOnFailureListener {
                    if (cont.isActive) {
                        cont.resume(null)
                    }
                }
        }
    }

    private fun hasLocationPermission(): Boolean {
        val fineGranted =
            ContextCompat.checkSelfPermission(
                applicationContext,
                Manifest.permission
                    .ACCESS_FINE_LOCATION
            ) == PackageManager.PERMISSION_GRANTED

        val coarseGranted =
            ContextCompat.checkSelfPermission(
                applicationContext,
                Manifest.permission
                    .ACCESS_COARSE_LOCATION
            ) == PackageManager.PERMISSION_GRANTED

        return fineGranted || coarseGranted
    }

    private fun readBatteryPercent(): Int? {
        val batteryManager =
            applicationContext.getSystemService(
                Context.BATTERY_SERVICE
            ) as? BatteryManager

        val percent =
            batteryManager?.getIntProperty(
                BatteryManager
                    .BATTERY_PROPERTY_CAPACITY
            )

        return if (
            percent != null &&
            percent in 0..100
        ) {
            percent
        } else {
            null
        }
    }

    private fun isBatterySaverActive(): Boolean {
        val powerManager =
            applicationContext.getSystemService(
                Context.POWER_SERVICE
            ) as? PowerManager

        return powerManager?.isPowerSaveMode == true
    }

    private fun readLastReportAtEpochMillis():
            Long? {
        val prefs =
            applicationContext.getSharedPreferences(
                PREFS_NAME,
                Context.MODE_PRIVATE
            )

        val value =
            prefs.getLong(
                KEY_LAST_REPORT_AT,
                -1L
            )

        return if (value >= 0) value else null
    }

    private fun writeLastReportAtEpochMillis(
        epochMillis: Long
    ) {
        applicationContext
            .getSharedPreferences(
                PREFS_NAME,
                Context.MODE_PRIVATE
            )
            .edit()
            .putLong(
                KEY_LAST_REPORT_AT,
                epochMillis
            )
            .apply()
    }

    /*
     * Payloads serializables espejo del contrato
     * LocationResult del backend.
     */
    @kotlinx.serialization.Serializable
    private data class CoordinatesPayload(
        val latitude: Double,
        val longitude: Double,
        val accuracyMeters: Double? = null,
        val altitudeMeters: Double? = null,
        val speedMetersPerSecond: Double? = null,
        val source: String,
        val capturedAtUtc: String
    )

    @kotlinx.serialization.Serializable
    private data class StatusOnlyPayload(
        val source: String,
        val status: String
    )

    companion object {
        private const val PREFS_NAME =
            "titanmdm_location_worker"

        private const val KEY_LAST_REPORT_AT =
            "last_proactive_report_at"

        private const val
        LOCATION_FIX_TIMEOUT_MS = 10_000L
    }
}
