package com.titanmdm.agent.heartbeat

import android.content.Context
import com.titanmdm.agent.core.config.AgentConfig
import com.titanmdm.agent.core.network.HeartbeatRequest
import com.titanmdm.agent.core.network.TitanApiClient
import com.titanmdm.agent.core.storage.AgentIdentityStore
import com.titanmdm.agent.inventory.DeviceInventoryProvider
import com.titanmdm.agent.launcher.TitanLauncherAcknowledgementStore
import com.titanmdm.agent.location.HybridLocationProvider
import java.time.Instant

sealed interface HeartbeatResult {

    data object Success : HeartbeatResult

    data object NotEnrolled : HeartbeatResult

    data class Failure(
        val message: String,
        val retryable: Boolean
    ) : HeartbeatResult
}

class HeartbeatRepository(
    context: Context
) {

    private val appContext =
        context.applicationContext

    private val identityStore =
        AgentIdentityStore(appContext)

    private val inventoryProvider =
        DeviceInventoryProvider(appContext)

    suspend fun send(): HeartbeatResult {

        val identity =
            identityStore.get()
                ?: return HeartbeatResult.NotEnrolled

        return try {

            val inventory =
                inventoryProvider.collect()

            val acknowledgement =
                TitanLauncherAcknowledgementStore.read(appContext.filesDir)
                    ?.toHeartbeatFields()

            val hybridLocation =
                HybridLocationProvider.capture(appContext)
            val location =
                hybridLocation.location

            val request =
                HeartbeatRequest(
                    deviceId = identity.deviceId,
                    deviceSecret = identity.deviceSecret,
                    ipAddress = inventory.ipAddress,
                    batteryLevel = inventory.batteryLevel,
                    agentVersion = AgentConfig.AGENT_VERSION,
                    operatingSystemVersion =
                        inventory.operatingSystemVersion,
                    latitude = location?.latitude,
                    longitude = location?.longitude,
                    locationAccuracyMeters =
                        location?.let {
                            if (it.hasAccuracy()) it.accuracy.toDouble() else null
                        },
                    locationProvider =
                        location?.provider,
                    bssid =
                        hybridLocation.wifi.bssid,
                    rssi =
                        hybridLocation.wifi.rssi,
                    locationCapturedAtUtc =
                        location?.let {
                            Instant.ofEpochMilli(it.time).toString()
                        },
                    launcherProfileId = acknowledgement?.get("profileId"),
                    launcherPublicationId = acknowledgement?.get("publicationId"),
                    launcherConfigVersion = acknowledgement?.get("configVersion")?.toIntOrNull(),
                    launcherAppliedAtUtc = acknowledgement?.get("appliedAtUtc"),
                    launcherSyncStatus = acknowledgement?.get("syncStatus"),
                    launcherRestrictionsRead = acknowledgement != null
                )

            val response =
                TitanApiClient.service.heartbeat(
                    request = request
                )

            when {

                response.isSuccessful -> {
                    HeartbeatResult.Success
                }

                response.code() in 500..599 -> {
                    HeartbeatResult.Failure(
                        message =
                            "TitanMDM API HTTP ${response.code()}",
                        retryable = true
                    )
                }

                response.code() == 408 ||
                        response.code() == 429 -> {
                    HeartbeatResult.Failure(
                        message =
                            "Heartbeat temporalmente rechazado HTTP ${response.code()}",
                        retryable = true
                    )
                }

                response.code() == 401 ||
                        response.code() == 403 ||
                        response.code() == 404 -> {
                    HeartbeatResult.Failure(
                        message =
                            "Identidad del dispositivo rechazada HTTP ${response.code()}",
                        retryable = false
                    )
                }

                else -> {
                    HeartbeatResult.Failure(
                        message =
                            "Heartbeat rechazado HTTP ${response.code()}",
                        retryable = false
                    )
                }
            }

        } catch (exception: Exception) {

            HeartbeatResult.Failure(
                message =
                    exception.message
                        ?: "Error de comunicación con TitanMDM.",
                retryable = true
            )
        }
    }
}