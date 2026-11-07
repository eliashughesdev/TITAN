package com.titanmdm.agent.scanner

import android.os.SystemClock
import android.util.Log
import android.view.KeyEvent
import java.util.concurrent.CopyOnWriteArraySet

/**
 * Lector Honeywell en modo teclado (Keystroke / "wedge"): el motor láser
 * "teclea" el contenido del código y termina con ENTER o TAB. Se distingue
 * de una persona por la velocidad (ráfaga de pulsaciones muy seguidas).
 *
 * MainActivity reenvía aquí cada KeyEvent; la pantalla de inscripción se
 * suscribe mientras está visible. Complementa al modo Intent
 * ([HoneywellScanner]), que es el preferido cuando el lector lo permite.
 */
object KeystrokeScanner {

    private const val TAG = "TitanKeystroke"
    private val listeners = CopyOnWriteArraySet<(String) -> Unit>()
    private val buffer = KeystrokeBurstBuffer()

    fun listen(onScan: (String) -> Unit): () -> Unit {
        listeners += onScan
        return { listeners -= onScan }
    }

    /** @return true si el evento cerró una lectura y no debe llegar a la UI. */
    fun onKeyEvent(event: KeyEvent): Boolean {
        if (listeners.isEmpty() || event.action != KeyEvent.ACTION_DOWN) return false

        // Hora de recepción: los eventos inyectados o de algunos lectores no
        // traen eventTime fiable.
        val now = SystemClock.uptimeMillis()

        return when (event.keyCode) {
            KeyEvent.KEYCODE_ENTER, KeyEvent.KEYCODE_NUMPAD_ENTER, KeyEvent.KEYCODE_TAB -> {
                val scanned = buffer.finish(now) ?: return false
                Log.i(TAG, "Lectura en modo teclado (${scanned.length} caracteres).")
                listeners.forEach { it(scanned) }
                true
            }

            else -> {
                val char = event.unicodeChar
                if (char > 0 && !Character.isISOControl(char)) {
                    buffer.add(char.toChar(), now)
                }
                false
            }
        }
    }
}

/**
 * Lógica pura (probada en unit tests): acumula caracteres y entrega el
 * texto solo si llegó como ráfaga de lector, no como tecleo humano.
 */
class KeystrokeBurstBuffer(
    private val maxGapMs: Long = 80,
    private val minLength: Int = 16,
    /** El ENTER/TAB final puede llegar algo después de la ráfaga. */
    private val maxEndGapMs: Long = 500
) {
    private val text = StringBuilder()
    private var lastAt = 0L

    fun add(char: Char, atMs: Long) {
        if (text.isNotEmpty() && atMs - lastAt > maxGapMs) text.clear()
        text.append(char)
        lastAt = atMs
    }

    /** Texto de la ráfaga, o null si fue demasiado corto o lento. */
    fun finish(atMs: Long): String? {
        val complete = text.length >= minLength && atMs - lastAt <= maxEndGapMs
        val result = if (complete) text.toString().trim() else null
        text.clear()
        return result?.takeIf { it.isNotEmpty() }
    }
}
