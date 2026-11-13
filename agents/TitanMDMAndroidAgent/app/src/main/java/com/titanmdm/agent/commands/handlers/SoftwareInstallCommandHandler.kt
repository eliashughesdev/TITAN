package com.titanmdm.agent.commands.handlers

import android.content.Context
import com.titanmdm.agent.apps.install.ApkDownloader
import com.titanmdm.agent.apps.install.ManagedAppDownload
import com.titanmdm.agent.apps.install.ManagedGms
import com.titanmdm.agent.apps.install.SilentPackageInstaller
import com.titanmdm.agent.commands.CommandExecutionResult
import com.titanmdm.agent.core.network.DeviceCommandDto
import com.titanmdm.agent.core.storage.AgentIdentityStore
import kotlinx.serialization.json.Json
import kotlinx.serialization.json.JsonObject
import kotlinx.serialization.json.JsonPrimitive
import kotlinx.serialization.json.buildJsonObject
import kotlinx.serialization.json.contentOrNull
import kotlinx.serialization.json.put

/**
 * SOFTWARE_INSTALL: despliegue de un APK del repositorio de software a
 * este equipo (o a su grupo) desde la consola.
 *
 * Payload: { packageId, downloadUrl, fileName, expectedSha256 }
 */
class SoftwareInstallCommandHandler(
    private val context: Context
) : CommandHandler {

    private val json = Json { ignoreUnknownKeys = true }

    override fun supports(commandType: String): Boolean =
        commandType.equals(COMMAND_TYPE, ignoreCase = true)

    override suspend fun execute(command: DeviceCommandDto): CommandExecutionResult {
        val payload = runCatching { json.parseToJsonElement(command.payloadJson) as? JsonObject }.getOrNull()
            ?: return failure("INVALID_PAYLOAD", "El payload del despliegue no es un objeto JSON.")

        fun text(name: String) = (payload[name] as? JsonPrimitive)?.contentOrNull?.trim()?.takeIf { it.isNotEmpty() }

        val packageName = text("packageName")
        val downloadUrl = when {
            packageName != null && !ManagedGms.hasManagedContainer(context) ->
                ManagedAppDownload.url(packageName)
            else -> text("downloadUrl")
        } ?: return failure("INVALID_PAYLOAD", "Falta downloadUrl.")
        val sha256 = text("expectedSha256") ?: return failure("INVALID_PAYLOAD", "Falta expectedSha256.")
        val fileName = text("fileName") ?: "paquete.apk"
        if (!fileName.endsWith(".apk", ignoreCase = true)) {
            return failure("UNSUPPORTED_PACKAGE", "Android solo instala paquetes APK ($fileName).")
        }

        val identity = AgentIdentityStore(context).get()
            ?: return failure("NOT_ENROLLED", "El equipo no está inscrito.")

        val installer = SilentPackageInstaller(context)
        val downloader = ApkDownloader(context)
        val urls = (if (packageName != null) {
            ManagedAppDownload.urls(packageName, text("downloadUrl"), ManagedGms.hasManagedContainer(context))
        } else {
            emptyList()
        }).ifEmpty { listOf(downloadUrl) }
        var downloaded: ApkDownloader.Outcome = ApkDownloader.Outcome.Failed("DOWNLOAD_URL", "Sin URL de descarga.")
        for (relativeUrl in urls) {
            downloaded = downloader.download(identity, relativeUrl, sha256, text("packageId") ?: packageName ?: fileName)
            if (downloaded is ApkDownloader.Outcome.Downloaded) break
        }
        val file = when (downloaded) {
            is ApkDownloader.Outcome.Downloaded -> downloaded.file
            is ApkDownloader.Outcome.Failed -> return failure(downloaded.code, downloaded.message)
        }

        try {
            val archive = installer.readArchive(file)
                ?: return failure("INVALID_APK", "$fileName no es un APK válido.")

            return when (val outcome = installer.install(file, archive.packageName)) {
                SilentPackageInstaller.Outcome.Installed -> CommandExecutionResult.Success(
                    resultJson = buildJsonObject {
                        put("commandId", command.commandId)
                        put("packageName", archive.packageName)
                        put("versionCode", archive.versionCode)
                        put("installed", true)
                    }.toString()
                )

                is SilentPackageInstaller.Outcome.Failed -> failure(outcome.code, outcome.message)
            }
        } finally {
            file.delete()
        }
    }

    private fun failure(code: String, message: String) =
        CommandExecutionResult.Failure(errorCode = code, errorMessage = message)

    companion object {
        const val COMMAND_TYPE = "SOFTWARE_INSTALL"
    }
}
