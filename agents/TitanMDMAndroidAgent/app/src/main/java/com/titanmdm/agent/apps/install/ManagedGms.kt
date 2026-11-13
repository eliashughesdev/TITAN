package com.titanmdm.agent.apps.install

import android.app.admin.DevicePolicyManager
import android.content.Context
import android.content.pm.PackageManager

/**
 * Contenedor GMS gestionado: Android Device Policy + Play Store.
 * Si el agente es Device Owner (QR interno / CK65), no hay ese contenedor
 * y las apps bajan por APK.
 */
object ManagedGms {
    const val CLOUD_DPC = "com.google.android.apps.work.clouddpc"
    const val PLAY_STORE = "com.android.vending"
    const val GMS = "com.google.android.gms"

    /**
     * CloudDPC viene preinstalado en muchos equipos con GMS: estar instalado
     * no significa que Android Management administre el equipo. Solo si es
     * el Device Owner, Play Store instala las apps y el agente no las baja.
     */
    fun hasManagedContainer(context: Context): Boolean {
        val pm = context.packageManager
        if (!installed(pm, PLAY_STORE) || !installed(pm, GMS)) return false
        val dpm = context.getSystemService(DevicePolicyManager::class.java) ?: return false
        return runCatching { dpm.isDeviceOwnerApp(CLOUD_DPC) }.getOrDefault(false) &&
            !dpm.isDeviceOwnerApp(context.packageName)
    }

    private fun installed(pm: PackageManager, packageName: String): Boolean =
        runCatching {
            if (android.os.Build.VERSION.SDK_INT >= android.os.Build.VERSION_CODES.TIRAMISU) {
                pm.getPackageInfo(packageName, PackageManager.PackageInfoFlags.of(0))
            } else {
                @Suppress("DEPRECATION")
                pm.getPackageInfo(packageName, 0)
            }
        }.isSuccess
}
