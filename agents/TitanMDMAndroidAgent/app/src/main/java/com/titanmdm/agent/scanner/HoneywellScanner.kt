package com.titanmdm.agent.scanner

import android.content.BroadcastReceiver
import android.content.Context
import android.content.Intent
import android.content.IntentFilter
import android.os.Build
import android.os.Bundle
import android.os.SystemClock
import androidx.core.content.ContextCompat
import java.util.concurrent.CopyOnWriteArraySet

/**
 * Lector integrado Honeywell (CK65/CT40/CT45/CT60/EDA51) por el Intent API
 * del Data Collection Service. No usa el SDK propietario.
 *
 * La pantalla de token reclama el perfil activo (Keyboard Wedge o lectura
 * directa) y escucha [ACTION_BARCODE_DATA]. [DEC_QR_ENABLED] deja leer el QR
 * de inscripción; [NTF_BAD_READ_ENABLED] en false evita el pitido de error
 * ("ring") al disparar el láser físico.
 *
 * En modo teclado la misma lectura también llega por [KeystrokeScanner].
 */
object HoneywellScanner {

    const val SERVICE_PACKAGE = "com.intermec.datacollectionservice"
    const val RECEIVER_CLASS = "com.titanmdm.agent.scanner.HoneywellBarcodeReceiver"

    /** BarcodeReader.PROPERTY_QR_CODE_ENABLED */
    const val DEC_QR_ENABLED = "DEC_QR_ENABLED"

    /** BarcodeReader.PROPERTY_NOTIFICATION_BAD_READ_ENABLED */
    const val NTF_BAD_READ_ENABLED = "NTF_BAD_READ_ENABLED"

    private const val ACTION_CLAIM = "com.honeywell.aidc.action.ACTION_CLAIM_SCANNER"
    private const val ACTION_RELEASE = "com.honeywell.aidc.action.ACTION_RELEASE_SCANNER"
    private const val EXTRA_SCANNER = "com.honeywell.aidc.extra.EXTRA_SCANNER"
    private const val EXTRA_PROPERTIES = "com.honeywell.aidc.extra.EXTRA_PROPERTIES"
    private const val INTERNAL_IMAGER = "dcs.scanner.imager"

    /** Acción propia, por si un perfil ya la tenía configurada. */
    const val ACTION_BARCODE = "com.titanmdm.agent.action.HONEYWELL_BARCODE"

    /**
     * Broadcast de fábrica (Scan to Intent / Keyboard Wedge con Data Intent).
     * Es la acción que el claim deja configurada en el perfil activo.
     */
    const val ACTION_BARCODE_DATA = "com.honeywell.decode.intent.ACTION_BARCODE_DATA"

    private const val CATEGORY_DEFAULT = "android.intent.category.DEFAULT"
    private const val DEDUPE_MS = 800L

    private val textKeys = listOf(
        "data",
        "com.honeywell.decode.intent.extra.DATA",
        "com.honeywell.aidc.extra.EXTRA_DATA",
        "barcode_data",
        "data_string"
    )

    private val byteKeys = listOf(
        "dataBytes",
        "com.honeywell.decode.intent.extra.DATA_BYTES"
    )

    private val listeners = CopyOnWriteArraySet<(String) -> Unit>()
    private var receiver: BroadcastReceiver? = null
    private var lastData: String? = null
    private var lastAt = 0L

    data class Info(
        val vendorDevice: Boolean,
        val serviceInstalled: Boolean,
        val serviceVersion: String?
    ) {
        val available get() = serviceInstalled
        /** CK65 puede ocultar el paquete y aun así atender el claim. */
        val shouldClaim get() = serviceInstalled || vendorDevice
    }

    fun info(context: Context): Info {
        val vendorDevice = Build.MANUFACTURER.contains("honeywell", ignoreCase = true) ||
            Build.MANUFACTURER.contains("intermec", ignoreCase = true)
        val service = runCatching {
            context.packageManager.getPackageInfo(SERVICE_PACKAGE, 0)
        }.getOrNull()
        return Info(vendorDevice, service != null, service?.versionName)
    }

