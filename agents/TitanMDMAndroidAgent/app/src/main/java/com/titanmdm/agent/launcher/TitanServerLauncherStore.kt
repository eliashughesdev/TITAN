package com.titanmdm.agent.launcher

import android.content.Context
import android.content.Intent
import kotlinx.serialization.json.Json
import kotlinx.serialization.json.JsonObject
import kotlinx.serialization.json.JsonPrimitive
import java.io.File

/**
 * Configuración del Titan Launcher entregada por el servidor TitanMDM
 * (comando APPLY_LAUNCHER_POLICY) a equipos inscritos con el agente,
 * sin Android Enterprise. Usa las mismas claves que la managed
 * configuration de Google (titan_*), así el lector y las validaciones
 * son idénticos.
 */
object TitanServerLauncherStore {

    const val FILE_NAME = "titan_server_launcher.json"

    /** Aviso interno al launcher para releer la configuración. */
    const val ACTION_CHANGED = "com.titanmdm.agent.action.LAUNCHER_POLICY_CHANGED"

    private val json = Json

    fun read(context: Context): Map<String, String> = read(context.filesDir)

    fun read(directory: File): Map<String, String> {
        val file = File(directory, FILE_NAME)
        if (!file.isFile) return emptyMap()
        val text = runCatching { file.readText() }.getOrNull() ?: return emptyMap()
        return TitanLabPolicyJson.toRestrictions(text) ?: emptyMap()
    }

    /** Guarda (o borra con un mapa vacío) y avisa al launcher. */
    fun save(context: Context, restrictions: Map<String, String>) {
        save(context.filesDir, restrictions)
        context.sendBroadcast(Intent(ACTION_CHANGED).setPackage(context.packageName))
    }

    fun save(directory: File, restrictions: Map<String, String>) {
        val file = File(directory, FILE_NAME)
        if (restrictions.isEmpty()) {
            file.delete()
            return
        }
        if (!directory.exists()) directory.mkdirs()

        // Escritura atómica: el launcher nunca lee un archivo a medias.
        val temp = File(directory, "$FILE_NAME.tmp")
        val content = JsonObject(restrictions.mapValues { JsonPrimitive(it.value) })
        temp.writeText(json.encodeToString(JsonObject.serializer(), content))
        if (!temp.renameTo(file)) {
            file.delete()
            temp.renameTo(file)
        }
    }
}
