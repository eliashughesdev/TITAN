package com.titanmdm.agent.launcher

import org.junit.Assert.assertEquals
import org.junit.Assert.assertFalse
import org.junit.Assert.assertNotNull
import org.junit.Assert.assertNull
import org.junit.Assert.assertTrue
import org.junit.Test

/**
 * Pruebas JVM de la resolución de política del Titan Launcher.
 *
 * Cubren prioridad entre la configuración administrada real y el perfil
 * local de laboratorio, el aislamiento debug/release y el bloqueo en
 * dispositivos realmente administrados.
 */
class TitanLauncherPolicyResolverTest {

    private val labProfile =
        """
        {
          "titan_company_name": "Laboratorio",
          "titan_welcome_message": "Perfil local",
          "titan_launcher_enabled": true,
          "titan_launcher_schema_version": 1,
          "titan_allowed_apps": [
            {"packageName":"com.microsoft.teams","label":"Teams","required":true},
            {"packageName":"com.microsoft.office.outlook","label":"Outlook","required":true},
            {"packageName":"com.android.chrome","label":"Chrome","required":false}
          ]
        }
        """.trimIndent()

    @Test
    fun `perfil de laboratorio valido se aplica y marca modo laboratorio`() {
        val resolution = TitanLauncherPolicyResolver.resolve(
            officialRestrictions = emptyMap(),
            labProfileJson = labProfile,
            labEnabled = true,
            deviceManaged = false
        )

        val policy = resolution.policy

        assertEquals(TitanLauncherPolicyOrigin.LabLocal, resolution.origin)
        assertTrue(policy.isLabProfile)
        assertEquals(TitanLauncherPolicyState.Valid, policy.state)
        assertEquals(3, policy.applications.size)

        // Modo laboratorio nunca equivale a administración real.
        assertFalse(policy.managed)

        assertEquals("com.microsoft.teams", policy.applications[0].packageName)
        assertTrue(policy.applications[0].required)
        assertEquals("com.android.chrome", policy.applications[2].packageName)
        assertFalse(policy.applications[2].required)
    }

    @Test
    fun `perfil de laboratorio invalido se reporta como invalido`() {
        val resolution = TitanLauncherPolicyResolver.resolve(
            officialRestrictions = emptyMap(),
            labProfileJson = "{ esto no es json",
            labEnabled = true,
            deviceManaged = false
        )

        assertEquals(TitanLauncherPolicyOrigin.LabLocal, resolution.origin)
        assertTrue(resolution.policy.isLabProfile)
        assertEquals(TitanLauncherPolicyState.Invalid, resolution.policy.state)
        assertFalse(resolution.policy.managed)
        assertNotNull(resolution.policy.error)
    }

    @Test
    fun `perfil con launcher deshabilitado queda ausente en modo laboratorio`() {
        val disabled =
            """
            {
              "titan_launcher_enabled": false,
              "titan_launcher_schema_version": 1,
              "titan_allowed_apps": []
            }
            """.trimIndent()

        val resolution = TitanLauncherPolicyResolver.resolve(
            officialRestrictions = emptyMap(),
            labProfileJson = disabled,
            labEnabled = true,
            deviceManaged = false
        )

        assertEquals(TitanLauncherPolicyOrigin.LabLocal, resolution.origin)
        assertEquals(TitanLauncherPolicyState.Absent, resolution.policy.state)
        assertTrue(resolution.policy.isLabProfile)
    }

    @Test
    fun `sin politica administrada ni perfil queda ausente`() {
        val resolution = TitanLauncherPolicyResolver.resolve(
            officialRestrictions = emptyMap(),
            labProfileJson = null,
            labEnabled = true,
            deviceManaged = false
        )

        assertEquals(TitanLauncherPolicyOrigin.LabLocal, resolution.origin)
        assertEquals(TitanLauncherPolicyState.Absent, resolution.policy.state)
        assertTrue(resolution.policy.isLabProfile)
        assertTrue(resolution.policy.applications.isEmpty())
    }

