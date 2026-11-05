package com.titanmdm.agent.admin

import android.app.admin.DeviceAdminReceiver
import android.content.ComponentName
import android.content.Context
import android.content.Intent
import com.titanmdm.agent.kiosk.KioskController

/**
 * Administrador de dispositivo de TitanMDM Agent.
 *
 * - Activado por el usuario (pantalla del agente): permite LOCK_DEVICE.
 * - Como Device Owner (sin Android Enterprise ni Google):
 *     adb shell dpm set-device-owner com.titanmdm.agent/.admin.TitanDeviceAdminReceiver
 *   habilita el modo kiosco (HOME fijo, barra de estado bloqueada,
 *   permisos de ubicación concedidos por política).
 */
class TitanDeviceAdminReceiver : DeviceAdminReceiver() {

    override fun onEnabled(context: Context, intent: Intent) {
        KioskController.apply(context)
    }

    override fun onProfileProvisioningComplete(context: Context, intent: Intent) {
        KioskController.apply(context)
    }

    companion object {
        fun component(context: Context): ComponentName =
            ComponentName(context.applicationContext, TitanDeviceAdminReceiver::class.java)
    }
}
