package com.titanmdm.agent.security

import android.app.KeyguardManager
import android.app.admin.DevicePolicyManager
import android.content.Context
import android.content.pm.PackageManager
import android.os.Build
import android.provider.Settings
import java.io.File
import java.time.Instant

class SecurityPostureProvider(
    context: Context
) {

    private val applicationContext =
        context.applicationContext

    fun collect(): SecurityPosture {

        val rootSignals =
            detectRootSignals()

        val packageInfo =
            try {
                applicationContext
                    .packageManager
                    .getPackageInfo(
                        applicationContext.packageName,
                        0
                    )
            } catch (_: Exception) {
                null
            }

        val versionCode =
            if (Build.VERSION.SDK_INT >=
                Build.VERSION_CODES.P) {

                packageInfo?.longVersionCode ?: 0L

            } else {

                @Suppress("DEPRECATION")
                packageInfo?.versionCode
                    ?.toLong()
                    ?: 0L
            }

        return SecurityPosture(
            androidVersion =
                Build.VERSION.RELEASE
                    ?: "Unknown",

            apiLevel =
                Build.VERSION.SDK_INT,

            securityPatchLevel =
                Build.VERSION.SECURITY_PATCH
                    ?.takeIf {
                        it.isNotBlank()
                    },

            buildFingerprint =
                Build.FINGERPRINT,

            deviceSecure =
                isDeviceSecure(),

            encryptionStatus =
                resolveEncryptionStatus(),

            adbEnabled =
                readGlobalFlag(
                    Settings.Global.ADB_ENABLED
                ),

            developerOptionsEnabled =
                readGlobalFlag(
                    Settings.Global
                        .DEVELOPMENT_SETTINGS_ENABLED
                ),

            rootDetected =
                rootSignals.isNotEmpty(),

            rootSignals =
                rootSignals,

            emulatorDetected =
                detectEmulator(),

            verifiedBootState =
                getSystemProperty(
                    "ro.boot.verifiedbootstate"
                ),

            bootloaderLocked =
                resolveBootloaderLocked(),

            selinuxEnforced =
                resolveSelinuxStatus(),

            agentPackageName =
                applicationContext.packageName,

            agentInstalled =
                packageInfo != null,

            agentVersionName =
                packageInfo?.versionName
                    ?: "Unknown",

            agentVersionCode =
                versionCode,

            unknownSourcesAllowed =
                resolveUnknownSources(),

            collectedAtUtc =
                Instant.now().toString()
        )
    }

    fun evaluateCompliance():
            ComplianceAssessment {

        val posture =
            collect()

        val findings =
            mutableListOf<ComplianceFinding>()

        fun add(
            code: String,
            category: String,
            severity: String,
            compliant: Boolean,
            title: String,
            description: String
        ) {
            findings.add(
                ComplianceFinding(
                    code = code,
                    category = category,
                    severity = severity,
                    compliant = compliant,
                    title = title,
                    description = description
                )
            )
        }

        add(
            code = "DEVICE_LOCK",
            category = "DeviceSecurity",
            severity = "High",
            compliant =
                posture.deviceSecure,
            title =
                "Bloqueo seguro",
            description =
                if (posture.deviceSecure)
                    "El dispositivo tiene configurado un método de bloqueo seguro."
                else
                    "El dispositivo no reporta un método de bloqueo seguro."
        )

        add(
            code = "ROOT_STATUS",
            category = "Integrity",
            severity = "Critical",
            compliant =
                !posture.rootDetected,
            title =
                "Integridad del sistema",
            description =
                if (!posture.rootDetected)
                    "No se detectaron indicadores básicos de root."
                else
                    "Se detectaron indicadores compatibles con un dispositivo rooteado."
        )

        add(
            code = "ADB_STATUS",
            category = "Development",
            severity = "Medium",
            compliant =
                !posture.adbEnabled,
            title =
                "Depuración ADB",
            description =
                if (!posture.adbEnabled)
                    "ADB está deshabilitado."
                else
                    "ADB está habilitado en el dispositivo."
        )

        add(
            code = "DEVELOPER_OPTIONS",
            category = "Development",
            severity = "Medium",
            compliant =
                !posture.developerOptionsEnabled,
            title =
                "Opciones de desarrollador",
            description =
                if (!posture.developerOptionsEnabled)
                    "Las opciones de desarrollador están deshabilitadas."
                else
                    "Las opciones de desarrollador están habilitadas."
        )

        add(
            code = "AGENT_INTEGRITY",
            category = "TitanMDM",
            severity = "Critical",
            compliant =
                posture.agentInstalled,
            title =
                "Agente TitanMDM",
            description =
                if (posture.agentInstalled)
                    "El agente TitanMDM está instalado y operativo."
                else
                    "No fue posible validar la instalación del agente."
        )

        val encrypted =
            posture.encryptionStatus
                .equals(
                    "Encrypted",
                    ignoreCase = true
                ) ||
                    posture.encryptionStatus
                        .equals(
                            "Active",
                            ignoreCase = true
                        )

        add(
            code = "ENCRYPTION",
            category = "DataProtection",
            severity = "High",
            compliant = encrypted,
            title =
                "Cifrado del dispositivo",
            description =
                "Estado reportado: ${posture.encryptionStatus}."
        )

        val bootIntegrity =
            posture.bootloaderLocked != false

        add(
            code = "BOOTLOADER",
            category = "Integrity",
            severity = "High",
            compliant = bootIntegrity,
            title =
                "Bootloader",
            description =
                when (posture.bootloaderLocked) {
                    true ->
                        "El bootloader reporta estado bloqueado."

                    false ->
                        "El bootloader reporta estado desbloqueado."

                    null ->
                        "El estado del bootloader no está disponible."
                }
        )

        val failed =
            findings.count {
                !it.compliant
            }

        val passed =
            findings.size - failed

        var penalty = 0

        findings
            .filter {
                !it.compliant
            }
            .forEach {

                penalty +=
                    when (
                        it.severity.lowercase()
                    ) {
                        "critical" -> 30
                        "high" -> 20
                        "medium" -> 10
                        else -> 5
                    }
            }

        val score =
            (100 - penalty)
                .coerceIn(
                    0,
                    100
                )

        val riskLevel =
            when {
                score >= 90 ->
                    "Low"

                score >= 70 ->
                    "Medium"

                score >= 40 ->
                    "High"

                else ->
                    "Critical"
            }

        val status =
            if (failed == 0)
                "Compliant"
            else
                "NonCompliant"

        return ComplianceAssessment(
            status = status,
            score = score,
            riskLevel = riskLevel,
            totalChecks =
                findings.size,
            passedChecks =
                passed,
            failedChecks =
                failed,
            findings =
                findings,
            posture =
                posture,
            evaluatedAtUtc =
                Instant.now().toString()
        )
    }

    private fun isDeviceSecure(): Boolean {

        val manager =
            applicationContext
                .getSystemService(
                    Context.KEYGUARD_SERVICE
                ) as? KeyguardManager

        return manager
            ?.isDeviceSecure
            ?: false
    }

    private fun resolveEncryptionStatus():
            String {

        val manager =
            applicationContext
                .getSystemService(
                    Context.DEVICE_POLICY_SERVICE
                ) as? DevicePolicyManager
                ?: return "Unknown"

        @Suppress("DEPRECATION")
        return when (
            manager.storageEncryptionStatus
        ) {
            DevicePolicyManager
                .ENCRYPTION_STATUS_ACTIVE,

            DevicePolicyManager
                .ENCRYPTION_STATUS_ACTIVE_DEFAULT_KEY,

            DevicePolicyManager
                .ENCRYPTION_STATUS_ACTIVE_PER_USER ->
                "Encrypted"

            DevicePolicyManager
                .ENCRYPTION_STATUS_INACTIVE ->
                "NotEncrypted"

            DevicePolicyManager
                .ENCRYPTION_STATUS_ACTIVATING ->
                "Activating"

            else ->
                "Unknown"
        }
    }

    private fun readGlobalFlag(
        key: String
    ): Boolean {

        return try {

            Settings.Global.getInt(
                applicationContext
                    .contentResolver,
                key,
                0
            ) == 1

        } catch (_: Exception) {
            false
        }
    }

    private fun detectRootSignals():
            List<String> {

        val signals =
            mutableListOf<String>()

        val paths =
            listOf(
                "/system/bin/su",
                "/system/xbin/su",
                "/sbin/su",
                "/system/app/Superuser.apk",
                "/system/app/SuperSU.apk",
                "/data/local/bin/su",
                "/data/local/xbin/su"
            )

        paths.forEach { path ->

            try {
                if (File(path).exists()) {
                    signals.add(
                        "FILE:$path"
                    )
                }
            } catch (_: Exception) {
            }
        }

        val tags =
            Build.TAGS

        if (
            tags?.contains(
                "test-keys",
                ignoreCase = true
            ) == true
        ) {
            signals.add(
                "BUILD_TEST_KEYS"
            )
        }

        val dangerousProps =
            listOf(
                "ro.debuggable" to "1",
                "ro.secure" to "0"
            )

        dangerousProps.forEach {
                (key, expected) ->

            val value =
                getSystemProperty(
                    key
                )

            if (value == expected) {
                signals.add(
                    "PROPERTY:$key=$value"
                )
            }
        }

        return signals
            .distinct()
    }

    private fun detectEmulator():
            Boolean {

        return (
                Build.FINGERPRINT
                    .startsWith(
                        "generic"
                    ) ||

                        Build.FINGERPRINT
                            .contains(
                                "emulator",
                                ignoreCase = true
                            ) ||

                        Build.MODEL
                            .contains(
                                "Emulator",
                                ignoreCase = true
                            ) ||

                        Build.MODEL
                            .contains(
                                "sdk_gphone",
                                ignoreCase = true
                            ) ||

                        Build.PRODUCT
                            .contains(
                                "sdk",
                                ignoreCase = true
                            )
                )
    }

    private fun resolveBootloaderLocked():
            Boolean? {

        val flashLocked =
            getSystemProperty(
                "ro.boot.flash.locked"
            )

        if (flashLocked == "1")
            return true

        if (flashLocked == "0")
            return false

        return when (
            getSystemProperty(
                "ro.boot.verifiedbootstate"
            )?.lowercase()
        ) {
            "green" -> true
            "orange" -> false
            else -> null
        }
    }

    private fun resolveSelinuxStatus():
            Boolean? {

        return try {

            val enforceFile =
                File(
                    "/sys/fs/selinux/enforce"
                )

            if (!enforceFile.exists())
                return null

            enforceFile
                .readText()
                .trim() == "1"

        } catch (_: Exception) {
            null
        }
    }

    private fun resolveUnknownSources():
            Boolean? {

        return try {

            if (
                Build.VERSION.SDK_INT >=
                Build.VERSION_CODES.O
            ) {
                applicationContext
                    .packageManager
                    .canRequestPackageInstalls()
            } else {

                @Suppress("DEPRECATION")
                Settings.Secure.getInt(
                    applicationContext
                        .contentResolver,
                    Settings.Secure
                        .INSTALL_NON_MARKET_APPS,
                    0
                ) == 1
            }

        } catch (_: Exception) {
            null
        }
    }

    private fun getSystemProperty(
        property: String
    ): String? {

        return try {

            val systemProperties =
                Class.forName(
                    "android.os.SystemProperties"
                )

            val get =
                systemProperties.getMethod(
                    "get",
                    String::class.java
                )

            (get.invoke(
                null,
                property
            ) as? String)
                ?.takeIf {
                    it.isNotBlank()
                }

        } catch (_: Exception) {
            null
        }
    }
}