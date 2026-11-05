package com.titanmdm.agent.service

import android.Manifest
import android.annotation.SuppressLint
import android.app.Notification
import android.app.NotificationChannel
import android.app.NotificationManager
import android.app.PendingIntent
import android.app.Service
import android.content.Context
import android.content.Intent
import android.content.pm.PackageManager
import android.content.pm.ServiceInfo
import android.os.Build
import android.os.IBinder
import android.os.PowerManager
import android.provider.Settings
import android.util.Log
import androidx.core.app.NotificationCompat
import androidx.core.app.ServiceCompat
import androidx.core.content.ContextCompat
import androidx.core.net.toUri
import com.titanmdm.agent.MainActivity
import com.titanmdm.agent.commands.CommandRepository
import com.titanmdm.agent.core.storage.AgentIdentityStore
import com.titanmdm.agent.heartbeat.HeartbeatRepository
import com.titanmdm.agent.heartbeat.HeartbeatResult
import com.titanmdm.agent.workers.AgentWorkScheduler
import kotlinx.coroutines.CoroutineScope
import kotlinx.coroutines.Dispatchers
import kotlinx.coroutines.Job
import kotlinx.coroutines.SupervisorJob
import kotlinx.coroutines.cancel
import kotlinx.coroutines.delay
import kotlinx.coroutines.isActive
import kotlinx.coroutines.launch

/**
 * Servicio en primer plano del agente: mantiene el equipo en línea
 * aunque el operador salga de la app (Android suspende la app y los
 * trabajos periódicos de WorkManager no bajan de 15 minutos).
 *
 * Ciclo: heartbeat + comandos cada [SYNC_INTERVAL_MS]; ubicación cada
 * [LOCATION_EVERY_CYCLES] ciclos (el servidor evalúa las geocercas y
 * su política de captura sigue mandando sobre frecuencia y batería).
 * WorkManager queda como respaldo si el sistema detiene el servicio.
 */
class AgentForegroundService : Service() {

    private val scope = CoroutineScope(SupervisorJob() + Dispatchers.IO)
    private var loop: Job? = null

    override fun onBind(intent: Intent?): IBinder? = null

    override fun onStartCommand(intent: Intent?, flags: Int, startId: Int): Int {
        if (!startInForeground()) {
            stopSelf()
            return START_NOT_STICKY
        }

        if (loop?.isActive != true) {
            loop = scope.launch { runLoop() }
        }
        return START_STICKY
    }

    override fun onDestroy() {
        scope.cancel()
        super.onDestroy()
    }

    private suspend fun runLoop() {
        val identityStore = AgentIdentityStore(applicationContext)
        val heartbeat = HeartbeatRepository(applicationContext)
        val commands = CommandRepository(applicationContext)
        var cycle = 0

        while (scope.isActive) {
            if (identityStore.get() == null) {
                // Sin inscripción no hay nada que mantener vivo.
                stopSelf()
                return
            }

            runCatching {
                when (val result = heartbeat.send()) {
                    HeartbeatResult.Success -> commands.pollAndExecute()
                    HeartbeatResult.NotEnrolled -> Unit
                    is HeartbeatResult.Failure -> Log.w(TAG, "Heartbeat: ${result.message}")
                }
            }.onFailure { Log.w(TAG, "Ciclo de sincronización fallido.", it) }

            if (cycle % LOCATION_EVERY_CYCLES == 0) {
                runCatching { AgentWorkScheduler.reportLocationNow(applicationContext) }
            }

            cycle++
            delay(SYNC_INTERVAL_MS)
        }
    }

