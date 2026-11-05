package com.titanmdm.agent.workers

import android.content.Context
import androidx.work.CoroutineWorker
import androidx.work.WorkerParameters
import com.titanmdm.agent.core.network.TitanApiClient
import com.titanmdm.agent.core.storage.AgentIdentityStore
import com.titanmdm.agent.location.GeofenceRegistrar

/*
 * P1 Geofencing proactivo: descarga las geocercas
 * asignadas y las registra en el SO. Se ejecuta al
 * arrancar/actualizar el agente y en cada ciclo del
 * LocationReportingWorker.
 */
class GeofenceSyncWorker(
    appContext: Context,
    params: WorkerParameters
) : CoroutineWorker(appContext, params) {

    override suspend fun doWork(): Result {
        val force = inputData.getBoolean(KEY_FORCE, false)

        return when (syncGeofences(applicationContext, force)) {
            true -> Result.success()
            false -> Result.retry()
            null -> Result.success()
        }
    }

    companion object {
        const val KEY_FORCE = "force"

        /*
         * true = sincronizado, false = error transitorio,
         * null = sin identidad / sin permisos (no reintentar).
         */
        suspend fun syncGeofences(context: Context, force: Boolean): Boolean? {
            val identity = AgentIdentityStore(context).get() ?: return null

            return try {
                val response =
                    TitanApiClient.service.getDeviceGeofences(
                        identity.deviceId,
                        identity.deviceSecret
                    )

                if (!response.isSuccessful) {
                    return false
                }

                val geofences = response.body().orEmpty()

                if (geofences.isNotEmpty() &&
                    !GeofenceRegistrar.hasRequiredPermissions(context)
                ) {
                    return null
                }

                GeofenceRegistrar.sync(context, geofences, force)
            } catch (e: Exception) {
                false
            }
        }
    }
}
