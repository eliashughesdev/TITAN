package com.titanmdm.agent.scanner

import android.content.BroadcastReceiver
import android.content.Context
import android.content.Intent
import android.content.IntentFilter
import android.os.Build
import android.os.Bundle
import androidx.core.content.ContextCompat

/**
 * Lector integrado de handhelds Honeywell (CK65/CT40/CT45/CT60/EDA51...) via
 * "Intent API" del Data Collection Service. No requiere SDK propietario.
 *
 * Mientras el agente reclama el lector, cada lectura del gatillo llega como
 * broadcast [ACTION_BARCODE] con el texto en "data". Al soltarlo, el lector
 * vuelve a su perfil normal (teclado/wedge) para las demas apps.
 *
 * El reclamo solo es valido con la actividad en primer plano: hay que
 * reclamar en onResume y soltar en onPause (ver [AgentScreen]).
 *
 * En modo teclado (configuracion de fabrica) la lectura llega por
 * [KeystrokeScanner] o directamente al campo del token.
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

    /**
     * Accion generica que algunos perfiles de Honeywell (Scan to Intent
     * configurado por TI o EZConfig) usan por defecto.
     */
    const val ACTION_BARCODE_GENERIC = "com.honeywell.decode.intent.ACTION_BARCODE_DATA"

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
            // Entrega por Intent (no teclado): evita la doble entrada.
            putBoolean("DPR_DATA_INTENT", true)
            putString("DPR_DATA_INTENT_ACTION", ACTION_BARCODE)
            // Misma categoria que el IntentFilter del receptor.
            putString("DPR_DATA_INTENT_CATEGORY", Intent.CATEGORY_DEFAULT)
            // El QR de inscripcion debe leerse aunque el perfil lo tenga apagado
            // (BarcodeReader.PROPERTY_QR_CODE_ENABLED del SDK de Honeywell).
            putBoolean("DEC_QR_ENABLED", true)
            // Sin pitido de lectura fallida (PROPERTY_NOTIFICATION_BAD_READ_ENABLED):
            // el operador solo oye el tono de exito al capturar el QR.
            putBoolean("NTF_BAD_READ_ENABLED", false)
            // El gatillo fisico dispara la lectura sin que la app lo controle.
            putString("TRIG_CONTROL_MODE", "autoControl")
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

    /** Texto de la lectura: "data" o, si falta, "dataBytes" en UTF-8. */
    fun barcodeText(intent: Intent): String? =
        intent.getStringExtra("data")?.takeIf { it.isNotBlank() }
            ?: intent.getByteArrayExtra("dataBytes")
                ?.toString(Charsets.UTF_8)
                ?.takeIf { it.isNotBlank() }

    /**
     * Registra el receptor de lecturas (sin reclamar el lector). Devuelve la
     * funcion que lo quita. El reclamo se hace aparte con [claim]/[release]
     * siguiendo el ciclo de vida de la pantalla.
     */
    fun register(context: Context, onBarcode: (String) -> Unit): () -> Unit {
        if (!info(context).available) return {}

        val receiver = object : BroadcastReceiver() {
            override fun onReceive(receiverContext: Context, intent: Intent) {
                if (intent.action != ACTION_BARCODE && intent.action != ACTION_BARCODE_GENERIC) return
                val data = barcodeText(intent) ?: return
                ScannerTelemetry.recordScan(receiverContext, intent.getStringExtra("codeId"))
                onBarcode(data)
            }
        }

        // Exportado: el emisor es el servicio de Honeywell (otra app). Solo
        // escucha mientras la pantalla de inscripcion (equipo sin inscribir)
        // esta visible, y el QR solo se acepta con el formato de TitanMDM.
        ContextCompat.registerReceiver(
            context,
            receiver,
            IntentFilter().apply {
                addAction(ACTION_BARCODE)
                addAction(ACTION_BARCODE_GENERIC)
                // El Data Collection Service exige una categoria no vacia;
                // sin ella el broadcast no llega al receptor en algunos equipos.
                addCategory(Intent.CATEGORY_DEFAULT)
            },
            ContextCompat.RECEIVER_EXPORTED
        )

        return { runCatching { context.unregisterReceiver(receiver) } }
    }
}
