package com.titanmdm.agent.receivers

import android.content.BroadcastReceiver
import android.content.Context
import android.content.Intent
import com.titanmdm.agent.workers.AgentWorkScheduler

class BootReceiver : BroadcastReceiver() {

    override fun onReceive(
        context: Context,
        intent: Intent
    ) {
        val action =
            intent.action ?: return

        when (action) {

            Intent.ACTION_BOOT_COMPLETED,
            Intent.ACTION_LOCKED_BOOT_COMPLETED,
            Intent.ACTION_MY_PACKAGE_REPLACED -> {

                AgentWorkScheduler
                    .scheduleAndSyncNow(
                        context.applicationContext
                    )
            }
        }
    }
}