    @Test
    fun `las restricciones administradas tienen prioridad sobre el laboratorio`() {
        val official = mapOf(
            TitanLauncherPolicyReader.KEY_SCHEMA_VERSION to "1",
            TitanLauncherPolicyReader.KEY_ENABLED to "true",
            TitanLauncherPolicyReader.KEY_COMPANY to "Empresa Real",
            TitanLauncherPolicyReader.KEY_APPS to
                """[{"packageName":"com.acme.corp","label":"Corp"}]"""
        )

        val resolution = TitanLauncherPolicyResolver.resolve(
            officialRestrictions = official,
            labProfileJson = labProfile,
            labEnabled = true,
            deviceManaged = false
        )

        assertEquals(
            TitanLauncherPolicyOrigin.AndroidDevicePolicy,
            resolution.origin
        )
        assertFalse(resolution.policy.isLabProfile)
        assertTrue(resolution.policy.managed)
        assertEquals("Empresa Real", resolution.policy.companyName)
        assertEquals(
            "com.acme.corp",
            resolution.policy.applications.single().packageName
        )
    }

    @Test
    fun `laboratorio deshabilitado no se aplica (aislamiento release)`() {
        val resolution = TitanLauncherPolicyResolver.resolve(
            officialRestrictions = emptyMap(),
            labProfileJson = labProfile,
            labEnabled = false,
            deviceManaged = false
        )

        assertEquals(TitanLauncherPolicyOrigin.None, resolution.origin)
        assertFalse(resolution.policy.isLabProfile)
        assertEquals(TitanLauncherPolicyState.Absent, resolution.policy.state)
    }

    @Test
    fun `dispositivo administrado bloquea el perfil de laboratorio`() {
        val resolution = TitanLauncherPolicyResolver.resolve(
            officialRestrictions = emptyMap(),
            labProfileJson = labProfile,
            labEnabled = true,
            deviceManaged = true
        )

        assertEquals(TitanLauncherPolicyOrigin.None, resolution.origin)
        assertFalse(resolution.policy.isLabProfile)
        assertEquals(TitanLauncherPolicyState.Absent, resolution.policy.state)
    }

    @Test
    fun `restricciones administradas invalidas no caen al laboratorio`() {
        val official = mapOf(
            TitanLauncherPolicyReader.KEY_ENABLED to "true"
            // sin lista de aplicaciones => inválido (fail-closed)
        )

        val resolution = TitanLauncherPolicyResolver.resolve(
            officialRestrictions = official,
            labProfileJson = labProfile,
            labEnabled = true,
            deviceManaged = false
        )

        assertEquals(
            TitanLauncherPolicyOrigin.AndroidDevicePolicy,
            resolution.origin
        )
        assertEquals(TitanLauncherPolicyState.Invalid, resolution.policy.state)
        assertFalse(resolution.policy.isLabProfile)
    }

    @Test
    fun `json de laboratorio aplana arreglos y primitivos a restricciones`() {
        val values = TitanLabPolicyJson.toRestrictions(labProfile)

        assertNotNull(values)
        assertEquals("Laboratorio", values!![TitanLauncherPolicyReader.KEY_COMPANY])
        assertEquals("true", values[TitanLauncherPolicyReader.KEY_ENABLED])
        assertEquals("1", values[TitanLauncherPolicyReader.KEY_SCHEMA_VERSION])

        val apps = values[TitanLauncherPolicyReader.KEY_APPS]
        assertNotNull(apps)
        assertTrue(apps!!.trimStart().startsWith("["))
    }

    @Test
    fun `json de laboratorio no objeto devuelve null`() {
        assertNull(TitanLabPolicyJson.toRestrictions("[1,2,3]"))
        assertNull(TitanLabPolicyJson.toRestrictions("no-json"))
    }
}
