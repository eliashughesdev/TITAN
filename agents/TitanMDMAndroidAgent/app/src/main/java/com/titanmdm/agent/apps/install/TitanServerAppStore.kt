package com.titanmdm.agent.apps.install

import android.content.Context
import kotlinx.serialization.json.Json
import kotlinx.serialization.json.JsonArray
import kotlinx.serialization.json.JsonElement
import java.io.File

/**
 * Metadatos de instalación de las apps enviadas por APPLY_LAUNCHER_POLICY.
 * Se conservan para que el launcher pueda reintentar la instalación
 * silenciosa cuando el operador toca una app obligatoria.
 */
object TitanServerAppStore {

    const val FILE_NAME = "titan_server_apps.json"

    private val json = Json

    fun save(context: Context, applications: JsonElement?) {
        val file = File(context.filesDir, FILE_NAME)

        if (applications == null ||
            (applications is JsonArray && applications.isEmpty())
        ) {
            file.delete()
            return
        }

        val temp = File(context.filesDir, "$FILE_NAME.tmp")
        temp.writeText(applications.toString())
        if (!temp.renameTo(file)) {
            file.delete()
            temp.renameTo(file)
        }
    }

    fun read(context: Context): List<PolicyApp> {
        val file = File(context.filesDir, FILE_NAME)
        if (!file.isFile) return emptyList()

        val text = runCatching { file.readText() }.getOrNull()
            ?: return emptyList()
        val element = runCatching { json.parseToJsonElement(text) }
            .getOrNull()
            ?: return emptyList()

        return PolicyApp.parseList(element)
    }
}
