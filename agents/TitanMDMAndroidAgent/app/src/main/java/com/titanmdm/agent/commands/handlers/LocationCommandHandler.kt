package com.titanmdm.agent.commands.handlers

import android.Manifest
import android.content.Context
import android.content.pm.PackageManager
import android.location.LocationManager
import androidx.core.content.ContextCompat
import com.titanmdm.agent.commands.CommandExecutionResult
import com.titanmdm.agent.core.network.DeviceCommandDto
import kotlinx.serialization.Serializable
import kotlinx.serialization.encodeToString
import kotlinx.serialization.json.Json
import java.time.Instant

class LocationCommandHandler(
    context: Context
) : CommandHandler {

    private val appContext =
        context.applicationContext

    override fun supports(
        commandType: String
    ): Boolean {
        return commandType.equals(
            "LOCATION_REQUEST",
            ignoreCase = true
        )
    }

    override suspend fun execute(
        command: DeviceCommandDto
    ): CommandExecutionResult {

        val fineGranted =
            ContextCompat.checkSelfPermission(
                appContext,
                Manifest.permission.ACCESS_FINE_LOCATION
            ) == PackageManager.PERMISSION_GRANTED

        val coarseGranted =
            ContextCompat.checkSelfPermission(
                appContext,
                Manifest.permission.ACCESS_COARSE_LOCATION
            ) == PackageManager.PERMISSION_GRANTED

        if (!fineGranted && !coarseGranted) {
            return CommandExecutionResult.Failure(
                errorCode =
                    "LOCATION_PERMISSION_REQUIRED",

                errorMessage =
                    "TitanMDM Agent no tiene permiso de ubicación."
            )
        }

        val manager =
            appContext.getSystemService(
                Context.LOCATION_SERVICE
            ) as LocationManager

        val providers =
            manager.getProviders(true)

        val locations =
            providers.mapNotNull {
                    provider ->
                try {
                    manager.getLastKnownLocation(
                        provider
                    )
                } catch (
                    _: SecurityException
                ) {
                    null
                }
            }

        val location =
            locations.maxByOrNull {
                it.time
            }

        if (location == null) {
            return CommandExecutionResult.Failure(
                errorCode =
                    "LOCATION_UNAVAILABLE",

                errorMessage =
                    "Android no dispone todavía de una ubicación conocida."
            )
        }

        val payload =
            LocationResult(
                latitude =
                    location.latitude,

                longitude =
                    location.longitude,

                accuracyMeters =
                    if (location.hasAccuracy())
                        location.accuracy.toDouble()
                    else null,

                altitudeMeters =
                    if (location.hasAltitude())
                        location.altitude
                    else null,

                speedMetersPerSecond =
                    if (location.hasSpeed())
                        location.speed.toDouble()
                    else null,

                source =
                    location.provider
                        ?: "AndroidAgent",

                capturedAtUtc =
                    Instant
                        .ofEpochMilli(
                            location.time
                        )
                        .toString()
            )

        return CommandExecutionResult.Success(
            resultJson =
                Json.encodeToString(
                    payload
                )
        )
    }

    @Serializable
    private data class LocationResult(
        val latitude: Double,
        val longitude: Double,
        val accuracyMeters: Double?,
        val altitudeMeters: Double?,
        val speedMetersPerSecond: Double?,
        val source: String,
        val capturedAtUtc: String
    )
}