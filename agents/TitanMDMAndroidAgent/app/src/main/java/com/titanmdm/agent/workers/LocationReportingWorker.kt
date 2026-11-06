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
import com.titanmdm.agent.core.network.LocationReportRequest
import com.titanmdm.agent.core.network.TitanApiClient
import com.titanmdm.agent.core.storage.AgentIdentityStore
import com.titanmdm.agent.location.HybridLocationProvider
import com.titanmdm.agent.location.LocationCaptureDecision
import com.titanmdm.agent.location.LocationCapturePayload
import com.titanmdm.agent.location.LocationCapturePolicy
import com.titanmdm.agent.location.WifiAccessPointSnapshot
import com.titanmdm.agent.location.WifiTelemetryProvider
import kotlinx.serialization.encodeToString
import kotlinx.serialization.json.Json
import java.time.Instant

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

            // P1: mantener geocercas del SO al dia (independiente del muestreo).
            GeofenceSyncWorker.syncGeofences(
                applicationContext,
                force = false
            )

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

            val hybrid =
                if (permissionGranted) {
                    HybridLocationProvider.capture(
                        applicationContext,
                        LOCATION_FIX_TIMEOUT_MS
                    )
                } else {
                    null
                }

            val wifi = hybrid?.wifi
                ?: WifiTelemetryProvider.read(applicationContext)

            val fix = hybrid?.location?.let { location ->
                LocationCapturePayload(
                    latitude = location.latitude,
                    longitude = location.longitude,
                    accuracyMeters =
                        if (location.hasAccuracy())
                            location.accuracy.toDouble()
                        else null,
                    altitudeMeters =
                        if (location.hasAltitude())
                            location.altitude
                        else null,
                    speedMetersPerSecond =
                        if (location.hasSpeed())
                            location.speed.toDouble()
                        else null,
                    source =
                        location.provider
                            ?: LocationCapturePolicy.PROACTIVE_SOURCE,
                    capturedAtUtc =
                        Instant.ofEpochMilli(location.time).toString()
                )
            }

            /*
             * El permiso denegado no se reporta como estado terminal:
             * el heartbeat y la ubicación por Wi-Fi siguen siendo
             * válidos y el backend puede resolver el punto de acceso.
             */
            if (!permissionGranted) {
                return uploadStatus(
                    identity.deviceId,
                    identity.deviceSecret,
                    source = WIFI_SOURCE,
                    status = statusForWifiOnly(wifi),
                    wifi = wifi
                )
            }

            val decision =
                LocationCapturePolicy.decide(
                    reportingEnabled =
                        policy.isEnabled,
                    permissionGranted =
                        true,
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
                    uploadStatus(
                        identity.deviceId,
                        identity.deviceSecret,
                        source = WIFI_SOURCE,
                        status = statusForWifiOnly(wifi),
                        wifi = wifi
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
                                        .capturedAtUtc,
                                bssid = wifi.bssid,
                                rssi = wifi.rssi
                            )
                        )

                    val uploaded =
                        upload(
                            identity.deviceId,
                            identity.deviceSecret,
                            payloadJson
                        )

                    // Result.Success es API restringida de WorkManager.
                    if (uploaded ==
                        Result.success()
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

    private suspend fun uploadStatus(
        deviceId: String,
        deviceSecret: String,
        source: String,
        status: String,
        wifi: WifiAccessPointSnapshot
    ): Result {
        val payloadJson =
            json.encodeToString(
                StatusOnlyPayload(
                    source = source,
                    status = status,
                    bssid = wifi.bssid,
                    rssi = wifi.rssi
                )
            )

        return upload(
            deviceId,
            deviceSecret,
            payloadJson
        )
    }

    private fun statusForWifiOnly(
        wifi: WifiAccessPointSnapshot
    ): String =
        if (wifi.bssid != null) {
            STATUS_WIFI_ONLY
        } else {
            LocationCapturePolicy.STATUS_UNAVAILABLE
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
        val capturedAtUtc: String,
        val bssid: String? = null,
        val rssi: Int? = null
    )

    @kotlinx.serialization.Serializable
    private data class StatusOnlyPayload(
        val source: String,
        val status: String,
        val bssid: String? = null,
        val rssi: Int? = null
    )

    companion object {
        private const val PREFS_NAME =
            "titanmdm_location_worker"

        private const val KEY_LAST_REPORT_AT =
            "last_proactive_report_at"

        private const val WIFI_SOURCE =
            "AndroidAgent-Proactive-WifiBssid"

        private const val STATUS_WIFI_ONLY =
            "WIFI_ONLY"

        private const val
        LOCATION_FIX_TIMEOUT_MS = 10_000L
    }
}
