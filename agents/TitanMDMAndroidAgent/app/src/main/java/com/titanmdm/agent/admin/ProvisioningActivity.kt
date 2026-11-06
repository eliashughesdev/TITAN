package com.titanmdm.agent.admin

import android.app.Activity
import android.app.admin.DevicePolicyManager
import android.content.Intent
import android.os.Build
import android.os.Bundle
import com.titanmdm.agent.kiosk.KioskController
import com.titanmdm.agent.service.AgentForegroundService

/**
 * Pasos del asistente de configuración en el aprovisionamiento por QR.
 *
 * Desde Android 10 el asistente llama a GET_PROVISIONING_MODE y, al final,
 * a ADMIN_POLICY_COMPLIANCE. Si el DPC no responde a ambas, Android aborta
 * con "No se puede configurar el dispositivo".
 */
class ProvisioningActivity : Activity() {

    override fun onCreate(savedInstanceState: Bundle?) {
        super.onCreate(savedInstanceState)

        when (intent?.action) {
            DevicePolicyManager.ACTION_GET_PROVISIONING_MODE -> returnProvisioningMode()
            DevicePolicyManager.ACTION_ADMIN_POLICY_COMPLIANCE -> completeCompliance()
            else -> setResult(RESULT_CANCELED)
        }
        finish()
    }

    private fun returnProvisioningMode() {
        val result = Intent()
        if (Build.VERSION.SDK_INT >= Build.VERSION_CODES.Q) {
            result.putExtra(
                DevicePolicyManager.EXTRA_PROVISIONING_MODE,
                DevicePolicyManager.PROVISIONING_MODE_FULLY_MANAGED_DEVICE
            )
        }
        // Reenvía los extras del QR para que lleguen a ADMIN_POLICY_COMPLIANCE.
        TitanDeviceAdminReceiver.provisioningExtras(intent)?.let {
            result.putExtra(DevicePolicyManager.EXTRA_PROVISIONING_ADMIN_EXTRAS_BUNDLE, it)
        }
        setResult(RESULT_OK, result)
    }

    private fun completeCompliance() {
        TitanDeviceAdminReceiver.applyProvisioningExtras(
            this,
            TitanDeviceAdminReceiver.provisioningExtras(intent)
        )
        KioskController.apply(this)
        AgentForegroundService.start(this)
        setResult(RESULT_OK)
    }
}