    /**
     * Propiedades del claim. Booleanos (no enteros): el Data Collection
     * Service aplica [DEC_QR_ENABLED]/[NTF_BAD_READ_ENABLED] con putBoolean,
     * igual que BarcodeReader.setProperty(String, boolean) del SDK.
     * No se fuerza el perfil "DEFAULT": el láser físico usa el perfil activo
     * (wedge o intent). Reclamar otro deja el pitido de error encendido.
     */
    fun claimProperties(packageName: String): Map<String, Any> = linkedMapOf(
        "DPR_DATA_INTENT" to true,
        "DPR_DATA_INTENT_ACTION" to ACTION_BARCODE_DATA,
        "DPR_DATA_INTENT_CATEGORY" to CATEGORY_DEFAULT,
        "DPR_DATA_INTENT_PACKAGE_NAME" to packageName,
        "DPR_DATA_INTENT_CLASS_NAME" to RECEIVER_CLASS,
        DEC_QR_ENABLED to true,
        NTF_BAD_READ_ENABLED to false,
        "NTF_GOOD_READ_ENABLED" to true,
        "DPR_LAUNCH_BROWSER" to false,
        "TRIG_CONTROL_MODE" to "autoControl"
    )

    fun claim(context: Context) {
        val properties = Bundle()
        claimProperties(context.packageName).forEach { (key, value) ->
            when (value) {
                is Boolean -> properties.putBoolean(key, value)
                is String -> properties.putString(key, value)
                is Int -> properties.putInt(key, value)
            }
        }
        context.sendBroadcast(
            Intent(ACTION_CLAIM)
                .setPackage(SERVICE_PACKAGE)
                .putExtra(EXTRA_SCANNER, INTERNAL_IMAGER)
                .putExtra(EXTRA_PROPERTIES, properties)
        )
    }

    fun release(context: Context) {
        context.sendBroadcast(Intent(ACTION_RELEASE).setPackage(SERVICE_PACKAGE))
    }

    fun textFromExtras(values: Map<String, String?>): String? =
        textKeys.firstNotNullOfOrNull { key -> values[key]?.takeIf { it.isNotBlank() } }

    /** Texto de la lectura: "data" o, si falta, "dataBytes" en UTF-8. */
    fun barcodeText(intent: Intent): String? {
        textFromExtras(textKeys.associateWith { intent.getStringExtra(it) })?.let { return it }
        return byteKeys.firstNotNullOfOrNull { key ->
            intent.getByteArrayExtra(key)?.toString(Charsets.UTF_8)?.takeIf { it.isNotBlank() }
        }
    }

    fun symbology(intent: Intent): String? =
        intent.getStringExtra("codeId")
            ?: intent.getStringExtra("com.honeywell.decode.intent.extra.CODE_ID")

    /**
     * Entrega una lectura a la pantalla de token. El manifest receiver y el
     * receptor dinámico pasan por aquí; la misma ráfaga no se procesa dos veces.
     */
    fun dispatch(context: Context, intent: Intent) {
        val action = intent.action
        if (action != ACTION_BARCODE && action != ACTION_BARCODE_DATA) return
        val data = barcodeText(intent)?.trim()?.trim('\u0000') ?: return
        val now = SystemClock.uptimeMillis()
        if (data == lastData && now - lastAt < DEDUPE_MS) return
        lastData = data
        lastAt = now
        ScannerTelemetry.recordScan(context, symbology(intent))
        listeners.forEach { it(data) }
    }

    /**
     * Escucha [ACTION_BARCODE_DATA] mientras la pantalla de inscripción está
     * visible, haya o no visibilidad del paquete del servicio (Android 11+).
     * El reclamo es aparte: [claim] en ON_RESUME y [release] en ON_PAUSE.
     */
    fun register(context: Context, onBarcode: (String) -> Unit): () -> Unit {
        val app = context.applicationContext
        listeners += onBarcode
        ensureReceiver(app)
        return {
            listeners -= onBarcode
            if (listeners.isEmpty()) {
                receiver?.let { runCatching { app.unregisterReceiver(it) } }
                receiver = null
            }
        }
    }

    private fun ensureReceiver(app: Context) {
        if (receiver != null) return
        val created = object : BroadcastReceiver() {
            override fun onReceive(receiverContext: Context, intent: Intent) {
                dispatch(receiverContext, intent)
            }
        }
        ContextCompat.registerReceiver(
            app,
            created,
            IntentFilter().apply {
                addAction(ACTION_BARCODE)
                addAction(ACTION_BARCODE_DATA)
                addCategory(CATEGORY_DEFAULT)
            },
            ContextCompat.RECEIVER_EXPORTED
        )
        receiver = created
    }
}
