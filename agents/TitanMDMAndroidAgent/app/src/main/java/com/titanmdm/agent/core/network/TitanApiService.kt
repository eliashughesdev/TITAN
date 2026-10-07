package com.titanmdm.agent.core.network

import retrofit2.Response
import retrofit2.http.Body
import retrofit2.http.GET
import retrofit2.http.Header
import retrofit2.http.POST
import retrofit2.http.Path

interface TitanApiService {

    /*
     * ========================================================
     * REGISTRATION
     * ========================================================
     */

    @POST("api/enrollment/register")
    suspend fun registerDevice(
        @Body request: RegisterDeviceRequest
    ): Response<RegisterDeviceResponse>


    /*
     * ========================================================
     * HEARTBEAT
     *
     * El backend autentica actualmente mediante DeviceId +
     * DeviceSecret incluidos en DeviceHeartbeatRequest.
     * ========================================================
     */

    @POST("api/device/heartbeat")
    suspend fun heartbeat(
        @Body request: HeartbeatRequest
    ): Response<HeartbeatResponse>


    /*
     * ========================================================
     * COMMAND ENGINE
     *
     * El backend DeviceCommandAgentController autentica usando:
     *
     * X-Titan-Device-Id
     * X-Titan-Device-Secret
     * ========================================================
     */

    @GET("api/device/commands")
    suspend fun getPendingCommands(
        @Header("X-Titan-Device-Id")
        deviceId: String,

        @Header("X-Titan-Device-Secret")
        deviceSecret: String
    ): Response<List<DeviceCommandDto>>


    @POST("api/device/commands/{commandId}/delivered")
    suspend fun markCommandDelivered(
        @Path("commandId")
        commandId: String,

        @Header("X-Titan-Device-Id")
        deviceId: String,

        @Header("X-Titan-Device-Secret")
        deviceSecret: String
    ): Response<Unit>


    @POST("api/device/commands/{commandId}/executing")
    suspend fun markCommandExecuting(
        @Path("commandId")
        commandId: String,

        @Header("X-Titan-Device-Id")
        deviceId: String,

        @Header("X-Titan-Device-Secret")
        deviceSecret: String
    ): Response<Unit>


    @POST("api/device/commands/{commandId}/success")
    suspend fun markCommandSuccess(
        @Path("commandId")
        commandId: String,

        @Header("X-Titan-Device-Id")
        deviceId: String,

        @Header("X-Titan-Device-Secret")
        deviceSecret: String,

        @Body
        request: CommandSuccessRequest
    ): Response<Unit>


    @POST("api/device/commands/{commandId}/failed")
    suspend fun markCommandFailed(
        @Path("commandId")
        commandId: String,

        @Header("X-Titan-Device-Id")
        deviceId: String,

        @Header("X-Titan-Device-Secret")
        deviceSecret: String,

        @Body
        request: CommandFailedRequest
    ): Response<Unit>
}