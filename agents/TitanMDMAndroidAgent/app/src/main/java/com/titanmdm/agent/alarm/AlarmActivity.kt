package com.titanmdm.agent.alarm

import android.app.Activity
import android.graphics.Color
import android.os.Build
import android.os.Bundle
import android.view.Gravity
import android.view.WindowManager
import android.widget.LinearLayout
import android.widget.TextView

class AlarmActivity : Activity() {

    override fun onCreate(savedInstanceState: Bundle?) {
        super.onCreate(savedInstanceState)

        window.addFlags(
            WindowManager.LayoutParams.FLAG_KEEP_SCREEN_ON or
                WindowManager.LayoutParams.FLAG_TURN_SCREEN_ON or
                WindowManager.LayoutParams.FLAG_SHOW_WHEN_LOCKED
        )

        if (Build.VERSION.SDK_INT >= Build.VERSION_CODES.O_MR1) {
            setShowWhenLocked(true)
            setTurnScreenOn(true)
        }

        val title = TextView(this).apply {
            text = "ALARMA REMOTA TITANMDM"
            textSize = 28f
            setTextColor(Color.WHITE)
            gravity = Gravity.CENTER
        }

        val hint = TextView(this).apply {
            text = "Toque la pantalla para silenciar"
            textSize = 18f
            setTextColor(Color.parseColor("#FCA5A5"))
            gravity = Gravity.CENTER
        }

        val root = LinearLayout(this).apply {
            orientation = LinearLayout.VERTICAL
            gravity = Gravity.CENTER
            setBackgroundColor(Color.BLACK)
            setPadding(48, 48, 48, 48)
            addView(title)
            addView(hint)
            setOnClickListener {
                DeviceAlarmController.stop(this@AlarmActivity)
                finish()
            }
        }

        setContentView(root)
    }

    override fun onDestroy() {
        DeviceAlarmController.stop(this)
        super.onDestroy()
    }
}
