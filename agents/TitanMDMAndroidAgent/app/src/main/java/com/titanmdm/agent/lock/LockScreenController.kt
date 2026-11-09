package com.titanmdm.agent.lock

import android.Manifest
import android.app.NotificationChannel
import android.app.NotificationManager
import android.app.PendingIntent
import android.app.admin.DevicePolicyManager
import android.content.Context
import android.content.Intent
import android.content.pm.PackageManager
import android.util.Base64
import android.util.Log
import androidx.core.app.NotificationCompat
import androidx.core.content.ContextCompat
import androidx.core.content.edit
import com.titanmdm.agent.admin.TitanDeviceAdminReceiver
import java.security.SecureRandom

/**
 * Bloqueo remoto con PIN temporal y mensaje corporativo (modo extravío).
 *
 *  - PIN: DevicePolicyManager.resetPassword() ya no funciona para Device
 *    Owner desde Android 8 (targetSdk >= 26 lanza SecurityException). Se usa
 *    resetPasswordWithToken() con un token propio registrado por adelantado;
 *    en un equipo sin bloqueo previo (de fábrica) el token queda activo al
 *    instante.
 *  - Mensaje: setDeviceOwnerLockScreenInfo() lo pinta en la pantalla de
 *    bloqueo del sistema, donde se teclea el PIN, y sobrevive a reinicios.
 *    Además se muestra a pantalla completa LockMessageActivity.
 *  - El estado persiste hasta UNLOCK_DEVICE desde la consola.
 */
object LockScreenController {

    private const val TAG = "TitanLock"
    private const val PREFS = "titan_lock_state"
    private const val KEY_TOKEN = "reset_token"
    private const val KEY_ACTIVE = "active"
    private const val KEY_MESSAGE = "message"
    private const val KEY_PIN_SET = "pin_set"
    private const val KEY_SINCE = "since"
    private const val CHANNEL_ID = "titan_lock_message"
    private const val NOTIFICATION_ID = 7312

    data class State(val active: Boolean, val message: String?, val pinSet: Boolean, val sinceMs: Long)

    data class Outcome(
        val success: Boolean,
        val errorCode: String? = null,
        val errorMessage: String? = null,
        val pinApplied: Boolean = false,
        val messageShown: Boolean = false,
        val locked: Boolean = false
    )

    fun state(context: Context): State {
        val prefs = prefs(context)
        return State(
            active = prefs.getBoolean(KEY_ACTIVE, false),
            message = prefs.getString(KEY_MESSAGE, null),
            pinSet = prefs.getBoolean(KEY_PIN_SET, false),
            sinceMs = prefs.getLong(KEY_SINCE, 0L)
        )
    }

    /**
     * Registra el token de restablecimiento si falta. Se llama al arrancar y
     * en cada política: si el equipo aún no tiene bloqueo, se activa al
     * instante y el PIN remoto queda disponible.
     */
    fun ensureResetToken(context: Context): Boolean {
        val app = context.applicationContext
        val dpm = app.getSystemService(DevicePolicyManager::class.java) ?: return false
        if (!dpm.isDeviceOwnerApp(app.packageName)) return false
        val admin = TitanDeviceAdminReceiver.component(app)

        return runCatching {
            if (storedToken(app) != null && dpm.isResetPasswordTokenActive(admin)) return@runCatching true

            val token = ByteArray(32).also { SecureRandom().nextBytes(it) }
            if (!dpm.setResetPasswordToken(admin, token)) return@runCatching false
            prefs(app).edit { putString(KEY_TOKEN, Base64.encodeToString(token, Base64.NO_WRAP)) }
            dpm.isResetPasswordTokenActive(admin)
        }.onFailure { Log.w(TAG, "No fue posible registrar el token de PIN.", it) }
            .getOrDefault(false)
    }

