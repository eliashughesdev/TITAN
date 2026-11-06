package com.titanmdm.agent.receivers

import android.content.BroadcastReceiver
import android.content.Context
import android.content.Intent
import com.titanmdm.agent.core.storage.AgentIdentityStore
import com.titanmdm.agent.service.AgentForegroundService
import kotlinx.coroutines.CoroutineScope
import kotlinx.coroutines.Dispatchers
import kotlinx.coroutines.SupervisorJob
import kotlinx.coroutines.launch

/**
 * Watchdog de AlarmManager: relanza el foreground service si el
 * fabricante lo detuvo. No hace trabajo de red por si mismo.
 */
class HeartbeatWatchdogReceiver : BroadcastReceiver() {

    override fun onReceive(
        context: Context,
        intent: Intent
    ) {
        val appContext = context.applicationContext
        val pendingResult = goAsync()

        CoroutineScope(SupervisorJob() + Dispatchers.IO).launch {
            try {
                val identity =
                    runCatching { AgentIdentityStore(appContext).get() }
                        .getOrNull()

                if (identity != null &&
                    identity.deviceId.isNotBlank()
                ) {
                    AgentForegroundService.start(appContext)
                }
            } finally {
                pendingResult.finish()
            }
        }
    }
}
