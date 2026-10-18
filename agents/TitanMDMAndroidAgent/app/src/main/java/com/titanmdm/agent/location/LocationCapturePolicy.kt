package com.titanmdm.agent.location

/*
 * ANDROID-GEO-01
 *
 * Decision pura sobre que debe enviar el agente en
 * cada ciclo de captura proactiva de ubicacion.
 *
 * Reglas:
 * - Permiso denegado -> reportar solo estado
 *   PERMISSION_DENIED (sin coordenadas).
 * - Politica deshabilitada -> no capturar.
 * - Ahorro de bateria o bateria baja -> no capturar.
 * - Intervalo no cumplido -> no capturar.
 * - Sin fix -> reportar solo estado UNAVAILABLE.
 * - Con fix -> reportar coordenadas.
 *
 * No inventa posiciones. No envía coordenadas
 * jamas cuando el permiso falta o la politica lo
 * prohibe.
 */
sealed interface LocationCaptureDecision {

    /*
     * Enviar coordenadas reales al backend.
     */
    data class ReportCoordinates(
        val payload: LocationCapturePayload
    ) : LocationCaptureDecision

    /*
     * Enviar solo estado de captura, sin
     * coordenadas.
     */
    data class ReportStatusOnly(
        val status: String,
        val source: String
    ) : LocationCaptureDecision

    /*
     * No enviar nada en este ciclo.
     */
    data object Skip : LocationCaptureDecision
}

/*
 * Fix capturado por el dispositivo, previo a la
 * decision de reporte.
 */
data class LocationCapturePayload(
    val latitude: Double,
    val longitude: Double,
    val accuracyMeters: Double?,
    val altitudeMeters: Double?,
    val speedMetersPerSecond: Double?,
    val source: String,
    val capturedAtUtc: String
)

object LocationCapturePolicy {

    const val STATUS_PERMISSION_DENIED =
        "PERMISSION_DENIED"

    const val STATUS_UNAVAILABLE = "UNAVAILABLE"

    const val LOW_BATTERY_THRESHOLD_PERCENT = 15

    const val PROACTIVE_SOURCE =
        "AndroidAgent-Proactive"

    /*
     * WorkManager no permite periodos inferiores a
     * 15 minutos para trabajo periodico. El intervalo
     * configurado por el administrador puede ser
     * mayor, nunca menor.
     */
    const val MIN_REPORTING_INTERVAL_MINUTES = 15

    fun decide(
        reportingEnabled: Boolean,
        permissionGranted: Boolean,
        batteryPercent: Int?,
        batterySaverActive: Boolean,
        lastReportAtEpochMillis: Long?,
        nowEpochMillis: Long,
        capturedFix: LocationCapturePayload?,
        reportingIntervalMinutes: Int =
            MIN_REPORTING_INTERVAL_MINUTES
    ): LocationCaptureDecision {

        /*
         * Sin permiso se reporta SIEMPRE el estado,
         * aunque la politica este deshabilitada: es
         * un estado honesto del dispositivo y no
         * contiene coordenadas.
         */
        if (!permissionGranted) {
            return LocationCaptureDecision
                .ReportStatusOnly(
                    status =
                        STATUS_PERMISSION_DENIED,
                    source = PROACTIVE_SOURCE
                )
        }

        if (!reportingEnabled) {
            return LocationCaptureDecision.Skip
        }

        if (batterySaverActive) {
            return LocationCaptureDecision.Skip
        }

        if (batteryPercent != null &&
            batteryPercent <
                LOW_BATTERY_THRESHOLD_PERCENT
        ) {
            return LocationCaptureDecision.Skip
        }

        /*
         * El administrador puede pedir un intervalo
         * mayor al minimo de WorkManager; se respeta
         * el mayor de los dos.
         */
        val intervalMinutes =
            maxOf(
                reportingIntervalMinutes,
                MIN_REPORTING_INTERVAL_MINUTES
            )

        if (lastReportAtEpochMillis != null) {
            val elapsedMillis =
                nowEpochMillis -
                    lastReportAtEpochMillis

            val intervalMillis =
                intervalMinutes * 60_000L

            if (elapsedMillis < intervalMillis) {
                return LocationCaptureDecision.Skip
            }
        }

        val fix =
            capturedFix
                ?: return LocationCaptureDecision
                    .ReportStatusOnly(
                        status = STATUS_UNAVAILABLE,
                        source = PROACTIVE_SOURCE
                    )

        return LocationCaptureDecision
            .ReportCoordinates(
                payload = fix
            )
    }
}
