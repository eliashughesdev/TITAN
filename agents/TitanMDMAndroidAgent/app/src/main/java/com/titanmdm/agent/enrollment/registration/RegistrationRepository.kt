package com.titanmdm.agent.enrollment.registration

import android.content.Context
import com.titanmdm.agent.core.config.AgentConfig
import com.titanmdm.agent.core.network.RegisterDeviceRequest
import com.titanmdm.agent.core.network.TitanApiClient
import com.titanmdm.agent.core.storage.AgentIdentity
import com.titanmdm.agent.core.storage.AgentIdentityStore
import com.titanmdm.agent.inventory.DeviceInventoryProvider

sealed interface RegistrationResult {

    data class Success(
        val identity: AgentIdentity
    ) : RegistrationResult

    data class Failure(
        val message: String
    ) : RegistrationResult
}

class RegistrationRepository(
    context: Context
) {

    private val identityStore =
        AgentIdentityStore(
            context.applicationContext
        )

    private val inventoryProvider =
        DeviceInventoryProvider(
            context.applicationContext
        )

    suspend fun register(
        enrollmentToken: String
    ): RegistrationResult {

        if (enrollmentToken.isBlank()) {
            return RegistrationResult.Failure(
                "El token de inscripción es obligatorio."
            )
        }

        return try {

            val inventory =
                inventoryProvider.collect()

            val response =
                TitanApiClient.service.registerDevice(
                    RegisterDeviceRequest(
                        enrollmentToken =
                            enrollmentToken.trim(),

                        deviceName =
                            inventory.deviceName,

                        platform =
                            AgentConfig.PLATFORM,

                        serialNumber =
                            inventory.serialNumber,

                        manufacturer =
                            inventory.manufacturer,

                        model =
                            inventory.model,

                        operatingSystem =
                            inventory.operatingSystem,

                        operatingSystemVersion =
                            inventory.operatingSystemVersion,

                        agentVersion =
                            AgentConfig.agentVersion,

                        ipAddress =
                            inventory.ipAddress
                    )
                )

            if (!response.isSuccessful) {
                return RegistrationResult.Failure(
                    "TitanMDM rechazó la inscripción. HTTP ${response.code()}."
                )
            }

            val body =
                response.body()
                    ?: return RegistrationResult.Failure(
                        "TitanMDM devolvió una respuesta vacía."
                    )

            val organizationId = body.organizationId
                ?.trim()
                ?.takeIf { it.isNotEmpty() }
                ?: return RegistrationResult.Failure(
                    "TitanMDM no devolvió organizationId durante el registro."
                )

            val registeredDeviceName = body.deviceName
                ?.trim()
                ?.takeIf { it.isNotEmpty() }
                ?: inventory.deviceName

            val identity = AgentIdentity(
                deviceId = body.deviceId,
                organizationId = organizationId,
                deviceName = registeredDeviceName,
                deviceSecret = body.deviceSecret
            )

            identityStore.save(identity)

            return RegistrationResult.Success(identity)

        } catch (exception: Exception) {

            RegistrationResult.Failure(
                exception.message
                    ?: "No fue posible conectar con TitanMDM."
            )
        }
    }
}