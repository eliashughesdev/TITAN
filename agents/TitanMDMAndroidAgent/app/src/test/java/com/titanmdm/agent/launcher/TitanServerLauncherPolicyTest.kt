package com.titanmdm.agent.launcher

import org.junit.Assert.assertEquals
import org.junit.Assert.assertFalse
import org.junit.Assert.assertTrue
import org.junit.Rule
import org.junit.Test
import org.junit.rules.TemporaryFolder

/**
 * Política del launcher entregada por el servidor TitanMDM al agente
 * (equipos sin Android Enterprise, comando APPLY_LAUNCHER_POLICY).
 */
class TitanServerLauncherPolicyTest {

    @get:Rule
    val folder = TemporaryFolder()

    private val server = mapOf(
        TitanLauncherPolicyReader.KEY_SCHEMA_VERSION to "1",
        TitanLauncherPolicyReader.KEY_ENABLED to "true",
        TitanLauncherPolicyReader.KEY_COMPANY to "CISA",
        TitanLauncherPolicyReader.KEY_APPS to
            """[{"packageName":"com.honeywell.demos","label":"Demos"}]"""
    )

    @Test
    fun `sin managed config se aplica la politica del servidor`() {
        val resolution = TitanLauncherPolicyResolver.resolve(
            officialRestrictions = emptyMap(),
            labProfileJson = null,
            labEnabled = true,
            deviceManaged = false,
            serverRestrictions = server
        )

        assertEquals(TitanLauncherPolicyOrigin.TitanServer, resolution.origin)
        assertEquals("CISA", resolution.policy.companyName)
        assertFalse(resolution.policy.isLabProfile)
    }

    @Test
    fun `la managed config de Google tiene prioridad sobre el servidor`() {
        val official = server + (TitanLauncherPolicyReader.KEY_COMPANY to "Google")

        val resolution = TitanLauncherPolicyResolver.resolve(
            officialRestrictions = official,
            labProfileJson = null,
            labEnabled = false,
            deviceManaged = true,
            serverRestrictions = server
        )

        assertEquals(TitanLauncherPolicyOrigin.AndroidDevicePolicy, resolution.origin)
        assertEquals("Google", resolution.policy.companyName)
    }

    @Test
    fun `la politica del servidor genera acuse aplicado para el heartbeat`() {
        val resolution = TitanLauncherPolicyResolver.resolve(
            officialRestrictions = emptyMap(),
            labProfileJson = null,
            labEnabled = false,
            deviceManaged = false,
            serverRestrictions = server
        )

        val ack = TitanLauncherAcknowledgementStore.fromPolicy(
            resolution.policy,
            resolution.origin,
            resourceFailed = false
        )

        assertTrue(ack.restrictionsRead)
        assertEquals("Applied", ack.syncStatus)
    }

    @Test
    fun `el almacen guarda, relee y borra la configuracion`() {
        val dir = folder.root

        TitanServerLauncherStore.save(dir, server)
        assertEquals(server, TitanServerLauncherStore.read(dir))

        TitanServerLauncherStore.save(dir, emptyMap())
        assertTrue(TitanServerLauncherStore.read(dir).isEmpty())
    }
}
