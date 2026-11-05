package com.titanmdm.agent.location

import android.Manifest
import android.annotation.SuppressLint
import android.app.PendingIntent
import android.content.Context
import android.content.Intent
import android.content.pm.PackageManager
import android.os.Build
import androidx.core.content.ContextCompat
import com.google.android.gms.location.Geofence
import com.google.android.gms.location.GeofencingRequest
import com.google.android.gms.location.LocationServices
import com.titanmdm.agent.core.network.DeviceGeofenceResponse
import com.titanmdm.agent.receivers.GeofenceBroadcastReceiver
import kotlinx.coroutines.suspendCancellableCoroutine
import kotlin.coroutines.resume

/*
 * P1 Geofencing proactivo.
 *
 * Registra en el SO (GeofencingClient) las geocercas
 * asignadas por el servidor. El SO despierta al agente
 * en cada cruce de zona: la ubicacion deja de depender
 * solo del muestreo periodico de 15 minutos.
 *
 * - Requiere ACCESS_FINE_LOCATION y, desde Android 10,
 *   ACCESS_BACKGROUND_LOCATION (concedido por politica
 *   de Device Owner). Sin permisos no registra nada.
 * - Android limita a 100 geocercas por app.
 * - El registro se pierde al reiniciar: BootReceiver y
 *   LocationReportingWorker vuelven a sincronizar.
 */
object GeofenceRegistrar {

    const val MAX_GEOFENCES = 100

    private const val MIN_RADIUS_METERS = 100f

    private const val REQUEST_CODE = 4127

    private const val PREFS_NAME = "titanmdm_geofences"

    private const val KEY_SIGNATURE = "registered_signature"

    fun hasRequiredPermissions(context: Context): Boolean {
        val fine =
            ContextCompat.checkSelfPermission(
                context,
                Manifest.permission.ACCESS_FINE_LOCATION
            ) == PackageManager.PERMISSION_GRANTED

        if (!fine) {
            return false
        }

        if (Build.VERSION.SDK_INT < Build.VERSION_CODES.Q) {
            return true
        }

        return ContextCompat.checkSelfPermission(
            context,
            Manifest.permission.ACCESS_BACKGROUND_LOCATION
        ) == PackageManager.PERMISSION_GRANTED
    }

    /*
     * Firma estable del conjunto de geocercas: evita
     * re-registrar (y reiniciar el estado del SO) si
     * nada cambio en el servidor.
     */
    fun signatureOf(geofences: List<DeviceGeofenceResponse>): String {
        return geofences
            .sortedBy { it.id }
            .joinToString("|") {
                "${it.id}:${it.latitude}:${it.longitude}:" +
                    "${it.radiusMeters}:${it.alertOnEnter}:${it.alertOnExit}"
            }
    }

    fun transitionTypesOf(geofence: DeviceGeofenceResponse): Int {
        var types = 0

        if (geofence.alertOnEnter) {
            types = types or Geofence.GEOFENCE_TRANSITION_ENTER
        }

        if (geofence.alertOnExit) {
            types = types or Geofence.GEOFENCE_TRANSITION_EXIT
        }

        return types
    }

    @SuppressLint("MissingPermission")
    suspend fun sync(
        context: Context,
        geofences: List<DeviceGeofenceResponse>,
        force: Boolean = false
    ): Boolean {
        val appContext = context.applicationContext

        val eligible =
            geofences
                .filter { transitionTypesOf(it) != 0 }
                .take(MAX_GEOFENCES)

        val signature = signatureOf(eligible)

        val prefs =
            appContext.getSharedPreferences(PREFS_NAME, Context.MODE_PRIVATE)

        if (!force && prefs.getString(KEY_SIGNATURE, null) == signature) {
            return true
        }

        val client = LocationServices.getGeofencingClient(appContext)
        val pendingIntent = pendingIntent(appContext)

        awaitTask { client.removeGeofences(pendingIntent) }

        if (eligible.isEmpty()) {
            prefs.edit().putString(KEY_SIGNATURE, signature).apply()
            return true
        }

        if (!hasRequiredPermissions(appContext)) {
            prefs.edit().remove(KEY_SIGNATURE).apply()
            return false
        }

        val request =
            GeofencingRequest.Builder()
                .setInitialTrigger(
                    GeofencingRequest.INITIAL_TRIGGER_ENTER
                )
                .addGeofences(
                    eligible.map { geofence ->
                        Geofence.Builder()
                            .setRequestId(geofence.id)
                            .setCircularRegion(
                                geofence.latitude,
                                geofence.longitude,
                                maxOf(
                                    geofence.radiusMeters.toFloat(),
                                    MIN_RADIUS_METERS
                                )
                            )
                            .setExpirationDuration(Geofence.NEVER_EXPIRE)
                            .setTransitionTypes(transitionTypesOf(geofence))
                            .setNotificationResponsiveness(60_000)
                            .build()
                    }
                )
                .build()

        val added = awaitTask { client.addGeofences(request, pendingIntent) }

        if (added) {
            prefs.edit().putString(KEY_SIGNATURE, signature).apply()
        } else {
            prefs.edit().remove(KEY_SIGNATURE).apply()
        }

        return added
    }

    /*
     * Tras reinicio el SO olvida las geocercas: se
     * invalida la firma para forzar el re-registro.
     */
    fun invalidate(context: Context) {
        context.applicationContext
            .getSharedPreferences(PREFS_NAME, Context.MODE_PRIVATE)
            .edit()
            .remove(KEY_SIGNATURE)
            .apply()
    }

    private fun pendingIntent(context: Context): PendingIntent {
        val intent = Intent(context, GeofenceBroadcastReceiver::class.java)

        // GeofencingClient agrega extras: el intent debe ser mutable (API 31+).
        val flags =
            PendingIntent.FLAG_UPDATE_CURRENT or
                if (Build.VERSION.SDK_INT >= Build.VERSION_CODES.S) {
                    PendingIntent.FLAG_MUTABLE
                } else {
                    0
                }

        return PendingIntent.getBroadcast(context, REQUEST_CODE, intent, flags)
    }

    private suspend fun awaitTask(
        start: () -> com.google.android.gms.tasks.Task<Void>
    ): Boolean {
        return try {
            suspendCancellableCoroutine { cont ->
                start()
                    .addOnSuccessListener { if (cont.isActive) cont.resume(true) }
                    .addOnFailureListener { if (cont.isActive) cont.resume(false) }
            }
        } catch (e: Exception) {
            false
        }
    }
}
