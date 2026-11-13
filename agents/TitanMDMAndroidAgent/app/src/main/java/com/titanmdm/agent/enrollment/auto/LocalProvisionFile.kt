package com.titanmdm.agent.enrollment.auto

import android.app.admin.DevicePolicyManager
import android.content.Context
import android.os.Build
import android.os.Environment
import android.util.Log
import com.titanmdm.agent.admin.TitanDeviceAdminReceiver
import kotlinx.serialization.json.Json
import kotlinx.serialization.json.JsonObject
import kotlinx.serialization.json.contentOrNull
import kotlinx.serialization.json.jsonPrimitive
import java.io.File

/**
 * Aprovisionamiento masivo sin QR ni cámara. Al arrancar, si no hay una
 * inscripción pendiente, se lee /sdcard/titan_provision.json (USB o staging):
 *
 *   { "server_url": "http://172.21.20.14:8020", "enrollment_token": "<token>" }
 *
 * También acepta "server"/"token" y camelCase. Si el alta se acepta, el
 * archivo se borra para no dejar el token en almacenamiento compartido.
 */
object LocalProvisionFile {

    const val FILE_NAME = "titan_provision.json"
    const val ABSOLUTE_PATH = "/sdcard/$FILE_NAME"

    private const val TAG = "TitanLocalProvision"
    private const val MAX_BYTES = 8_192L
    private val json = Json { ignoreUnknownKeys = true }

    fun parse(text: String?): ProvisioningConfig? {
        val raw = text?.trim()?.removePrefix("\uFEFF") ?: return null
        if (raw.isEmpty() || !raw.startsWith("{")) return null
        val obj = runCatching { json.parseToJsonElement(raw) as? JsonObject }.getOrNull() ?: return null
        fun pick(vararg keys: String): String? =
            keys.firstNotNullOfOrNull { key ->
                runCatching { obj[key]?.jsonPrimitive?.contentOrNull }
                    .getOrNull()
                    ?.trim()
                    ?.takeIf { it.isNotEmpty() }
            }
        return ProvisioningConfig.from(
            mapOf(
                ProvisioningConfig.KEY_SERVER_URL to pick("server_url", "server", "serverUrl"),
                ProvisioningConfig.KEY_ENROLLMENT_TOKEN to pick("enrollment_token", "token", "enrollmentToken")
            )
        ).takeIf { it.canAutoEnroll }
    }

    fun candidateFiles(externalRoot: File?): List<File> {
        val roots = listOfNotNull(
            File("/sdcard"),
            File("/storage/emulated/0"),
            File("/storage/self/primary"),
            File("/mnt/sdcard"),
            externalRoot
        ).distinctBy { it.path }
        return roots.map { File(it, FILE_NAME) }.distinctBy { it.path }
    }

    /** @return true si el archivo tenía un alta válida y quedó encolada. */
    fun consumeIfPresent(context: Context): Boolean {
        val app = context.applicationContext
        if (PendingEnrollmentStore(app).get() != null) return false
        grantStorageIfOwner(app)
        val file = findReadable(app) ?: return false
        val text = runCatching {
            if (file.length() > MAX_BYTES) return false
            file.readText(Charsets.UTF_8)
        }.getOrElse {
            Log.w(TAG, "No se pudo leer $ABSOLUTE_PATH")
            return false
        }
        val config = parse(text)
        if (config == null) {
            Log.w(TAG, "$FILE_NAME no tiene servidor y token válidos.")
            return false
        }
        if (!AutoEnrollment.accept(app, config)) return false
        if (!file.delete()) runCatching { file.writeText("") }
        Log.i(TAG, "Inscripción tomada de $ABSOLUTE_PATH")
        return true
    }

    private fun findReadable(context: Context): File? {
        val root = runCatching { Environment.getExternalStorageDirectory() }.getOrNull()
        return candidateFiles(root).firstOrNull { file ->
            runCatching { file.isFile && file.canRead() }.getOrDefault(false)
        }
    }

    private fun grantStorageIfOwner(context: Context) {
        val dpm = context.getSystemService(DevicePolicyManager::class.java) ?: return
        if (!dpm.isDeviceOwnerApp(context.packageName)) return
        val admin = TitanDeviceAdminReceiver.component(context)
        val permissions = buildList {
            add(android.Manifest.permission.READ_EXTERNAL_STORAGE)
            if (Build.VERSION.SDK_INT >= Build.VERSION_CODES.R) {
                add(android.Manifest.permission.MANAGE_EXTERNAL_STORAGE)
            }
        }
        permissions.forEach { permission ->
            runCatching {
                dpm.setPermissionGrantState(
                    admin,
                    context.packageName,
                    permission,
                    DevicePolicyManager.PERMISSION_GRANT_STATE_GRANTED
                )
            }
        }
    }
}
