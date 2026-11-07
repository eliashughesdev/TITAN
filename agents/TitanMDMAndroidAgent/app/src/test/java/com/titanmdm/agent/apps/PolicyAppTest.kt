package com.titanmdm.agent.apps

import com.titanmdm.agent.apps.install.AppAction
import com.titanmdm.agent.apps.install.AppInstallPlanner
import com.titanmdm.agent.apps.install.PolicyApp
import kotlinx.serialization.json.Json
import org.junit.Assert.assertEquals
import org.junit.Assert.assertFalse
import org.junit.Assert.assertTrue
import org.junit.Test

class PolicyAppTest {

    private val payload = """
        [
          {"packageName":"com.honeywell.demos","installType":"FORCE_INSTALLED","available":true,
           "packageId":"p1","downloadUrl":"/api/device/software-packages/p1/download",
           "sha256":"ABC","versionCode":12,"versionName":"1.2"},
          {"packageName":"com.android.chrome","installType":"PREINSTALLED","available":false},
          {"packageName":"com.microsoft.wms","installType":"AVAILABLE","available":false},
          {"packageName":"com.honeywell.demos","installType":"KIOSK","available":true,
           "downloadUrl":"/x","sha256":"DEF"},
          {"packageName":"no valido","available":true},
          {"packageName":"com.sin.url","available":true}
        ]
    """.trimIndent()

    private val apps = PolicyApp.parseList(Json.parseToJsonElement(payload))

    @Test
    fun `lee las apps validas sin duplicados`() {
        assertEquals(
            listOf("com.honeywell.demos", "com.android.chrome", "com.microsoft.wms", "com.sin.url"),
            apps.map { it.packageName }
        )

        val demos = apps[0]
        assertTrue(demos.available)
        assertEquals(12L, demos.versionCode)
        assertEquals("/api/device/software-packages/p1/download", demos.downloadUrl)
        assertFalse("sin downloadUrl/sha256 no es descargable", apps[3].available)
        assertTrue(demos.required)
        assertFalse("AVAILABLE es opcional", apps[2].required)
    }

    @Test
    fun `instala si falta o esta desactualizada`() {
        val demos = apps[0]
        assertEquals(AppAction.Install, AppInstallPlanner.plan(demos, null))
        assertEquals(AppAction.Install, AppInstallPlanner.plan(demos, 11))
        assertEquals(AppAction.UpToDate, AppInstallPlanner.plan(demos, 12))
        assertEquals(AppAction.UpToDate, AppInstallPlanner.plan(demos, 30))
    }

    @Test
    fun `sin APK solo cumple si ya esta instalada`() {
        val chrome = apps[1]
        assertEquals(AppAction.InstalledWithoutPackage, AppInstallPlanner.plan(chrome, 600))
        assertEquals(AppAction.Missing, AppInstallPlanner.plan(chrome, null))
        assertFalse(AppInstallPlanner.satisfied(AppAction.Missing))
        assertTrue(AppInstallPlanner.satisfied(AppAction.InstalledWithoutPackage))
    }

    @Test
    fun `app AVAILABLE sin APK no incumple la politica`() {
        val wms = apps[2]
        assertEquals(AppAction.SkippedOptional, AppInstallPlanner.plan(wms, null))
        assertTrue(AppInstallPlanner.satisfied(AppAction.SkippedOptional))
        assertEquals(AppAction.InstalledWithoutPackage, AppInstallPlanner.plan(wms, 3))
    }

    @Test
    fun `payload sin applications no instala nada`() {
        assertTrue(PolicyApp.parseList(null).isEmpty())
        assertTrue(PolicyApp.parseList(Json.parseToJsonElement("{}")).isEmpty())
    }
}
