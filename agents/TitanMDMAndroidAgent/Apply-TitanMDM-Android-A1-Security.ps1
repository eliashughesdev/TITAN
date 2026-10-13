param([string]$Root = 'C:\TitanMDM')
$ErrorActionPreference = 'Stop'
$agent = Join-Path $Root 'agents\TitanMDMAndroidAgent\app'
$source = Join-Path $agent 'src\main\java\com\titanmdm\agent\core\config\AgentConfig.kt'
$gradle = Join-Path $agent 'build.gradle.kts'
$manifest = Join-Path $agent 'src\main\AndroidManifest.xml'
foreach ($f in @($source,$gradle,$manifest)) { if (!(Test-Path -LiteralPath $f)) { throw "Missing file: $f" } }
$gitbranch = (& git -C $Root branch --show-current).Trim()
if ($LASTEXITCODE -ne 0 -or !$gitbranch -or $gitbranch -in @('main','master')) {throw 'Use a feature branch, never main/master.'}
$stamp = Get-Date -Format 'yyyyMMdd-HHmmss'
foreach ($f in @($source,$gradle,$manifest)) {Copy-Item -LiteralPath $f -Destination "$f.$stamp.bak"}
$kt = @'
package com.titanmdm.agent.core.config

import com.titanmdm.agent.BuildConfig
import java.net.URI

object AgentConfig {
    const val AGENT_NAME = "TitanMDM Android Agent"
    const val AGENT_VERSION = "1.0.0"
    const val AGENT_VERSION_CODE = 1
    const val PLATFORM = "Android"
    const val CONNECT_TIMEOUT_SECONDS = 30L
    const val READ_TIMEOUT_SECONDS = 30L
    const val WRITE_TIMEOUT_SECONDS = 30L
    const val HEARTBEAT_INTERVAL_MINUTES = 15L
    const val HEARTBEAT_WORK_NAME = "titanmdm_periodic_heartbeat"
    const val COMMAND_POLL_INTERVAL_MINUTES = 15L
    const val COMMAND_WORK_NAME = "titanmdm_periodic_commands"
    const val REGISTRATION_MAX_RETRIES = 3
    const val NETWORK_RETRY_DELAY_MS = 2_000L
    const val USER_AGENT = "TitanMDM-Android-Agent/$AGENT_VERSION"
    val agentName get() = AGENT_NAME
    val agentVersion get() = AGENT_VERSION
    val agentVersionCode get() = AGENT_VERSION_CODE
    val platform get() = PLATFORM
    val heartbeatIntervalMinutes get() = HEARTBEAT_INTERVAL_MINUTES
    val commandPollIntervalMinutes get() = COMMAND_POLL_INTERVAL_MINUTES
    fun normalizedBaseUrl(): String {
        val raw = BuildConfig.TITAN_API_BASE_URL.trim()
        require(raw.isNotBlank()) { "TitanMDM API URL not configured" }
        val url = if (raw.endsWith('/')) raw else "$raw/"
        val uri = URI(url)
        require(!uri.host.isNullOrBlank() && uri.userInfo == null && uri.fragment == null) {
            "Invalid TitanMDM API URL"
        }
        val secure = uri.scheme.equals("https", ignoreCase = true)
        val emulatorDebug = BuildConfig.DEBUG && uri.scheme.equals("http", ignoreCase = true) &&
            uri.host in setOf("10.0.2.2", "localhost", "127.0.0.1")
        require(secure || emulatorDebug) { "HTTPS is mandatory outside the local debug emulator" }
        return url
    }
}
'@
[System.IO.File]::WriteAllText($source,$kt,(New-Object System.Text.UTF8Encoding($false)))
$g = [System.IO.File]::ReadAllText($gradle).Replace("`r`n", "`n")
$old = @'
    buildConfigField(
            "String",
            "TITAN_API_BASE_URL",
            "\"http://10.0.2.2:8020/\""
        )
'@
$g2 = [regex]::Replace($g, '(?ms)^\s*buildConfigField\(\s*"String",\s*"TITAN_API_BASE_URL",\s*"\\"http://10\.0\.2\.2:8020/\\""\s*\)', '', 1)
if ($g2 -eq $g) {throw 'Expected existing URL field not found in build.gradle.kts. Restored config file first from .bak.'}
$g = $g2
$g = $g.Replace('debug {' + "`n" + '            isMinifyEnabled = false', 'debug {' + "`n" + '            isMinifyEnabled = false' + "`n" + '            buildConfigField("String", "TITAN_API_BASE_URL", "\\\"http://10.0.2.2:8020/\\\"")')
# Use a more robust field representation through Gradle's quoted literal form.
$g = $g.Replace('"\\\"http://10.0.2.2:8020/\\\""', '"\"http://10.0.2.2:8020/\""')
$needle = 'release {' + "`n" + '            isMinifyEnabled = true'
$replace = @'
release {
            isMinifyEnabled = true
            val endpoint = providers.gradleProperty("titanApiBaseUrl")
                .orElse(providers.environmentVariable("TITAN_API_BASE_URL"))
                .orNull?.trim() ?: error("TITAN_API_BASE_URL is required for release builds")
            require(endpoint.startsWith("https://") &&
                    !endpoint.contains('"') && !endpoint.contains('\\')) {
                "Release API URL must be valid HTTPS"
            }
            buildConfigField("String", "TITAN_API_BASE_URL", "\\\"$endpoint\\\"")
'@
# Kotlin BuildConfig string literal must include escaped quotation marks.
$replace = $replace.Replace('"\\\"$endpoint\\\""', '"\"$endpoint\""')
if (!$g.Contains($needle)) {throw 'Gradle release block pattern missing'}
$g=$g.Replace($needle,$replace.TrimEnd())
[System.IO.File]::WriteAllText($gradle,$g,(New-Object System.Text.UTF8Encoding($false)))
$m = [System.IO.File]::ReadAllText($manifest)
if (!$m.Contains('android:usesCleartextTraffic="true"')) {throw 'Manifest already hardened or pattern changed'}
$m=$m.Replace('android:usesCleartextTraffic="true"','android:usesCleartextTraffic="false"')
[System.IO.File]::WriteAllText($manifest,$m,(New-Object System.Text.UTF8Encoding($false)))
$debugPath=Join-Path $agent 'src\debug\AndroidManifest.xml'
New-Item -ItemType Directory -Force -Path (Split-Path $debugPath) | Out-Null
$debugManifest = @'
<?xml version="1.0" encoding="utf-8"?>
<manifest xmlns:android="http://schemas.android.com/apk/res/android"
          xmlns:tools="http://schemas.android.com/tools">
    <application android:usesCleartextTraffic="true"
                 tools:replace="android:usesCleartextTraffic" />
</manifest>
'@
[System.IO.File]::WriteAllText($debugPath,$debugManifest,(New-Object System.Text.UTF8Encoding($false)))
Write-Host 'A1 security configuration updated. NO builds/tests run automatically.' -ForegroundColor Green
Write-Host 'Review git diff before building:'
& git -C $Root diff --check
& git -C $Root diff --stat
