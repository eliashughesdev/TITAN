package com.titanmdm.agent.scanner

import android.content.Context
import kotlinx.serialization.json.JsonObject
import kotlinx.serialization.json.buildJsonObject
import kotlinx.serialization.json.put
import java.time.Instant

/**
 * Telemetria del lector reportada al MDM en el comando DEVICE_INFO.
 * Por privacidad no se guarda el contenido leido: solo conteo, ultima
 * lectura y simbologia.
 */
object ScannerTelemetry {

    private const val PREFS = "titanmdm_scanner_telemetry"
    private const val KEY_COUNT = "scan_count"
    private const val KEY_LAST_AT = "last_scan_at_utc"
    private const val KEY_LAST_SYMBOLOGY = "last_symbology"

    fun recordScan(context: Context, symbology: String?) {
        val prefs = context.applicationContext.getSharedPreferences(PREFS, Context.MODE_PRIVATE)
        prefs.edit()
            .putLong(KEY_COUNT, prefs.getLong(KEY_COUNT, 0) + 1)
            .putString(KEY_LAST_AT, Instant.now().toString())
            .putString(KEY_LAST_SYMBOLOGY, symbology)
            .apply()
    }

    fun snapshot(context: Context): JsonObject {
        val info = HoneywellScanner.info(context)
        val prefs = context.applicationContext.getSharedPreferences(PREFS, Context.MODE_PRIVATE)
        return buildJsonObject {
            put("vendor", if (info.vendorDevice) "Honeywell" else null)
            put("integratedScanner", info.available)
            put("dataCollectionService", HoneywellScanner.SERVICE_PACKAGE)
            put("dataCollectionServiceVersion", info.serviceVersion)
            put("agentScanCount", prefs.getLong(KEY_COUNT, 0))
            put("lastScanAtUtc", prefs.getString(KEY_LAST_AT, null))
            put("lastSymbology", prefs.getString(KEY_LAST_SYMBOLOGY, null))
        }
    }
}
