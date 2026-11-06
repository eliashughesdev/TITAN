package com.titanmdm.agent.alarm

import android.app.NotificationChannel
import android.app.NotificationManager
import android.app.PendingIntent
import android.content.Context
import android.content.Intent
import android.media.AudioAttributes
import android.media.AudioManager
import android.media.MediaPlayer
import android.media.RingtoneManager
import android.os.Build
import android.os.Handler
import android.os.Looper
import android.os.VibrationEffect
import android.os.Vibrator
import android.os.VibratorManager
import androidx.core.app.NotificationCompat
import androidx.core.content.ContextCompat

object DeviceAlarmController {

    const val DEFAULT_DURATION_SECONDS = 30
    private const val MAX_DURATION_SECONDS = 60
    private const val CHANNEL_ID = "titan_agent_alarm"
    private const val NOTIFICATION_ID = 7311

    private val handler = Handler(Looper.getMainLooper())
    private var player: MediaPlayer? = null
    private var vibrator: Vibrator? = null
    private var stopRunnable: Runnable? = null

    fun play(
        context: Context,
        durationSeconds: Int = DEFAULT_DURATION_SECONDS
    ): Boolean {
        val appContext = context.applicationContext
        stop(appContext)
        forceAlarmVolume(appContext)

        val uri =
            RingtoneManager.getDefaultUri(RingtoneManager.TYPE_ALARM)
                ?: RingtoneManager.getDefaultUri(RingtoneManager.TYPE_RINGTONE)
                ?: return false

        val mediaPlayer = runCatching {
            MediaPlayer().apply {
                setAudioAttributes(
                    AudioAttributes.Builder()
                        .setUsage(AudioAttributes.USAGE_ALARM)
                        .setContentType(AudioAttributes.CONTENT_TYPE_SONIFICATION)
                        .build()
                )
                setDataSource(appContext, uri)
                isLooping = true
                prepare()
                start()
            }
        }.getOrNull() ?: return false

        player = mediaPlayer
        startVibration(appContext)
        showNotification(appContext)

        val boundedSeconds = durationSeconds.coerceIn(5, MAX_DURATION_SECONDS)
        val runnable = Runnable { stop(appContext) }
        stopRunnable = runnable
        handler.postDelayed(runnable, boundedSeconds * 1000L)

        return true
    }

    fun stop(context: Context) {
        stopRunnable?.let(handler::removeCallbacks)
        stopRunnable = null

        player?.let { media ->
            runCatching { if (media.isPlaying) media.stop() }
            runCatching { media.release() }
        }
        player = null

        vibrator?.cancel()
        vibrator = null

        val manager =
            context.getSystemService(NotificationManager::class.java)
        manager?.cancel(NOTIFICATION_ID)
    }

    private fun forceAlarmVolume(context: Context) {
        val audio =
            context.getSystemService(AudioManager::class.java) ?: return
        val max = audio.getStreamMaxVolume(AudioManager.STREAM_ALARM)
        runCatching {
            audio.setStreamVolume(AudioManager.STREAM_ALARM, max, 0)
        }
    }

    private fun startVibration(context: Context) {
        val deviceVibrator =
            if (Build.VERSION.SDK_INT >= Build.VERSION_CODES.S) {
                context.getSystemService(VibratorManager::class.java)
                    ?.defaultVibrator
            } else {
                @Suppress("DEPRECATION")
                context.getSystemService(Context.VIBRATOR_SERVICE) as? Vibrator
            } ?: return

        if (!deviceVibrator.hasVibrator()) return

        val pattern = longArrayOf(0L, 900L, 450L)
        runCatching {
            deviceVibrator.vibrate(
                VibrationEffect.createWaveform(pattern, 0)
            )
            vibrator = deviceVibrator
        }
    }

    private fun showNotification(context: Context) {
        val manager =
            context.getSystemService(NotificationManager::class.java) ?: return

        if (Build.VERSION.SDK_INT >= Build.VERSION_CODES.O) {
            val channel = NotificationChannel(
                CHANNEL_ID,
                "Alarma remota",
                NotificationManager.IMPORTANCE_HIGH
            ).apply {
                description = "Alarma enviada desde TitanMDM."
                setSound(null, null)
                enableVibration(false)
            }
            manager.createNotificationChannel(channel)
        }

        val open = PendingIntent.getActivity(
            context,
            NOTIFICATION_ID,
            Intent(context, AlarmActivity::class.java)
                .addFlags(Intent.FLAG_ACTIVITY_NEW_TASK),
            PendingIntent.FLAG_IMMUTABLE or PendingIntent.FLAG_UPDATE_CURRENT
        )

        val notification = NotificationCompat.Builder(context, CHANNEL_ID)
            .setSmallIcon(android.R.drawable.ic_lock_silent_mode_off)
            .setContentTitle("Alarma remota TitanMDM")
            .setContentText("Toque para silenciar la alarma.")
            .setCategory(NotificationCompat.CATEGORY_ALARM)
            .setPriority(NotificationCompat.PRIORITY_MAX)
            .setOngoing(true)
            .setContentIntent(open)
            .setFullScreenIntent(open, true)
            .build()

        runCatching {
            if (Build.VERSION.SDK_INT < Build.VERSION_CODES.TIRAMISU ||
                ContextCompat.checkSelfPermission(
                    context,
                    android.Manifest.permission.POST_NOTIFICATIONS
                ) == android.content.pm.PackageManager.PERMISSION_GRANTED
            ) {
                manager.notify(NOTIFICATION_ID, notification)
            }
        }
    }
}
