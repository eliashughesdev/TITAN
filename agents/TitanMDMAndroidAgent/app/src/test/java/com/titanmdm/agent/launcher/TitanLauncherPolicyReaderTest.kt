package com.titanmdm.agent.launcher

import org.junit.Assert.assertEquals
import org.junit.Assert.assertFalse
import org.junit.Assert.assertNull
import org.junit.Assert.assertTrue
import org.junit.Test

/**
 * Pruebas JVM del lector puro de política del Titan Launcher.
 *
 * No dependen del framework Android: validan el comportamiento
 * fail-closed, el filtrado de paquetes y los valores por defecto.
 */
class TitanLauncherPolicyReaderTest {

    @Test
    fun `launcher deshabilitado queda ausente`() {
        val policy = TitanLauncherPolicyReader.read(
            mapOf(
                TitanLauncherPolicyReader.KEY_ENABLED to "false",
                TitanLauncherPolicyReader.KEY_APPS to "[]"
            )
        )

        assertEquals(TitanLauncherPolicyState.Absent, policy.state)
        assertFalse(policy.managed)
        assertTrue(policy.applications.isEmpty())
    }

    @Test
    fun `launcher habilitado sin lista es invalido`() {
        val policy = TitanLauncherPolicyReader.read(
            mapOf(
                TitanLauncherPolicyReader.KEY_ENABLED to "true"
            )
        )

        assertEquals(TitanLauncherPolicyState.Invalid, policy.state)
        assertFalse(policy.managed)
    }

    @Test
    fun `json corrupto es invalido`() {
        val policy = TitanLauncherPolicyReader.read(
            mapOf(
                TitanLauncherPolicyReader.KEY_ENABLED to "true",
                TitanLauncherPolicyReader.KEY_APPS to "{no-es-json"
            )
        )

        assertEquals(TitanLauncherPolicyState.Invalid, policy.state)
        assertFalse(policy.managed)
    }

    @Test
    fun `aplicaciones validas se analizan`() {
        val policy = TitanLauncherPolicyReader.read(
            mapOf(
                TitanLauncherPolicyReader.KEY_ENABLED to "true",
                TitanLauncherPolicyReader.KEY_COMPANY to "  Acme Corp  ",
                TitanLauncherPolicyReader.KEY_APPS to
                    """
                    [
                      {"packageName":"com.acme.mail","label":"Correo"},
                      {"packageName":"com.acme.crm","required":true}
                    ]
                    """.trimIndent()
            )
        )

        assertEquals(TitanLauncherPolicyState.Valid, policy.state)
        assertTrue(policy.managed)
        assertEquals("Acme Corp", policy.companyName)
        assertEquals(2, policy.applications.size)

        val mail = policy.applications.first { it.packageName == "com.acme.mail" }
        assertEquals("Correo", mail.label)
        assertFalse(mail.required)

        val crm = policy.applications.first { it.packageName == "com.acme.crm" }
        assertEquals("com.acme.crm", crm.label)
        assertTrue(crm.required)
    }

    @Test
    fun `paquetes invalidos y duplicados se descartan`() {
        val policy = TitanLauncherPolicyReader.read(
            mapOf(
                TitanLauncherPolicyReader.KEY_ENABLED to "true",
                TitanLauncherPolicyReader.KEY_APPS to
                    """
                    [
                      {"packageName":"sinpunto","label":"Mal"},
                      {"packageName":"com.acme.mail","label":"Correo"},
                      {"packageName":"com.acme.mail","label":"Duplicado"}
                    ]
                    """.trimIndent()
            )
        )

        assertEquals(TitanLauncherPolicyState.Valid, policy.state)
        assertEquals(1, policy.applications.size)
        assertEquals("com.acme.mail", policy.applications.first().packageName)
    }

    @Test
    fun `esquema no soportado es invalido`() {
        val policy = TitanLauncherPolicyReader.read(
            mapOf(
                TitanLauncherPolicyReader.KEY_ENABLED to "true",
                TitanLauncherPolicyReader.KEY_SCHEMA_VERSION to "99",
                TitanLauncherPolicyReader.KEY_APPS to "[]"
            )
        )

        assertEquals(TitanLauncherPolicyState.Invalid, policy.state)
    }

    @Test
    fun `valores por defecto de empresa y mensaje`() {
        val policy = TitanLauncherPolicyReader.read(
            mapOf(
                TitanLauncherPolicyReader.KEY_ENABLED to "true",
                TitanLauncherPolicyReader.KEY_APPS to "[]"
            )
        )

        assertEquals(TitanLauncherPolicy.DEFAULT_COMPANY, policy.companyName)
        assertEquals(TitanLauncherPolicy.DEFAULT_MESSAGE, policy.welcomeMessage)
        assertNull(policy.error)
    }
}
