package com.titanmdm.agent.launcher

import android.app.admin.DevicePolicyManager
import android.content.BroadcastReceiver
import android.content.ComponentName
import android.content.Context
import android.content.Intent
import android.content.IntentFilter
import android.content.pm.PackageManager
import android.graphics.drawable.BitmapDrawable
import android.os.Bundle
import androidx.activity.ComponentActivity
import androidx.activity.compose.setContent
import androidx.compose.runtime.getValue
import androidx.compose.runtime.mutableStateOf
import androidx.compose.runtime.setValue
import androidx.compose.ui.graphics.ImageBitmap
import androidx.compose.ui.graphics.asImageBitmap
import androidx.core.content.ContextCompat
import androidx.core.graphics.drawable.toBitmap
import com.titanmdm.agent.ui.theme.TitanMDMAndroidAgentTheme

/**
 * Punto de entrada HOME del Titan Launcher.
 *
 * Relee la política administrada sin reiniciar el dispositivo mediante:
 *  - ACTION_APPLICATION_RESTRICTIONS_CHANGED (cambios de managed config);
 *  - PACKAGE_ADDED / PACKAGE_REMOVED / PACKAGE_CHANGED / PACKAGE_REPLACED.
 *
 * Nunca abre aplicaciones fuera de la lista autorizada ni concede permisos.
 */
class TitanLauncherActivity : ComponentActivity() {

    private var policy by mutableStateOf(TitanLauncherPolicy())
    private var adminStatus by mutableStateOf(TitanLauncherAdminStatus.None)
    private var installedPackages by mutableStateOf<Set<String>>(emptySet())

    private val restrictionsReceiver =
        object : BroadcastReceiver() {
            override fun onReceive(
                context: Context?,
                intent: Intent?
            ) {
                if (intent?.action ==
                    Intent.ACTION_APPLICATION_RESTRICTIONS_CHANGED
                ) {
                    refresh()
                }
            }
        }

    private val packageReceiver =
        object : BroadcastReceiver() {
            override fun onReceive(
                context: Context?,
                intent: Intent?
            ) {
                when (intent?.action) {
                    Intent.ACTION_PACKAGE_ADDED,
                    Intent.ACTION_PACKAGE_REMOVED,
                    Intent.ACTION_PACKAGE_CHANGED,
                    Intent.ACTION_PACKAGE_REPLACED -> refresh()
                }
            }
        }

    override fun onCreate(savedInstanceState: Bundle?) {
        super.onCreate(savedInstanceState)

        refresh()

        setContent {
            TitanMDMAndroidAgentTheme {
                TitanLauncherScreen(
                    policy = policy,
                    adminStatus = adminStatus,
                    isInstalled = { it in installedPackages },
                    iconProvider = ::loadIcon,
                    onLaunch = ::launchApp
                )
            }
        }
    }

    override fun onStart() {
        super.onStart()

        ContextCompat.registerReceiver(
            this,
            restrictionsReceiver,
            IntentFilter(
                Intent.ACTION_APPLICATION_RESTRICTIONS_CHANGED
            ),
            ContextCompat.RECEIVER_NOT_EXPORTED
        )

        val packageFilter =
            IntentFilter().apply {
                addAction(Intent.ACTION_PACKAGE_ADDED)
                addAction(Intent.ACTION_PACKAGE_REMOVED)
                addAction(Intent.ACTION_PACKAGE_CHANGED)
                addAction(Intent.ACTION_PACKAGE_REPLACED)
                addDataScheme("package")
            }

        ContextCompat.registerReceiver(
            this,
            packageReceiver,
            packageFilter,
            ContextCompat.RECEIVER_NOT_EXPORTED
        )

        refresh()
    }

    override fun onStop() {
        runCatching { unregisterReceiver(restrictionsReceiver) }
        runCatching { unregisterReceiver(packageReceiver) }
        super.onStop()
    }

    private fun refresh() {
        val newPolicy = TitanLauncherPolicyReader.read(this)

        policy = newPolicy

        installedPackages = newPolicy.applications
            .asSequence()
            .map { it.packageName }
            .filter { isInstalled(it) }
            .toSet()

        adminStatus = readAdminStatus()
    }

    private fun isInstalled(packageName: String): Boolean {
        return try {
            packageManager.getApplicationInfo(packageName, 0)
            true
        } catch (_: PackageManager.NameNotFoundException) {
            false
        }
    }

    private fun loadIcon(packageName: String): ImageBitmap? {
        return try {
            val info = packageManager.getApplicationInfo(packageName, 0)
            val drawable = info.loadIcon(packageManager)

            val bitmap = if (drawable is BitmapDrawable && drawable.bitmap != null) {
                drawable.bitmap
            } else {
                drawable.toBitmap(width = 108, height = 108)
            }

            bitmap.asImageBitmap()
        } catch (_: Exception) {
            null
        }
    }

    private fun launchApp(packageName: String) {
        // Doble verificación: nunca abrir una app fuera de la lista autorizada.
        if (policy.applications.none { it.packageName == packageName }) {
            return
        }

        val intent =
            packageManager.getLaunchIntentForPackage(packageName) ?: return

        try {
            startActivity(intent)
        } catch (_: Exception) {
            // El estado se recalcula al regresar al launcher.
        }
    }

    private fun readAdminStatus(): TitanLauncherAdminStatus {
        val manager =
            getSystemService(Context.DEVICE_POLICY_SERVICE)
                as? DevicePolicyManager
                ?: return TitanLauncherAdminStatus.None

        val isDeviceOwner =
            runCatching { manager.isDeviceOwnerApp(packageName) }
                .getOrDefault(false)

        val isProfileOwner =
            runCatching { manager.isProfileOwnerApp(packageName) }
                .getOrDefault(false)

        val isDedicated =
            isDeviceOwner &&
                runCatching {
                    manager.isLockTaskPermitted(packageName)
                }.getOrDefault(false)

        return TitanLauncherAdminStatus(
            isDeviceOwner = isDeviceOwner,
            isProfileOwner = isProfileOwner,
            isAdminActive = false,
            isFullyManaged = isDeviceOwner || isProfileOwner,
            isDedicated = isDedicated
        )
    }
}
