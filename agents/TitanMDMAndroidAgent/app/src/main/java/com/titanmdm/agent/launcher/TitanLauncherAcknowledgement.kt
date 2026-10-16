package com.titanmdm.agent.launcher

import java.io.File
import java.time.Instant

data class TitanLauncherAcknowledgement(
    val restrictionsRead: Boolean,
    val syncStatus: String,
    val profileId: String? = null,
    val publicationId: String? = null,
    val configVersion: Int? = null,
    val appliedAtUtc: String? = null,
    val lab: Boolean = false
) {
    fun toHeartbeatFields(): Map<String, String>? {
        if (lab || !restrictionsRead) return null
        return buildMap {
            put("restrictionsRead", "true")
            put("syncStatus", syncStatus)
            profileId?.let { put("profileId", it) }
            publicationId?.let { put("publicationId", it) }
            configVersion?.let { put("configVersion", it.toString()) }
            appliedAtUtc?.let { put("appliedAtUtc", it) }
        }
    }
}

object TitanLauncherAcknowledgementStore {
    const val FILE_NAME = "titan_launcher_ack.txt"

    fun write(directory: File, acknowledgement: TitanLauncherAcknowledgement) {
        if (!directory.exists()) directory.mkdirs()
        File(directory, FILE_NAME).writeText(encode(acknowledgement))
    }

    fun read(directory: File): TitanLauncherAcknowledgement? {
        val file = File(directory, FILE_NAME)
        if (!file.isFile) return null
        return decode(runCatching { file.readText() }.getOrNull() ?: return null)
    }

    fun fromPolicy(
        policy: TitanLauncherPolicy,
        origin: TitanLauncherPolicyOrigin,
        resourceFailed: Boolean
    ): TitanLauncherAcknowledgement {
        val official = origin == TitanLauncherPolicyOrigin.AndroidDevicePolicy &&
            !policy.isLabProfile
        val status = when {
            !official -> "Absent"
            policy.state == TitanLauncherPolicyState.Valid && resourceFailed -> "ResourceError"
            policy.state == TitanLauncherPolicyState.Valid -> "Applied"
            policy.state == TitanLauncherPolicyState.Invalid -> "Invalid"
            else -> "Revoked"
        }
        val applied = official && policy.state == TitanLauncherPolicyState.Valid
        return TitanLauncherAcknowledgement(
            restrictionsRead = official,
            syncStatus = status,
            profileId = if (applied) policy.profileId else null,
            publicationId = if (applied) policy.publicationId else null,
            configVersion = if (applied) policy.configVersion else null,
            appliedAtUtc = if (applied) Instant.now().toString() else null,
            lab = policy.isLabProfile || origin == TitanLauncherPolicyOrigin.LabLocal
        )
    }

    private fun encode(value: TitanLauncherAcknowledgement): String {
        return listOf(
            value.restrictionsRead.toString(),
            value.syncStatus,
            value.profileId.orEmpty(),
            value.publicationId.orEmpty(),
            value.configVersion?.toString().orEmpty(),
            value.appliedAtUtc.orEmpty(),
            value.lab.toString()
        ).joinToString("\n")
    }

    private fun decode(text: String): TitanLauncherAcknowledgement? {
        val lines = text.lines()
        if (lines.size < 7) return null
        return TitanLauncherAcknowledgement(
            restrictionsRead = lines[0] == "true",
            syncStatus = lines[1],
            profileId = lines[2].ifBlank { null },
            publicationId = lines[3].ifBlank { null },
            configVersion = lines[4].toIntOrNull(),
            appliedAtUtc = lines[5].ifBlank { null },
            lab = lines[6] == "true"
        )
    }
}