    /** LOCK_DEVICE: aplica PIN y mensaje (si vienen) y bloquea la pantalla. */
    fun lock(context: Context, request: LockRequest): Outcome {
        val app = context.applicationContext
        val dpm = app.getSystemService(DevicePolicyManager::class.java)
            ?: return Outcome(false, "DEVICE_ADMIN_REQUIRED", "El equipo no expone DevicePolicyManager.")
        val admin = TitanDeviceAdminReceiver.component(app)

        if (!dpm.isAdminActive(admin)) {
            return Outcome(
                false, "DEVICE_ADMIN_REQUIRED",
                "Active el administrador de dispositivo de TitanMDM Agent para permitir el bloqueo."
            )
        }

        val owner = dpm.isDeviceOwnerApp(app.packageName)
        if (request.isLostMode && !owner) {
            return Outcome(
                false, "DEVICE_OWNER_REQUIRED",
                "El PIN temporal y el mensaje requieren que TitanMDM Agent sea Device Owner (inscripción por QR)."
            )
        }

        // Si el PIN falla, el equipo se bloquea igual y se informa el error.
        val pinError = request.pin?.let { applyPin(app, dpm, it) }
        val pinApplied = request.pin != null && pinError == null

        val text = request.lockScreenText()
        if (text != null) {
            runCatching { dpm.setDeviceOwnerLockScreenInfo(admin, text) }
                .onFailure { Log.w(TAG, "No fue posible fijar el mensaje de bloqueo.", it) }
        }

        if (request.isLostMode) {
            val previous = state(app)
            prefs(app).edit {
                putBoolean(KEY_ACTIVE, true)
                putString(KEY_MESSAGE, text)
                putBoolean(KEY_PIN_SET, pinApplied || previous.pinSet)
                putLong(KEY_SINCE, System.currentTimeMillis())
            }
        }

        dpm.lockNow()

        val shown = text != null && show(app, text)
        return pinError?.copy(messageShown = shown, locked = true)
            ?: Outcome(true, pinApplied = pinApplied, messageShown = shown, locked = true)
    }

    /** UNLOCK_DEVICE: retira el mensaje y el PIN temporal puesto por TitanMDM. */
    fun clear(context: Context, removePin: Boolean = true): Outcome {
        val app = context.applicationContext
        val dpm = app.getSystemService(DevicePolicyManager::class.java)
            ?: return Outcome(false, "DEVICE_ADMIN_REQUIRED", "El equipo no expone DevicePolicyManager.")
        if (!dpm.isDeviceOwnerApp(app.packageName)) {
            return Outcome(false, "DEVICE_OWNER_REQUIRED", "Quitar el bloqueo requiere Device Owner.")
        }
        val admin = TitanDeviceAdminReceiver.component(app)
        val current = state(app)

        runCatching { dpm.setDeviceOwnerLockScreenInfo(admin, null) }
            .onFailure { Log.w(TAG, "No fue posible retirar el mensaje de bloqueo.", it) }

        var pinRemoved = false
        if (removePin && current.pinSet) {
            val token = storedToken(app)
            pinRemoved = token != null && runCatching {
                dpm.isResetPasswordTokenActive(admin) && dpm.resetPasswordWithToken(admin, null, token, 0)
            }.getOrDefault(false)
            if (!pinRemoved) {
                return Outcome(
                    false, "PIN_REMOVE_FAILED",
                    "Se retiró el mensaje, pero el equipo no permitió quitar el PIN (¿política de contraseña activa?)."
                )
            }
        }

        prefs(app).edit {
            putBoolean(KEY_ACTIVE, false)
            remove(KEY_MESSAGE)
            putBoolean(KEY_PIN_SET, false)
            remove(KEY_SINCE)
        }
        app.getSystemService(NotificationManager::class.java)?.cancel(NOTIFICATION_ID)
        runCatching {
            app.startActivity(
                Intent(app, LockMessageActivity::class.java)
                    .addFlags(Intent.FLAG_ACTIVITY_NEW_TASK or Intent.FLAG_ACTIVITY_SINGLE_TOP)
                    .putExtra(LockMessageActivity.EXTRA_CLOSE, true)
            )
        }
        return Outcome(true, pinApplied = false, messageShown = false)
    }

