package com.titanmdm.agent.receivers

import android.content.BroadcastReceiver
import android.content.Context
import android.content.Intent
import com.titanmdm.agent.enrollment.auto.AutoEnrollment
import com.titanmdm.agent.enrollment.auto.LocalProvisionFile
import com.titanmdm.agent.kiosk.KioskController
import com.titanmdm.agent.lock.LockScreenController
import com.titanmdm.agent.location.GeofenceRegistrar
import com.titanmdm.agent.service.AgentForegroundService
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

                // Modo kiosco (solo efectivo con Device Owner).
                runCatching {
                    KioskController.apply(
                        context.applicationContext
                    )
                }

                // Modo extravío: el mensaje vuelve a mostrarse tras reiniciar.
                runCatching {
                    LockScreenController.restore(
                        context.applicationContext
                    )
                }

                // /sdcard/titan_provision.json o un QR que aún no llegó al servidor.
                LocalProvisionFile.consumeIfPresent(
                    context.applicationContext
                )
                AutoEnrollment.resumeIfPending(
                    context.applicationContext
                )

                // El SO olvida las geocercas al reiniciar.
                GeofenceRegistrar.invalidate(
                    context.applicationContext
                )

                AgentWorkScheduler
                    .scheduleAndSyncNow(
                        context.applicationContext
                    )

                // Sincronización continua (heartbeat cada 60 s).
                AgentForegroundService.start(
                    context.applicationContext
                )
            }
        }
    }
}