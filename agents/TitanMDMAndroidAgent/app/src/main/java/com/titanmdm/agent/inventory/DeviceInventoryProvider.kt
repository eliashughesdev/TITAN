package com.titanmdm.agent.inventory

import android.annotation.SuppressLint
import android.content.Context
import android.os.BatteryManager
import android.os.Build
import android.provider.Settings
import java.net.Inet4Address
import java.net.NetworkInterface
import java.util.Collections

data class LocalDeviceInventory(
    val deviceName: String,
    val serialNumber: String,
    val manufacturer: String,
    val model: String,
    val operatingSystem: String,
    val operatingSystemVersion: String,
    val apiLevel: Int,
    val buildNumber: String,
    val hardware: String,
    val bootloader: String,
    val batteryLevel: Int?,
    val ipAddress: String?
)

class DeviceInventoryProvider(
    private val context: Context
) {

    fun collect(): LocalDeviceInventory {

        return LocalDeviceInventory(
            deviceName = resolveDeviceName(),
            serialNumber = resolveStableIdentifier(),
            manufacturer = Build.MANUFACTURER,
            model = Build.MODEL,
            operatingSystem = "Android",
            operatingSystemVersion =
                Build.VERSION.RELEASE ?: "Unknown",
            apiLevel = Build.VERSION.SDK_INT,
            buildNumber =
                Build.DISPLAY ?: Build.ID,
            hardware = Build.HARDWARE,
            bootloader = Build.BOOTLOADER,
            batteryLevel = getBatteryLevel(),
            ipAddress = getIpAddress()
        )
    }

    private fun resolveDeviceName(): String {
        val manufacturer =
            Build.MANUFACTURER
                .trim()
                .replaceFirstChar {
                    if (it.isLowerCase()) {
                        it.titlecase()
                    } else {
                        it.toString()
                    }
                }

        return "$manufacturer ${Build.MODEL}".trim()
    }

    @SuppressLint("HardwareIds")
    private fun resolveStableIdentifier(): String {

        val androidId =
            Settings.Secure.getString(
                context.contentResolver,
                Settings.Secure.ANDROID_ID
            )

        return androidId
            ?.takeIf { it.isNotBlank() }
            ?: "android-${Build.FINGERPRINT.hashCode()}"
    }

    private fun getBatteryLevel(): Int? {
        val manager =
            context.getSystemService(
                Context.BATTERY_SERVICE
            ) as? BatteryManager
                ?: return null

        val value =
            manager.getIntProperty(
                BatteryManager.BATTERY_PROPERTY_CAPACITY
            )

        return value.takeIf {
            it in 0..100
        }
    }

    private fun getIpAddress(): String? {
        return try {

            Collections.list(
                NetworkInterface.getNetworkInterfaces()
            )
                .asSequence()
                .filter { it.isUp && !it.isLoopback }
                .flatMap {
                    Collections.list(
                        it.inetAddresses
                    ).asSequence()
                }
                .filterIsInstance<Inet4Address>()
                .firstOrNull {
                    !it.isLoopbackAddress
                }
                ?.hostAddress

        } catch (_: Exception) {
            null
        }
    }
}