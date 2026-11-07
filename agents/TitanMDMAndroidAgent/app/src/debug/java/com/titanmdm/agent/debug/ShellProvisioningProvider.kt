package com.titanmdm.agent.debug

import android.content.ContentProvider
import android.content.ContentValues
import android.database.Cursor
import android.net.Uri
import android.os.Binder
import android.os.Bundle
import android.os.PersistableBundle

import com.titanmdm.agent.admin.TitanDeviceAdminReceiver
import com.titanmdm.agent.enrollment.auto.PendingEnrollmentStore

/**
 * Solo build debug y solo desde adb shell (UID 2000) o root: reproduce el
 * paso final del aprovisionamiento por QR con los mismos extras, para
 * probar la inscripción automática en emulador o por cable.
 *
 *   adb shell content call --uri content://com.titanmdm.agent.shellprovisioning \
 *     --method provision --arg http://10.0.2.2:8020 --extra enrollment_token:s:<token>
 *
 * (El servidor va en --arg: "content" no admite ':' en los valores de --extra.)
 *
 *   adb shell content call --uri content://com.titanmdm.agent.shellprovisioning --method status
 */
class ShellProvisioningProvider : ContentProvider() {

    override fun onCreate(): Boolean = true

    override fun call(method: String, arg: String?, extras: Bundle?): Bundle {
        val uid = Binder.getCallingUid()
        if (uid != SHELL_UID && uid != ROOT_UID) {
            throw SecurityException("Solo adb shell puede usar este proveedor.")
        }

        val context = requireNotNull(context).applicationContext
        return when (method) {
            "provision" -> {
                val bundle = PersistableBundle().apply {
                    putString("server_url", arg ?: extras?.getString("server_url"))
                    putString("enrollment_token", extras?.getString("enrollment_token"))
                }
                TitanDeviceAdminReceiver.applyProvisioningExtras(context, bundle)
                Bundle().apply { putBoolean("accepted", PendingEnrollmentStore(context).get() != null) }
            }

            "status" -> {
                val pending = PendingEnrollmentStore(context).get()
                Bundle().apply {
                    putBoolean("pending", pending != null)
                    putInt("attempts", pending?.attempts ?: 0)
                    putString("lastError", pending?.lastError)
                    putBoolean("failedPermanently", pending?.failedPermanently ?: false)
                }
            }

            else -> throw IllegalArgumentException("Método desconocido: $method")
        }
    }

    override fun query(uri: Uri, projection: Array<out String>?, selection: String?, selectionArgs: Array<out String>?, sortOrder: String?): Cursor? = null
    override fun getType(uri: Uri): String? = null
    override fun insert(uri: Uri, values: ContentValues?): Uri? = null
    override fun delete(uri: Uri, selection: String?, selectionArgs: Array<out String>?): Int = 0
    override fun update(uri: Uri, values: ContentValues?, selection: String?, selectionArgs: Array<out String>?): Int = 0
}

// UID fijos de Android para adb shell y root (constantes ocultas del SDK).
private const val SHELL_UID = 2000
private const val ROOT_UID = 0
