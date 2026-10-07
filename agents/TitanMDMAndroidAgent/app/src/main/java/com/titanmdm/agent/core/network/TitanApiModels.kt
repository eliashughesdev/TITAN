package com.titanmdm.agent.core.network

import kotlinx.serialization.SerialName
import kotlinx.serialization.Serializable

/*
 * ============================================================
 * TITANMDM ANDROID AGENT
 * API CONTRACT MODELS
 * ============================================================
 *
 * Estos modelos reflejan los contratos reales del backend.
 *
 * SEGURIDAD:
 * - Nunca registrar deviceSecret.
 * - Nunca persistir enrollmentToken después del registro.
 * ============================================================
 */


/* ============================================================
 * DEVICE REGISTRATION
 * POST /api/enrollment/register
 * ============================================================ */

@Serializable
data class RegisterDeviceRequest(

    @SerialName("enrollmentToken")
    val enrollmentToken: String,

    @SerialName("deviceName")
    val deviceName: String,

    @SerialName("platform")
    val platform: String,

    @SerialName("serialNumber")
    val serialNumber: String,

    @SerialName("manufacturer")
    val manufacturer: String? = null,

    @SerialName("model")
    val model: String? = null,

    @SerialName("operatingSystem")
    val operatingSystem: String? = null,

    @SerialName("operatingSystemVersion")
    val operatingSystemVersion: String? = null,

    @SerialName("agentVersion")
    val agentVersion: String? = null,

    @SerialName("ipAddress")
    val ipAddress: String? = null,

    @SerialName("macAddress")
    val macAddress: String? = null
)


@Serializable
data class RegisterDeviceResponse(

    @SerialName("deviceId")
    val deviceId: String,

    @SerialName("organizationId")
    val organizationId: String,

    @SerialName("deviceName")
    val deviceName: String,

    @SerialName("platform")
    val platform: String,

    @SerialName("status")
    val status: String,

    @SerialName("complianceStatus")
    val complianceStatus: String,

    @SerialName("isManaged")
    val isManaged: Boolean,

    @SerialName("enrolledAtUtc")
    val enrolledAtUtc: String,

    @SerialName("deviceSecret")
    val deviceSecret: String
)


/* ============================================================
 * HEARTBEAT
 * POST /api/device/heartbeat
 * ============================================================ */

@Serializable
data class HeartbeatRequest(

    @SerialName("deviceId")
    val deviceId: String,

    @SerialName("deviceSecret")
    val deviceSecret: String,

    @SerialName("ipAddress")
    val ipAddress: String? = null,

    @SerialName("batteryLevel")
    val batteryLevel: Int? = null,

    @SerialName("agentVersion")
    val agentVersion: String? = null,

    @SerialName("operatingSystemVersion")
    val operatingSystemVersion: String? = null
)


@Serializable
data class HeartbeatResponse(

    @SerialName("deviceId")
    val deviceId: String,

    @SerialName("status")
    val status: String,

    @SerialName("complianceStatus")
    val complianceStatus: String,

    @SerialName("serverTimeUtc")
    val serverTimeUtc: String,

    @SerialName("lastSeenAtUtc")
    val lastSeenAtUtc: String? = null
)


/* ============================================================
 * DEVICE COMMANDS
 *
 * Backend:
 *
 * AgentCommandDto(
 *     Guid CommandId,
 *     string CommandType,
 *     string PayloadJson,
 *     DateTime CreatedAtUtc,
 *     DateTime ExpiresAtUtc
 * )
 * ============================================================ */

@Serializable
data class DeviceCommandDto(

    @SerialName("commandId")
    val commandId: String,

    @SerialName("commandType")
    val commandType: String,

    @SerialName("payloadJson")
    val payloadJson: String = "{}",

    @SerialName("createdAtUtc")
    val createdAtUtc: String,

    @SerialName("expiresAtUtc")
    val expiresAtUtc: String
)


/* ============================================================
 * COMMAND SUCCESS
 * ============================================================ */

@Serializable
data class CommandSuccessRequest(

    @SerialName("resultJson")
    val resultJson: String? = null
)


/* ============================================================
 * COMMAND FAILURE
 * ============================================================ */

@Serializable
data class CommandFailedRequest(

    @SerialName("errorCode")
    val errorCode: String,

    @SerialName("errorMessage")
    val errorMessage: String,

    @SerialName("resultJson")
    val resultJson: String? = null
)


/* ============================================================
 * GENERIC API RESPONSE
 * ============================================================ */

@Serializable
data class ApiMessageResponse(

    @SerialName("message")
    val message: String? = null
)