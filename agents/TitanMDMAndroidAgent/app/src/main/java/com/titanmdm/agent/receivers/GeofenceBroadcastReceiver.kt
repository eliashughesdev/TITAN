package com.titanmdm.agent.receivers

import android.content.BroadcastReceiver
import android.content.Context
import android.content.Intent
import com.google.android.gms.location.Geofence
import com.google.android.gms.location.GeofencingEvent
import com.titanmdm.agent.workers.GeofenceTransitionWorker

/*
 * P1 Geofencing proactivo: recibe las transiciones que
 * detecta el SO y delega el envio a WorkManager (con
 * reintentos y restriccion de red). El receiver no hace
 * I/O de red para respetar el limite de ~10 s.
 */
class GeofenceBroadcastReceiver : BroadcastReceiver() {

    override fun onReceive(context: Context, intent: Intent) {
        val event = GeofencingEvent.fromIntent(intent) ?: return

        if (event.hasError()) {
            return
        }

        val transition =
            when (event.geofenceTransition) {
                Geofence.GEOFENCE_TRANSITION_ENTER -> "Enter"
                Geofence.GEOFENCE_TRANSITION_EXIT -> "Exit"
                Geofence.GEOFENCE_TRANSITION_DWELL -> "Dwell"
                else -> return
            }

        val location = event.triggeringLocation

        event.triggeringGeofences.orEmpty().forEach { geofence ->
            GeofenceTransitionWorker.enqueue(
                context = context.applicationContext,
                geofenceId = geofence.requestId,
                transition = transition,
                latitude = location?.latitude,
                longitude = location?.longitude,
                accuracyMeters =
                    if (location?.hasAccuracy() == true) {
                        location.accuracy.toDouble()
                    } else {
                        null
                    },
                capturedAtEpochMillis =
                    location?.time ?: System.currentTimeMillis()
            )
        }
    }
}