    /** Tras reiniciar: el modo extravío sigue activo hasta que TI lo quite. */
    fun restore(context: Context) {
        val current = state(context)
        if (!current.active) return
        current.message?.let { show(context.applicationContext, it) }
    }

    private fun applyPin(app: Context, dpm: DevicePolicyManager, pin: String): Outcome? {
        val admin = TitanDeviceAdminReceiver.component(app)
        if (!ensureResetToken(app)) {
            return Outcome(
                false, "RESET_TOKEN_INACTIVE",
                "El equipo ya tenía un bloqueo de pantalla: desbloquéelo una vez para habilitar el PIN remoto."
            )
        }
        val token = storedToken(app)
            ?: return Outcome(false, "RESET_TOKEN_INACTIVE", "No hay token de PIN registrado en el equipo.")

        val applied = runCatching { dpm.resetPasswordWithToken(admin, pin, token, 0) }
            .onFailure { Log.w(TAG, "resetPasswordWithToken falló.", it) }
            .getOrDefault(false)

        return if (applied) {
            null
        } else {
            Outcome(
                false, "PIN_RESET_FAILED",
                "El equipo rechazó el PIN temporal (no cumple la política de contraseña)."
            )
        }
    }

    private fun show(app: Context, message: String): Boolean {
        showNotification(app, message)
        return runCatching {
            app.startActivity(
                Intent(app, LockMessageActivity::class.java)
                    .addFlags(
                        Intent.FLAG_ACTIVITY_NEW_TASK or
                            Intent.FLAG_ACTIVITY_CLEAR_TOP or
                            Intent.FLAG_ACTIVITY_SINGLE_TOP
                    )
                    .putExtra(LockMessageActivity.EXTRA_MESSAGE, message)
            )
            true
        }.getOrDefault(false)
    }

    private fun showNotification(context: Context, message: String) {
        val manager = context.getSystemService(NotificationManager::class.java) ?: return

        manager.createNotificationChannel(
            NotificationChannel(CHANNEL_ID, "Bloqueo remoto", NotificationManager.IMPORTANCE_HIGH).apply {
                description = "Mensaje de bloqueo enviado desde TitanMDM."
                setSound(null, null)
                enableVibration(false)
            }
        )

        val open = PendingIntent.getActivity(
            context,
            NOTIFICATION_ID,
            Intent(context, LockMessageActivity::class.java)
                .addFlags(Intent.FLAG_ACTIVITY_NEW_TASK)
                .putExtra(LockMessageActivity.EXTRA_MESSAGE, message),
            PendingIntent.FLAG_IMMUTABLE or PendingIntent.FLAG_UPDATE_CURRENT
        )

        val notification = NotificationCompat.Builder(context, CHANNEL_ID)
            .setSmallIcon(android.R.drawable.ic_lock_lock)
            .setContentTitle("Equipo bloqueado por TitanMDM")
            .setContentText(message)
            .setStyle(NotificationCompat.BigTextStyle().bigText(message))
            .setCategory(NotificationCompat.CATEGORY_ALARM)
            .setPriority(NotificationCompat.PRIORITY_MAX)
            .setVisibility(NotificationCompat.VISIBILITY_PUBLIC)
            .setOngoing(true)
            .setAutoCancel(false)
            .setContentIntent(open)
            .setFullScreenIntent(open, true)
            .build()

        val allowed = android.os.Build.VERSION.SDK_INT < android.os.Build.VERSION_CODES.TIRAMISU ||
            ContextCompat.checkSelfPermission(context, Manifest.permission.POST_NOTIFICATIONS) ==
            PackageManager.PERMISSION_GRANTED
        if (allowed) runCatching { manager.notify(NOTIFICATION_ID, notification) }
    }

    private fun storedToken(context: Context): ByteArray? =
        prefs(context).getString(KEY_TOKEN, null)
            ?.let { runCatching { Base64.decode(it, Base64.NO_WRAP) }.getOrNull() }

    private fun prefs(context: Context) =
        context.applicationContext.getSharedPreferences(PREFS, Context.MODE_PRIVATE)
}
