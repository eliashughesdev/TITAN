package com.titanmdm.agent.service

import android.Manifest
import android.annotation.SuppressLint
import android.app.AlarmManager
import android.app.Notification
import android.app.NotificationChannel
import android.app.NotificationManager
import android.app.PendingIntent
import android.app.Service
import android.content.Context
import android.content.Intent
import android.content.pm.PackageManager
import android.content.pm.ServiceInfo
import android.net.ConnectivityManager
import android.net.Network
import android.os.Build
import android.os.IBinder
import android.os.PowerManager
import android.os.SystemClock
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
import com.titanmdm.agent.receivers.HeartbeatWatchdogReceiver
import com.titanmdm.agent.workers.AgentWorkScheduler
import kotlinx.coroutines.CoroutineScope
import kotlinx.coroutines.Dispatchers
import kotlinx.coroutines.Job
import kotlinx.coroutines.SupervisorJob
import kotlinx.coroutines.cancel
import kotlinx.coroutines.channels.Channel
import kotlinx.coroutines.isActive
import kotlinx.coroutines.launch
import kotlinx.coroutines.withTimeoutOrNull
import kotlin.random.Random

/**
 * Servicio en primer plano del agente. Mantiene el heartbeat y el canal
 * de comandos aunque el operador minimice la app, la pantalla se apague
 * o Android intente suspender el proceso.
 *
 * Defensas:
 * - START_STICKY y notificacion persistente de alta prioridad.
 * - Wake lock parcial mientras el servicio esta activo.
 * - Reintentos con backoff exponencial y jitter.
 * - NetworkCallback que despierta el ciclo al recuperar conectividad.
 * - Watchdog de AlarmManager para relanzar el servicio cada 15 minutos.
 * - Exencion de optimizacion de bateria solicitada desde la UI.
 */
class AgentForegroundService : Service() {

    private val scope = CoroutineScope(SupervisorJob() + Dispatchers.IO)
    private val retrySignal = Channel<Unit>(Channel.CONFLATED)

    private var loop: Job? = null
    private var wakeLock: PowerManager.WakeLock? = null
    private var networkCallback: ConnectivityManager.NetworkCallback? = null

    override fun onBind(intent: Intent?): IBinder? = null

    override fun onStartCommand(intent: Intent?, flags: Int, startId: Int): Int {
        if (!startInForeground()) {
            stopSelf()
            return START_NOT_STICKY
        }

        acquireWakeLock()
        registerNetworkWatchdog()
        scheduleWatchdog(this)

        if (loop?.isActive != true) {
            loop = scope.launch { runLoop() }
        }
        return START_STICKY
    }

    override fun onDestroy() {
        unregisterNetworkWatchdog()
        releaseWakeLock()
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
                stopSelf()
                break
            }

