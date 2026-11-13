package com.titanmdm.agent.location

import android.Manifest
import android.annotation.SuppressLint
import android.content.Context
import android.content.pm.PackageManager
import android.os.BatteryManager
import android.os.PowerManager
import androidx.core.content.ContextCompat
import com.titanmdm.agent.core.network.LocationReportRequest
import com.titanmdm.agent.core.network.TitanApiClient
import com.titanmdm.agent.core.storage.AgentIdentityStore
import com.titanmdm.agent.workers.GeofenceSyncWorker
import kotlinx.coroutines.sync.Mutex
import kotlinx.coroutines.sync.withLock
import kotlinx.serialization.encodeToString
import kotlinx.serialization.json.Json
import java.time.Instant

/**
 * Captura y envio proactivo de ubicacion, independiente del planificador.
 *
 * Lo usan tanto el servicio en primer plano (muestreo periodico con la app
 * cerrada) como [com.titanmdm.agent.workers.LocationReportingWorker]
 * (respaldo de WorkManager). No inventa posiciones: sin permiso reporta
 * PERMISSION_DENIED y sin fix reporta UNAVAILABLE.
 */
sealed interface LocationReportOutcome {

    /** No hay identidad de agente: no se puede reportar. */
    data object NotEnrolled : LocationReportOutcome

    /** La politica o el intervalo indican que aun no toca reportar. */
    data object Skipped : LocationReportOutcome

    /** Se enviaron coordenadas reales. */
    data object Reported : LocationReportOutcome

    /** Se envio solo el estado de captura (sin coordenadas). */
    data object StatusReported : LocationReportOutcome

    /** Error transitorio; el llamador puede reintentar. */
    data object Retry : LocationReportOutcome
}

object LocationReporter {

    private const val PREFS_NAME = "titanmdm_location_worker"

    private const val KEY_LAST_REPORT_AT = "last_proactive_report_at"

    private const val LOCATION_FIX_TIMEOUT_MS = 10_000L

    private const val WIFI_SOURCE = "AndroidAgent-Proactive-WifiBssid"

    private val mutex = Mutex()

    private val json = Json {
        encodeDefaults = true
        explicitNulls = false
    }

    suspend fun report(context: Context): LocationReportOutcome =
        mutex.withLock {
            reportLocked(context.applicationContext)
        }

