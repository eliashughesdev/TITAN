package com.titanmdm.agent.scanner

import android.content.BroadcastReceiver
import android.content.Context
import android.content.Intent

/**
 * Receptor declarado: el Data Collection Service entrega
 * com.honeywell.decode.intent.ACTION_BARCODE_DATA de forma explícita
 * (paquete + clase del claim). Sin un componente registrado, el CK65
 * trata la lectura como fallida y emite el pitido de error.
 */
class HoneywellBarcodeReceiver : BroadcastReceiver() {
    override fun onReceive(context: Context, intent: Intent) {
        HoneywellScanner.dispatch(context, intent)
    }
}
