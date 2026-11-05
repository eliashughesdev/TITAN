package com.titanmdm.agent.enrollment.registration

import android.content.Context
import com.titanmdm.agent.core.config.AgentConfig
import com.titanmdm.agent.core.config.ServerEndpointStore
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

    /**
     * @param serverUrl servidor recibido por QR; null = el ya configurado.
     * Si la inscripcion falla se restaura el servidor anterior.
     */
    suspend fun register(
        enrollmentToken: String,
        serverUrl: String? = null
    ): RegistrationResult {

        if (enrollmentToken.isBlank()) {
            return RegistrationResult.Failure(
                "El token de inscripción es obligatorio."
            )
        }

        val previousServer = ServerEndpointStore.current()
        if (serverUrl != null) {
            val normalized = AgentConfig.validateServerUrl(serverUrl)
                .getOrElse {
                    return RegistrationResult.Failure(
                        it.message ?: "La URL del servidor TitanMDM no es válida."
                    )
                }
            ServerEndpointStore.save(normalized)
        }

        val result = registerWithCurrentServer(enrollmentToken)
        if (result is RegistrationResult.Failure && serverUrl != null) {
            ServerEndpointStore.save(previousServer)
        }
        return result
    }

    private suspend fun registerWithCurrentServer(
        enrollmentToken: String
    ): RegistrationResult {

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
                    when (response.code()) {
                        400 -> "Solicitud de inscripción inválida (HTTP 400)."
                        401 -> "Token inválido, vencido o revocado. Genere un QR nuevo en la consola."
                        409 -> "Token agotado o el número de serie ya está inscrito (HTTP 409)."
                        else -> "TitanMDM rechazó la inscripción. HTTP ${response.code()}."
                    }
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