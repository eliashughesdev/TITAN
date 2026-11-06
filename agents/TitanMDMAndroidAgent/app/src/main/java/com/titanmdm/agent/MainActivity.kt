package com.titanmdm.agent

import android.app.ActivityManager
import android.app.admin.DevicePolicyManager
import android.os.Bundle
import androidx.activity.ComponentActivity
import androidx.activity.addCallback
import androidx.activity.compose.setContent
import androidx.activity.enableEdgeToEdge
import com.titanmdm.agent.kiosk.KioskController
import com.titanmdm.agent.service.AgentForegroundService
import com.titanmdm.agent.ui.TitanAgentScreen
import com.titanmdm.agent.ui.theme.TitanMDMAndroidAgentTheme

class MainActivity : ComponentActivity() {

    override fun onCreate(
        savedInstanceState: Bundle?
    ) {
        super.onCreate(savedInstanceState)

        onBackPressedDispatcher.addCallback(this) { }

        enableEdgeToEdge()

        AgentForegroundService.start(this)

        setContent {

            TitanMDMAndroidAgentTheme {

                TitanAgentScreen()
            }
        }
    }

    override fun onResume() {
        super.onResume()

        syncKioskLock()
    }

    @Deprecated("Kiosk mode blocks back navigation.")
    @Suppress("DEPRECATION")
    override fun onBackPressed() {
    }

    private fun syncKioskLock() {
        val devicePolicyManager =
            getSystemService(DevicePolicyManager::class.java)
                ?: return

        if (!devicePolicyManager.isDeviceOwnerApp(packageName)) return

        runCatching { KioskController.apply(this) }

        val activityManager =
            getSystemService(ActivityManager::class.java)
                ?: return

        if (activityManager.lockTaskModeState != ActivityManager.LOCK_TASK_MODE_NONE) return

        if (KioskController.shouldLockTask(this)) {
            runCatching { startLockTask() }
        }
    }
}