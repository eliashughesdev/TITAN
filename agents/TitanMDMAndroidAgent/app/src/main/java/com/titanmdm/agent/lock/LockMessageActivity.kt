package com.titanmdm.agent.lock

import android.app.KeyguardManager
import android.content.Intent
import android.graphics.Color
import android.os.Build
import android.os.Bundle
import android.view.Gravity
import android.view.WindowManager
import android.widget.Button
import android.widget.LinearLayout
import android.widget.TextView
import androidx.activity.ComponentActivity
import androidx.activity.addCallback

/**
 * Mensaje de extravío a pantalla completa, también sobre la pantalla de
 * bloqueo. "Desbloquear" abre el teclado del PIN del sistema: solo quien
 * conoce el PIN temporal pasa. El mensaje del sistema (lock screen info)
 * sigue visible hasta que TI envíe UNLOCK_DEVICE.
 */
class LockMessageActivity : ComponentActivity() {

    private lateinit var messageView: TextView

    override fun onCreate(savedInstanceState: Bundle?) {
        super.onCreate(savedInstanceState)

        if (intent?.getBooleanExtra(EXTRA_CLOSE, false) == true) {
            finish()
            return
        }

        window.addFlags(WindowManager.LayoutParams.FLAG_KEEP_SCREEN_ON)
        if (Build.VERSION.SDK_INT >= Build.VERSION_CODES.O_MR1) {
            setShowWhenLocked(true)
            setTurnScreenOn(true)
        } else {
            @Suppress("DEPRECATION")
            window.addFlags(
                WindowManager.LayoutParams.FLAG_SHOW_WHEN_LOCKED or
                    WindowManager.LayoutParams.FLAG_TURN_SCREEN_ON
            )
        }

        val title = TextView(this).apply {
            text = "EQUIPO BLOQUEADO"
            textSize = 30f
            setTextColor(Color.WHITE)
            gravity = Gravity.CENTER
        }

        messageView = TextView(this).apply {
            textSize = 20f
            setTextColor(Color.WHITE)
            gravity = Gravity.CENTER
            setPadding(0, 32, 0, 32)
        }

        val hint = TextView(this).apply {
            text = "Si encontró este equipo, comuníquese con el área de TI."
            textSize = 16f
            setTextColor(Color.parseColor("#93C5FD"))
            gravity = Gravity.CENTER
        }

        val unlock = Button(this).apply {
            text = "Desbloquear"
            setOnClickListener { requestUnlock() }
        }

        val root = LinearLayout(this).apply {
            orientation = LinearLayout.VERTICAL
            gravity = Gravity.CENTER
            setBackgroundColor(Color.BLACK)
            setPadding(48, 48, 48, 48)
            addView(title)
            addView(messageView)
            addView(hint)
            addView(unlock)
        }

        setContentView(root)
        // El retroceso no cierra el mensaje.
        onBackPressedDispatcher.addCallback(this) { }

        renderMessage(intent)
    }

    override fun onNewIntent(intent: Intent) {
        super.onNewIntent(intent)
        if (intent.getBooleanExtra(EXTRA_CLOSE, false)) {
            finish()
            return
        }
        setIntent(intent)
        renderMessage(intent)
    }

    /** Pide el PIN del sistema; con PIN correcto el operador vuelve al equipo. */
    private fun requestUnlock() {
        val keyguard = getSystemService(KeyguardManager::class.java)
        if (keyguard == null || !keyguard.isKeyguardLocked) {
            finish()
            return
        }
        keyguard.requestDismissKeyguard(
            this,
            object : KeyguardManager.KeyguardDismissCallback() {
                override fun onDismissSucceeded() {
                    finish()
                }
            }
        )
    }

    private fun renderMessage(intent: Intent?) {
        messageView.text = intent
            ?.getStringExtra(EXTRA_MESSAGE)
            ?.trim()
            ?.takeIf { it.isNotEmpty() }
            ?: LockScreenController.state(this).message
            ?: "Este equipo fue bloqueado por el área de TI."
    }

    companion object {
        const val EXTRA_MESSAGE = "titan_lock_message"
        const val EXTRA_CLOSE = "titan_lock_close"
    }
}
