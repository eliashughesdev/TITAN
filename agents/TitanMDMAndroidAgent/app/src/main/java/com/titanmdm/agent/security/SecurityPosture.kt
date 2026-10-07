package com.titanmdm.agent.security

import kotlinx.serialization.Serializable

@Serializable
data class SecurityPosture(
    val platform: String = "Android",
    val androidVersion: String,
    val apiLevel: Int,
    val securityPatchLevel: String?,
    val buildFingerprint: String,
    val deviceSecure: Boolean,
    val encryptionStatus: String,
    val adbEnabled: Boolean,
    val developerOptionsEnabled: Boolean,
    val rootDetected: Boolean,
    val rootSignals: List<String>,
    val emulatorDetected: Boolean,
    val verifiedBootState: String?,
    val bootloaderLocked: Boolean?,
    val selinuxEnforced: Boolean?,
    val agentPackageName: String,
    val agentInstalled: Boolean,
    val agentVersionName: String,
    val agentVersionCode: Long,
    val unknownSourcesAllowed: Boolean?,
    val collectedAtUtc: String
)

@Serializable
data class ComplianceFinding(
    val code: String,
    val category: String,
    val severity: String,
    val compliant: Boolean,
    val title: String,
    val description: String
)

@Serializable
data class ComplianceAssessment(
    val platform: String = "Android",
    val status: String,
    val score: Int,
    val riskLevel: String,
    val totalChecks: Int,
    val passedChecks: Int,
    val failedChecks: Int,
    val findings: List<ComplianceFinding>,
    val posture: SecurityPosture,
    val evaluatedAtUtc: String
)