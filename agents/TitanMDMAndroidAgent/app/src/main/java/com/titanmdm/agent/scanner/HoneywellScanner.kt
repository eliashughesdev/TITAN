package com.titanmdm.agent.scanner

import android.content.BroadcastReceiver
import android.content.Context
import android.content.Intent
import android.content.IntentFilter
import android.os.Build
import android.os.Bundle
import androidx.core.content.ContextCompat

/**
 * Lector integrado de handhelds Honeywell (CT40/CT45/CT60/EDA51...) via
 * "Intent API" del Data Collection Service. No requiere SDK propietario.
 *
 * Mientras el agente reclama el lector, cada lectura llega como broadcast
 * [ACTION_BARCODE] con el texto en "data". Al soltarlo, el lector vuelve
 * a su perfil normal (teclado/wedge) para las demas apps.
 *
 * En modo teclado (configuracion de fabrica) no hace falta nada de esto:
 * la lectura se escribe en el campo del token y el agente la interpreta.
 */
object HoneywellScanner {

    const val SERVICE_PACKAGE = "com.intermec.datacollectionservice"

    private const val ACTION_CLAIM = "com.honeywell.aidc.action.ACTION_CLAIM_SCANNER"
    private const val ACTION_RELEASE = "com.honeywell.aidc.action.ACTION_RELEASE_SCANNER"
    private const val EXTRA_SCANNER = "com.honeywell.aidc.extra.EXTRA_SCANNER"
    private const val EXTRA_PROFILE = "com.honeywell.aidc.extra.EXTRA_PROFILE"
    private const val EXTRA_PROPERTIES = "com.honeywell.aidc.extra.EXTRA_PROPERTIES"
    private const val INTERNAL_IMAGER = "dcs.scanner.imager"

    /** Accion propia hacia la que el lector entrega las lecturas. */
    const val ACTION_BARCODE = "com.titanmdm.agent.action.HONEYWELL_BARCODE"

    data class Info(
        val vendorDevice: Boolean,
        val serviceInstalled: Boolean,
        val serviceVersion: String?
    ) {
        val available get() = serviceInstalled
    }

    fun info(context: Context): Info {
        val vendorDevice = Build.MANUFACTURER.contains("honeywell", ignoreCase = true) ||
            Build.MANUFACTURER.contains("intermec", ignoreCase = true)
        // Requiere <queries> en el manifest (visibilidad de paquetes, Android 11+).
        val service = runCatching {
            context.packageManager.getPackageInfo(SERVICE_PACKAGE, 0)
        }.getOrNull()
        return Info(vendorDevice, service != null, service?.versionName)
    }

    fun claim(context: Context) {
        val properties = Bundle().apply {
            putBoolean("DPR_DATA_INTENT", true)
            putString("DPR_DATA_INTENT_ACTION", ACTION_BARCODE)
        }
        context.sendBroadcast(
            Intent(ACTION_CLAIM)
                .setPackage(SERVICE_PACKAGE)
                .putExtra(EXTRA_SCANNER, INTERNAL_IMAGER)
                .putExtra(EXTRA_PROFILE, "DEFAULT")
                .putExtra(EXTRA_PROPERTIES, properties)
        )
    }

    fun release(context: Context) {
        context.sendBroadcast(Intent(ACTION_RELEASE).setPackage(SERVICE_PACKAGE))
    }

    /**
     * Reclama el lector y registra el receptor. Devuelve la funcion que
     * libera ambos (llamar en onPause / onDispose).
     */
    fun listen(context: Context, onBarcode: (String) -> Unit): () -> Unit {
        if (!info(context).available) return {}

        val receiver = object : BroadcastReceiver() {
            override fun onReceive(receiverContext: Context, intent: Intent) {
                if (intent.action != ACTION_BARCODE) return
                val data = intent.getStringExtra("data") ?: return
                ScannerTelemetry.recordScan(receiverContext, intent.getStringExtra("codeId"))
                onBarcode(data)
            }
        }

        // Exportado: el emisor es el servicio de Honeywell (otra app).
        // Las lecturas solo rellenan el formulario; inscribir exige confirmacion.
        ContextCompat.registerReceiver(
            context,
            receiver,
            IntentFilter(ACTION_BARCODE),
            ContextCompat.RECEIVER_EXPORTED
        )
        claim(context)

        return {
            runCatching { context.unregisterReceiver(receiver) }
            release(context)
        }
    }
}
