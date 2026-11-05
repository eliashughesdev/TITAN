package com.titanmdm.agent.kiosk

import com.titanmdm.agent.launcher.TitanLauncherPolicyReader
import org.junit.Assert.assertEquals
import org.junit.Assert.assertFalse
import org.junit.Assert.assertTrue
import org.junit.Test

class KioskSettingsTest {

    private val launcher = mapOf(
        TitanLauncherPolicyReader.KEY_SCHEMA_VERSION to "1",
        TitanLauncherPolicyReader.KEY_ENABLED to "true",
        TitanLauncherPolicyReader.KEY_APPS to
            """[{"packageName":"com.microsoft.d365.scm.warehousing","label":"WMS"}]"""
    )

    @Test
    fun `sin politica no se bloquea nada`() {
        val settings = KioskSettings.from(emptyMap())
        assertFalse(settings.launcherActive)
        assertFalse(settings.persistentHome)
        assertFalse(settings.lockStatusBar)
        assertFalse(settings.lockSystemKeys)
    }

    @Test
    fun `politica sin claves de bloqueo fija solo el HOME`() {
        val settings = KioskSettings.from(launcher)
        assertTrue(settings.launcherActive)
        assertTrue(settings.persistentHome)
        assertFalse(settings.lockStatusBar)
        assertFalse(settings.lockSystemKeys)
        assertEquals(listOf("com.microsoft.d365.scm.warehousing"), settings.allowedPackages)
    }

    @Test
    fun `los bloqueos llegan desde la politica web`() {
        val settings = KioskSettings.from(
            launcher + mapOf(
                KioskSettings.KEY_LOCK_STATUS_BAR to "true",
                KioskSettings.KEY_LOCK_SYSTEM_KEYS to "true",
                KioskSettings.KEY_PERSISTENT_HOME to "false"
            )
        )
        assertTrue(settings.lockStatusBar)
        assertTrue(settings.lockSystemKeys)
        assertFalse(settings.persistentHome)
    }

    @Test
    fun `politica del launcher invalida no bloquea el equipo`() {
        val settings = KioskSettings.from(
            mapOf(
                TitanLauncherPolicyReader.KEY_ENABLED to "true",
                KioskSettings.KEY_LOCK_STATUS_BAR to "true"
            )
        )
        assertFalse(settings.lockStatusBar)
    }
}