    @SuppressLint("MissingPermission")
    private suspend fun reportLocked(context: Context): LocationReportOutcome {
        val identity =
            AgentIdentityStore(context).get()
                ?: return LocationReportOutcome.NotEnrolled

        // Mantener las geocercas del SO al dia (independiente del muestreo).
        runCatching { GeofenceSyncWorker.syncGeofences(context, force = false) }

        val policyResponse =
            runCatching {
                TitanApiClient.service.getReportingPolicy(
                    identity.deviceId,
                    identity.deviceSecret
                )
            }.getOrNull()
                ?: return LocationReportOutcome.Retry

        if (!policyResponse.isSuccessful) {
            return LocationReportOutcome.Retry
        }

        val policy =
            policyResponse.body()
                ?: return LocationReportOutcome.NotEnrolled

        val permissionGranted = hasLocationPermission(context)
        val wifi = WifiTelemetryProvider.read(context)

        if (!permissionGranted) {
            return uploadStatus(
                identity.deviceId,
                identity.deviceSecret,
                status = LocationCapturePolicy.STATUS_PERMISSION_DENIED,
                wifi = wifi
            )
        }

        val hybrid = HybridLocationProvider.capture(
            context,
            LOCATION_FIX_TIMEOUT_MS
        )

        val wifiSnapshot = hybrid.wifi

        val fix = hybrid.location?.let { location ->
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

        val decision = LocationCapturePolicy.decide(
            reportingEnabled = policy.isEnabled,
            permissionGranted = true,
            batteryPercent = readBatteryPercent(context),
            batterySaverActive = isBatterySaverActive(context),
            lastReportAtEpochMillis = readLastReportAtEpochMillis(context),
            nowEpochMillis = System.currentTimeMillis(),
            capturedFix = fix,
            reportingIntervalMinutes = policy.reportingIntervalMinutes
        )

        return when (decision) {
            is LocationCaptureDecision.Skip ->
                LocationReportOutcome.Skipped

            is LocationCaptureDecision.ReportStatusOnly ->
                uploadStatus(
                    identity.deviceId,
                    identity.deviceSecret,
                    status = decision.status,
                    wifi = wifiSnapshot
                )

            is LocationCaptureDecision.ReportCoordinates ->
                uploadCoordinates(
                    context,
                    identity.deviceId,
                    identity.deviceSecret,
                    decision.payload,
                    wifiSnapshot
                )
        }
    }

    private suspend fun uploadCoordinates(
        context: Context,
        deviceId: String,
        deviceSecret: String,
        payload: LocationCapturePayload,
        wifi: WifiAccessPointSnapshot
    ): LocationReportOutcome {
        val payloadJson = json.encodeToString(
            CoordinatesPayload(
                latitude = payload.latitude,
                longitude = payload.longitude,
                accuracyMeters = payload.accuracyMeters,
                altitudeMeters = payload.altitudeMeters,
                speedMetersPerSecond = payload.speedMetersPerSecond,
                source = payload.source,
                capturedAtUtc = payload.capturedAtUtc,
                bssid = wifi.bssid,
                rssi = wifi.rssi
            )
        )

        val uploaded = upload(deviceId, deviceSecret, payloadJson)

        if (uploaded == LocationReportOutcome.Reported) {
            writeLastReportAtEpochMillis(
                context,
                System.currentTimeMillis()
            )
        }

        return uploaded
    }

    private suspend fun uploadStatus(
        deviceId: String,
        deviceSecret: String,
        status: String,
        wifi: WifiAccessPointSnapshot
    ): LocationReportOutcome {
        val payloadJson = json.encodeToString(
            StatusOnlyPayload(
                source = WIFI_SOURCE,
                status = status,
                bssid = wifi.bssid,
                rssi = wifi.rssi
            )
        )

        return when (upload(deviceId, deviceSecret, payloadJson)) {
            LocationReportOutcome.Reported ->
                LocationReportOutcome.StatusReported

            else -> LocationReportOutcome.Retry
        }
    }

    private suspend fun upload(
        deviceId: String,
        deviceSecret: String,
        payloadJson: String
    ): LocationReportOutcome {
        val response =
            runCatching {
                TitanApiClient.service.reportLocation(
                    deviceId,
                    deviceSecret,
                    LocationReportRequest(payloadJson)
                )
            }.getOrNull()
                ?: return LocationReportOutcome.Retry

        return if (response.isSuccessful) {
            LocationReportOutcome.Reported
        } else {
            LocationReportOutcome.Retry
        }
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

    private fun readBatteryPercent(context: Context): Int? {
        val batteryManager =
            context.getSystemService(Context.BATTERY_SERVICE)
                as? BatteryManager

        val percent =
            batteryManager?.getIntProperty(
                BatteryManager.BATTERY_PROPERTY_CAPACITY
            )

        return percent?.takeIf { it in 0..100 }
    }

    private fun isBatterySaverActive(context: Context): Boolean {
        val powerManager =
            context.getSystemService(Context.POWER_SERVICE)
                as? PowerManager

        return powerManager?.isPowerSaveMode == true
    }

    private fun readLastReportAtEpochMillis(
        context: Context
    ): Long? {
        val value =
            context.getSharedPreferences(
                PREFS_NAME,
                Context.MODE_PRIVATE
            ).getLong(KEY_LAST_REPORT_AT, -1L)

        return if (value >= 0L) value else null
    }

    private fun writeLastReportAtEpochMillis(
        context: Context,
        epochMillis: Long
    ) {
        context.getSharedPreferences(
            PREFS_NAME,
            Context.MODE_PRIVATE
        )
            .edit()
            .putLong(KEY_LAST_REPORT_AT, epochMillis)
            .apply()
    }

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
}
