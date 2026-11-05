package com.titanmdm.agent.workers

import android.content.Context
import androidx.work.BackoffPolicy
import androidx.work.Constraints
import androidx.work.CoroutineWorker
import androidx.work.ExistingWorkPolicy
import androidx.work.NetworkType
import androidx.work.OneTimeWorkRequestBuilder
import androidx.work.WorkManager
import androidx.work.WorkerParameters
import androidx.work.workDataOf
import com.titanmdm.agent.core.network.LocationReportRequest
import com.titanmdm.agent.core.network.TitanApiClient
import com.titanmdm.agent.core.storage.AgentIdentityStore
import kotlinx.serialization.Serializable
import kotlinx.serialization.encodeToString
import kotlinx.serialization.json.Json
import java.time.Instant
import java.util.concurrent.TimeUnit

/*
 * P1 Geofencing proactivo: envia al servidor una
 * transicion de zona en cuanto el SO la detecta.
 * Source = AndroidAgent-GeofenceTransition; el servidor
 * genera GeofenceEvent + dispara automatizaciones
 * GeofenceEnter / GeofenceExit.
 */
class GeofenceTransitionWorker(
    appContext: Context,
    params: WorkerParameters
) : CoroutineWorker(appContext, params) {

    private val json = Json {
        encodeDefaults = true
        explicitNulls = false
    }

    override suspend fun doWork(): Result {
        val geofenceId = inputData.getString(KEY_GEOFENCE_ID) ?: return Result.failure()
        val transition = inputData.getString(KEY_TRANSITION) ?: return Result.failure()

        val identity =
            AgentIdentityStore(applicationContext).get()
                ?: return Result.success()

        val latitude = inputData.getDouble(KEY_LATITUDE, Double.NaN)
        val longitude = inputData.getDouble(KEY_LONGITUDE, Double.NaN)
        val accuracy = inputData.getDouble(KEY_ACCURACY, Double.NaN)
        val capturedAt = inputData.getLong(KEY_CAPTURED_AT, System.currentTimeMillis())

        val hasFix = !latitude.isNaN() && !longitude.isNaN()

        val payload =
            TransitionPayload(
                latitude = if (hasFix) latitude else null,
                longitude = if (hasFix) longitude else null,
                accuracyMeters = if (accuracy.isNaN()) null else accuracy,
                source = SOURCE,
                status = if (hasFix) null else "UNAVAILABLE",
                capturedAtUtc = Instant.ofEpochMilli(capturedAt).toString(),
                geofenceId = geofenceId,
                transition = transition
            )

        return try {
            val response =
                TitanApiClient.service.reportLocation(
                    identity.deviceId,
                    identity.deviceSecret,
                    LocationReportRequest(json.encodeToString(payload))
                )

            when {
                response.isSuccessful -> Result.success()
                // 4xx: el servidor rechazo el payload; reintentar no ayuda.
                response.code() in 400..499 -> Result.failure()
                runAttemptCount >= MAX_ATTEMPTS -> Result.failure()
                else -> Result.retry()
            }
        } catch (e: Exception) {
            if (runAttemptCount >= MAX_ATTEMPTS) Result.failure() else Result.retry()
        }
    }

    @Serializable
    private data class TransitionPayload(
        val latitude: Double? = null,
        val longitude: Double? = null,
        val accuracyMeters: Double? = null,
        val source: String,
        val status: String? = null,
        val capturedAtUtc: String,
        val geofenceId: String,
        val transition: String
    )

    companion object {
        const val SOURCE = "AndroidAgent-GeofenceTransition"

        private const val MAX_ATTEMPTS = 8
        private const val KEY_GEOFENCE_ID = "geofenceId"
        private const val KEY_TRANSITION = "transition"
        private const val KEY_LATITUDE = "latitude"
        private const val KEY_LONGITUDE = "longitude"
        private const val KEY_ACCURACY = "accuracy"
        private const val KEY_CAPTURED_AT = "capturedAt"

        fun enqueue(
            context: Context,
            geofenceId: String,
            transition: String,
            latitude: Double?,
            longitude: Double?,
            accuracyMeters: Double?,
            capturedAtEpochMillis: Long
        ) {
            val request =
                OneTimeWorkRequestBuilder<GeofenceTransitionWorker>()
                    .setInputData(
                        workDataOf(
                            KEY_GEOFENCE_ID to geofenceId,
                            KEY_TRANSITION to transition,
                            KEY_LATITUDE to (latitude ?: Double.NaN),
                            KEY_LONGITUDE to (longitude ?: Double.NaN),
                            KEY_ACCURACY to (accuracyMeters ?: Double.NaN),
                            KEY_CAPTURED_AT to capturedAtEpochMillis
                        )
                    )
                    .setConstraints(
                        Constraints.Builder()
                            .setRequiredNetworkType(NetworkType.CONNECTED)
                            .build()
                    )
                    .setBackoffCriteria(BackoffPolicy.EXPONENTIAL, 30, TimeUnit.SECONDS)
                    .addTag("titanmdm-geofence-transition")
                    .build()

            // Una transicion por geocerca+tipo+instante: APPEND conserva el orden Enter -> Exit.
            WorkManager
                .getInstance(context)
                .enqueueUniqueWork(
                    "titanmdm-geofence-$geofenceId",
                    ExistingWorkPolicy.APPEND_OR_REPLACE,
                    request
                )
        }
    }
}