    private fun startInForeground(): Boolean {
        ensureChannel(this)
        val notification = buildNotification(this)

        // Android 14+: el tipo "location" exige permiso de ubicación concedido.
        val type = if (Build.VERSION.SDK_INT >= Build.VERSION_CODES.Q) {
            ServiceInfo.FOREGROUND_SERVICE_TYPE_DATA_SYNC or
                (if (hasLocationPermission(this)) ServiceInfo.FOREGROUND_SERVICE_TYPE_LOCATION else 0)
        } else 0

        return runCatching {
            ServiceCompat.startForeground(this, NOTIFICATION_ID, notification, type)
            true
        }.recoverCatching {
            // Arranque desde segundo plano sin permiso para "location": solo sincronización.
            if (Build.VERSION.SDK_INT >= Build.VERSION_CODES.Q) {
                ServiceCompat.startForeground(
                    this,
                    NOTIFICATION_ID,
                    notification,
                    ServiceInfo.FOREGROUND_SERVICE_TYPE_DATA_SYNC
                )
            }
            true
        }.getOrElse {
            Log.w(TAG, "No fue posible iniciar el servicio en primer plano.", it)
            false
        }
    }

    companion object {
        private const val TAG = "TitanAgentService"
        private const val CHANNEL_ID = "titan_agent_service"
        private const val NOTIFICATION_ID = 7301

        const val SYNC_INTERVAL_MS = 60_000L
        const val LOCATION_EVERY_CYCLES = 5

        /** Idempotente: tras inscribirse, al arrancar el equipo y al abrir la app. */
        fun start(context: Context) {
            val app = context.applicationContext
            // Sin inscripción el servicio se detiene solo en su primer ciclo.
            runCatching {
                ContextCompat.startForegroundService(app, Intent(app, AgentForegroundService::class.java))
            }.onFailure { Log.w(TAG, "No fue posible iniciar el servicio.", it) }
        }

        fun isIgnoringBatteryOptimizations(context: Context): Boolean {
            val power = context.getSystemService(PowerManager::class.java) ?: return true
            return power.isIgnoringBatteryOptimizations(context.packageName)
        }

        /** Pide al usuario excluir el agente de la optimización de batería. */
        @SuppressLint("BatteryLife")
        fun batteryOptimizationIntent(context: Context): Intent =
            Intent(
                Settings.ACTION_REQUEST_IGNORE_BATTERY_OPTIMIZATIONS,
                "package:${context.packageName}".toUri()
            )

        private fun hasLocationPermission(context: Context): Boolean =
            ContextCompat.checkSelfPermission(context, Manifest.permission.ACCESS_FINE_LOCATION) ==
                PackageManager.PERMISSION_GRANTED ||
                ContextCompat.checkSelfPermission(context, Manifest.permission.ACCESS_COARSE_LOCATION) ==
                PackageManager.PERMISSION_GRANTED

        private fun ensureChannel(context: Context) {
            val manager = context.getSystemService(NotificationManager::class.java) ?: return
            if (manager.getNotificationChannel(CHANNEL_ID) != null) return
            manager.createNotificationChannel(
                NotificationChannel(
                    CHANNEL_ID,
                    "Conexión con TitanMDM",
                    NotificationManager.IMPORTANCE_LOW
                ).apply {
                    description = "Mantiene el equipo sincronizado con la consola TitanMDM."
                    setShowBadge(false)
                }
            )
        }

        private fun buildNotification(context: Context): Notification {
            val open = PendingIntent.getActivity(
                context,
                0,
                Intent(context, MainActivity::class.java),
                PendingIntent.FLAG_IMMUTABLE or PendingIntent.FLAG_UPDATE_CURRENT
            )
            return NotificationCompat.Builder(context, CHANNEL_ID)
                .setSmallIcon(android.R.drawable.stat_notify_sync)
                .setContentTitle("TitanMDM activo")
                .setContentText("Equipo administrado y sincronizado.")
                .setOngoing(true)
                .setOnlyAlertOnce(true)
                .setContentIntent(open)
                .setForegroundServiceBehavior(NotificationCompat.FOREGROUND_SERVICE_IMMEDIATE)
                .build()
        }
    }
}
