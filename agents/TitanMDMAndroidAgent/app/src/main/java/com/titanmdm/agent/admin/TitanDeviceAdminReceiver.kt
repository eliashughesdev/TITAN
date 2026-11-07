package com.titanmdm.agent.admin

import android.app.admin.DeviceAdminReceiver
import android.app.admin.DevicePolicyManager
import android.content.ComponentName
import android.content.Context
import android.content.Intent
import android.os.Build
import android.os.PersistableBundle
import com.titanmdm.agent.core.config.AgentConfig
import com.titanmdm.agent.core.config.ServerEndpointStore
import com.titanmdm.agent.enrollment.auto.AutoEnrollment
import com.titanmdm.agent.enrollment.auto.ProvisioningConfig
import com.titanmdm.agent.kiosk.KioskController
import com.titanmdm.agent.service.AgentForegroundService

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
        applyProvisioningExtras(context, provisioningExtras(intent))
        KioskController.apply(context)
        AgentForegroundService.start(context)
    }

    companion object {
        fun component(context: Context): ComponentName =
            ComponentName(context.applicationContext, TitanDeviceAdminReceiver::class.java)

        /**
         * ADMIN_EXTRAS_BUNDLE del QR: guarda el servidor y, si trae
         * enrollment_token, deja programada la inscripción automática.
         */
        fun applyProvisioningExtras(context: Context, extras: PersistableBundle?) {
            if (extras == null) return

            val config = ProvisioningConfig.from(
                mapOf(
                    ProvisioningConfig.KEY_SERVER_URL to extras.getString(ProvisioningConfig.KEY_SERVER_URL),
                    ProvisioningConfig.KEY_ENROLLMENT_TOKEN to extras.getString(ProvisioningConfig.KEY_ENROLLMENT_TOKEN)
                )
            )

            if (config.canAutoEnroll) {
                AutoEnrollment.accept(context, config)
                return
            }

            // Solo servidor: el operador inscribe luego con el QR del agente.
            val normalized = config.serverUrl
                ?.let { AgentConfig.validateServerUrl(it).getOrNull() }
                ?: return
            ServerEndpointStore.init(context)
            ServerEndpointStore.save(normalized)
        }

        fun provisioningExtras(intent: Intent): PersistableBundle? =
            if (Build.VERSION.SDK_INT >= Build.VERSION_CODES.TIRAMISU) {
                intent.getParcelableExtra(
                    DevicePolicyManager.EXTRA_PROVISIONING_ADMIN_EXTRAS_BUNDLE,
                    PersistableBundle::class.java
                )
            } else {
                @Suppress("DEPRECATION")
                intent.getParcelableExtra(
                    DevicePolicyManager.EXTRA_PROVISIONING_ADMIN_EXTRAS_BUNDLE
                )
            }
    }
}
