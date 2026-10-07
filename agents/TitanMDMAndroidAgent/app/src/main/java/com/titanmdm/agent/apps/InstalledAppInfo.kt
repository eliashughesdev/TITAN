package com.titanmdm.agent.apps

import kotlinx.serialization.Serializable

@Serializable
data class InstalledAppInfo(
    val packageName: String,
    val applicationName: String,
    val versionName: String?,
    val versionCode: Long,
    val isSystemApp: Boolean,
    val isEnabled: Boolean,
    val firstInstallTimeUtc: Long?,
    val lastUpdateTimeUtc: Long?,
    val installerPackageName: String?
)