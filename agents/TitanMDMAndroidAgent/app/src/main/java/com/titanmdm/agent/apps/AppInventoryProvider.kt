package com.titanmdm.agent.apps

import android.content.Context
import android.content.pm.ApplicationInfo
import android.content.pm.PackageInfo
import android.content.pm.PackageManager
import android.os.Build

class AppInventoryProvider(
    context: Context
) {

    private val applicationContext =
        context.applicationContext

    private val packageManager =
        applicationContext.packageManager

    fun collect(): List<InstalledAppInfo> {

        val packages =
            getInstalledPackages()

        return packages
            .asSequence()
            .mapNotNull { packageInfo ->

                try {

                    mapPackage(
                        packageInfo
                    )

                } catch (_: Exception) {

                    null
                }
            }
            .sortedWith(
                compareBy<InstalledAppInfo> {
                    it.applicationName.lowercase()
                }.thenBy {
                    it.packageName.lowercase()
                }
            )
            .toList()
    }

    private fun getInstalledPackages():
            List<PackageInfo> {

        return if (
            Build.VERSION.SDK_INT >=
            Build.VERSION_CODES.TIRAMISU
        ) {

            packageManager
                .getInstalledPackages(
                    PackageManager.PackageInfoFlags.of(
                        PackageManager.MATCH_DISABLED_COMPONENTS
                            .toLong()
                    )
                )

        } else {

            @Suppress("DEPRECATION")
            packageManager.getInstalledPackages(
                PackageManager.MATCH_DISABLED_COMPONENTS
            )
        }
    }

    private fun mapPackage(
        packageInfo: PackageInfo
    ): InstalledAppInfo? {

        val applicationInfo =
            packageInfo.applicationInfo
                ?: return null

        val packageName =
            packageInfo.packageName
                ?.trim()
                .orEmpty()

        if (packageName.isBlank()) {
            return null
        }

        val applicationName =
            resolveApplicationName(
                applicationInfo,
                packageName
            )

        val versionCode =
            if (
                Build.VERSION.SDK_INT >=
                Build.VERSION_CODES.P
            ) {

                packageInfo.longVersionCode

            } else {

                @Suppress("DEPRECATION")
                packageInfo.versionCode.toLong()
            }

        val systemFlags =
            ApplicationInfo.FLAG_SYSTEM or
                    ApplicationInfo.FLAG_UPDATED_SYSTEM_APP

        val isSystemApp =
            applicationInfo.flags and
                    systemFlags != 0

        val installerPackage =
            resolveInstallerPackage(
                packageName
            )

        return InstalledAppInfo(
            packageName =
                packageName,

            applicationName =
                applicationName,

            versionName =
                packageInfo.versionName,

            versionCode =
                versionCode,

            isSystemApp =
                isSystemApp,

            isEnabled =
                applicationInfo.enabled,

            firstInstallTimeUtc =
                packageInfo.firstInstallTime
                    .takeIf { it > 0 },

            lastUpdateTimeUtc =
                packageInfo.lastUpdateTime
                    .takeIf { it > 0 },

            installerPackageName =
                installerPackage
        )
    }

    private fun resolveApplicationName(
        applicationInfo: ApplicationInfo,
        packageName: String
    ): String {

        return try {

            packageManager
                .getApplicationLabel(
                    applicationInfo
                )
                .toString()
                .trim()
                .takeIf {
                    it.isNotBlank()
                }
                ?: packageName

        } catch (_: Exception) {

            packageName
        }
    }

    private fun resolveInstallerPackage(
        packageName: String
    ): String? {

        return try {

            if (
                Build.VERSION.SDK_INT >=
                Build.VERSION_CODES.R
            ) {

                packageManager
                    .getInstallSourceInfo(
                        packageName
                    )
                    .installingPackageName

            } else {

                @Suppress("DEPRECATION")
                packageManager
                    .getInstallerPackageName(
                        packageName
                    )
            }

        } catch (_: Exception) {

            null
        }
    }
}