            when (synchronize(heartbeat, commands)) {
                SyncOutcome.Success -> {
                    if (cycle % LOCATION_EVERY_CYCLES == 0) {
                        runCatching {
                            AgentWorkScheduler.reportLocationNow(applicationContext)
                        }
                    }

                    cycle++
                    waitForSignalOrTimeout(SYNC_INTERVAL_MS)
                }

                SyncOutcome.NotEnrolled -> {
                    stopSelf()
                    break
                }

                SyncOutcome.Failed -> {
                    waitForSignalOrTimeout(FAILED_CYCLE_INTERVAL_MS)
                }
            }
        }
    }

    private suspend fun synchronize(
        heartbeat: HeartbeatRepository,
        commands: CommandRepository
    ): SyncOutcome {
        var attempt = 0

        while (scope.isActive) {
            val result = runCatching { heartbeat.send() }
                .getOrElse {
                    HeartbeatResult.Failure(
                        message = it.message ?: "Error de comunicacion.",
                        retryable = true
                    )
                }

            when (result) {
                HeartbeatResult.Success -> {
                    runCatching { commands.pollAndExecute() }
                        .onFailure {
                            Log.w(TAG, "No fue posible consultar comandos.", it)
                        }
                    return SyncOutcome.Success
                }

                HeartbeatResult.NotEnrolled -> {
                    return SyncOutcome.NotEnrolled
                }

                is HeartbeatResult.Failure -> {
                    Log.w(TAG, "Heartbeat: ${result.message}")

                    if (!result.retryable || attempt >= MAX_RETRY_ATTEMPTS) {
                        return SyncOutcome.Failed
                    }

                    waitForSignalOrTimeout(backoffMillis(attempt))
                    attempt++
                }
            }
        }

        return SyncOutcome.Failed
    }

    private suspend fun waitForSignalOrTimeout(timeoutMillis: Long) {
        withTimeoutOrNull(timeoutMillis) {
            retrySignal.receive()
        }
    }

    private fun backoffMillis(attempt: Int): Long {
        val exponential =
            BASE_RETRY_DELAY_MS * (1L shl attempt.coerceAtMost(5))

        return exponential
            .coerceAtMost(MAX_RETRY_DELAY_MS) +
            Random.nextLong(0L, RETRY_JITTER_MS)
    }

    @SuppressLint("WakelockTimeout")
    private fun acquireWakeLock() {
        if (wakeLock?.isHeld == true) {
            return
        }

        val power = getSystemService(PowerManager::class.java) ?: return

        runCatching {
            wakeLock = power
                .newWakeLock(
                    PowerManager.PARTIAL_WAKE_LOCK,
                    "$packageName:TitanHeartbeat"
                )
                .apply {
                    setReferenceCounted(false)
                    acquire()
                }
        }.onFailure {
            Log.w(TAG, "No fue posible adquirir el wake lock.", it)
        }
    }

    private fun releaseWakeLock() {
        wakeLock?.let {
            runCatching {
                if (it.isHeld) {
                    it.release()
                }
            }
        }
        wakeLock = null
    }

    private fun registerNetworkWatchdog() {
        if (networkCallback != null) {
            return
        }

        val connectivity =
            getSystemService(ConnectivityManager::class.java) ?: return

        val callback = object : ConnectivityManager.NetworkCallback() {
            override fun onAvailable(network: Network) {
                retrySignal.trySend(Unit)
            }

            override fun onLost(network: Network) {
                Log.w(TAG, "Sin red; el heartbeat reintentara con backoff.")
            }
        }

        runCatching {
            connectivity.registerDefaultNetworkCallback(callback)
            networkCallback = callback
        }.onFailure {
            Log.w(TAG, "No fue posible registrar NetworkCallback.", it)
        }
    }

    private fun unregisterNetworkWatchdog() {
        val callback = networkCallback ?: return
        val connectivity =
            getSystemService(ConnectivityManager::class.java)

        runCatching {
            connectivity?.unregisterNetworkCallback(callback)
        }

        networkCallback = null
    }

    private fun startInForeground(): Boolean {
        ensureChannel(this)
        val notification = buildNotification(this)

        val locationType =
            if (Build.VERSION.SDK_INT >= Build.VERSION_CODES.Q &&
                hasLocationPermission(this)
            ) {
                ServiceInfo.FOREGROUND_SERVICE_TYPE_LOCATION
            } else {
                0
            }

        val baseType =
            when {
                Build.VERSION.SDK_INT >= 34 ->
                    ServiceInfo.FOREGROUND_SERVICE_TYPE_SPECIAL_USE

                Build.VERSION.SDK_INT >= Build.VERSION_CODES.Q ->
                    ServiceInfo.FOREGROUND_SERVICE_TYPE_DATA_SYNC

                else -> 0
            }

        val primaryType = baseType or locationType

        return runCatching {
            ServiceCompat.startForeground(
                this,
                NOTIFICATION_ID,
                notification,
                primaryType
            )
            true
        }.recoverCatching {
            /*
             * Si el tipo combinado no esta permitido en este fabricante,
             * se degrada al tipo base sin ubicacion.
             */
            ServiceCompat.startForeground(
                this,
                NOTIFICATION_ID,
                notification,
                baseType
            )
            true
        }.recoverCatching {
            /*
             * Ultimo respaldo para Android 10-13: dataSync.
             */
            ServiceCompat.startForeground(
                this,
                NOTIFICATION_ID,
                notification,
                if (Build.VERSION.SDK_INT >= Build.VERSION_CODES.Q) {
                    ServiceInfo.FOREGROUND_SERVICE_TYPE_DATA_SYNC
                } else {
                    0
                }
            )
            true
        }.getOrElse {
            Log.w(TAG, "No fue posible iniciar el servicio en primer plano.", it)
            false
        }
    }

    private enum class SyncOutcome {
        Success,
        NotEnrolled,
        Failed
    }

    companion object {
        private const val TAG = "TitanAgentService"
        private const val CHANNEL_ID = "titan_agent_service_v2"
        private const val NOTIFICATION_ID = 7301

        private const val SYNC_INTERVAL_MS = 60_000L
        private const val FAILED_CYCLE_INTERVAL_MS = 30_000L
        private const val LOCATION_EVERY_CYCLES = 5

        private const val BASE_RETRY_DELAY_MS = 2_000L
        private const val MAX_RETRY_DELAY_MS = 60_000L
        private const val RETRY_JITTER_MS = 1_000L
        private const val MAX_RETRY_ATTEMPTS = 5

        private const val WATCHDOG_INTERVAL_MS = 15 * 60 * 1000L

        /** Idempotente: tras inscribirse, al arrancar el equipo y al abrir la app. */
        fun start(context: Context) {
            val app = context.applicationContext
            runCatching {
                ContextCompat.startForegroundService(
                    app,
                    Intent(app, AgentForegroundService::class.java)
                )
            }.onFailure {
                Log.w(TAG, "No fue posible iniciar el servicio.", it)
            }
        }

        /**
         * Watchdog independiente del proceso: si el fabricante mata el
         * servicio, AlarmManager lo relanza cada 15 minutos.
         */
        fun scheduleWatchdog(context: Context) {
            val alarmManager =
                context.getSystemService(AlarmManager::class.java) ?: return

            val pendingIntent = PendingIntent.getBroadcast(
                context,
                WATCHDOG_REQUEST_CODE,
                Intent(
                    context,
                    HeartbeatWatchdogReceiver::class.java
                ),
                PendingIntent.FLAG_IMMUTABLE or
                    PendingIntent.FLAG_UPDATE_CURRENT
            )

            runCatching {
                alarmManager.setInexactRepeating(
                    AlarmManager.ELAPSED_REALTIME_WAKEUP,
                    SystemClock.elapsedRealtime() + WATCHDOG_INTERVAL_MS,
                    WATCHDOG_INTERVAL_MS,
                    pendingIntent
                )
            }.onFailure {
                Log.w(TAG, "No fue posible programar el watchdog.", it)
            }
        }

        fun isIgnoringBatteryOptimizations(context: Context): Boolean {
            val power =
                context.getSystemService(PowerManager::class.java) ?: return true
            return power.isIgnoringBatteryOptimizations(context.packageName)
        }

        /** Pide al usuario excluir el agente de la optimizacion de bateria. */
        @SuppressLint("BatteryLife")
        fun batteryOptimizationIntent(context: Context): Intent =
            Intent(
                Settings.ACTION_REQUEST_IGNORE_BATTERY_OPTIMIZATIONS,
                "package:${context.packageName}".toUri()
            )

        private const val WATCHDOG_REQUEST_CODE = 9001

        private fun hasLocationPermission(context: Context): Boolean =
            ContextCompat.checkSelfPermission(
                context,
                Manifest.permission.ACCESS_FINE_LOCATION
            ) == PackageManager.PERMISSION_GRANTED ||
                ContextCompat.checkSelfPermission(
                    context,
                    Manifest.permission.ACCESS_COARSE_LOCATION
                ) == PackageManager.PERMISSION_GRANTED

        private fun ensureChannel(context: Context) {
            val manager =
                context.getSystemService(NotificationManager::class.java) ?: return
            if (manager.getNotificationChannel(CHANNEL_ID) != null) return

            manager.createNotificationChannel(
                NotificationChannel(
                    CHANNEL_ID,
                    "Conexion con TitanMDM",
                    NotificationManager.IMPORTANCE_HIGH
                ).apply {
                    description = "Mantiene el equipo sincronizado con la consola TitanMDM."
                    setSound(null, null)
                    enableVibration(false)
                    setShowBadge(false)
                    lockscreenVisibility =
                        Notification.VISIBILITY_PUBLIC
                }
            )
        }

        private fun buildNotification(context: Context): Notification {
            val open = PendingIntent.getActivity(
                context,
                0,
                Intent(context, MainActivity::class.java),
                PendingIntent.FLAG_IMMUTABLE or
                    PendingIntent.FLAG_UPDATE_CURRENT
            )
            return NotificationCompat.Builder(context, CHANNEL_ID)
                .setSmallIcon(android.R.drawable.stat_notify_sync)
                .setContentTitle("TitanMDM activo")
                .setContentText("Equipo administrado y sincronizado.")
                .setCategory(NotificationCompat.CATEGORY_SERVICE)
                .setPriority(NotificationCompat.PRIORITY_HIGH)
                .setVisibility(NotificationCompat.VISIBILITY_PUBLIC)
                .setOngoing(true)
                .setOnlyAlertOnce(true)
                .setSilent(true)
                .setContentIntent(open)
                .setForegroundServiceBehavior(
                    NotificationCompat.FOREGROUND_SERVICE_IMMEDIATE
                )
                .build()
        }
    }
}
