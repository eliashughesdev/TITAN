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
                      {"packageName":"COM.ACME.MAIL","label":"Duplicado"}
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
    fun `esquema presente debe ser entero v1 exacto`() {
        listOf("", "malformado", "0", "-1", "3", "99").forEach { schema ->
            val policy = TitanLauncherPolicyReader.read(
                mapOf(
                    TitanLauncherPolicyReader.KEY_SCHEMA_VERSION to schema,
                    TitanLauncherPolicyReader.KEY_ENABLED to "true",
                    TitanLauncherPolicyReader.KEY_APPS to "[]"
                )
            )

            assertEquals(
                "schema=$schema",
                TitanLauncherPolicyState.Invalid,
                policy.state
            )
        }
    }

    @Test
    fun `esquema v1 explicito exige booleano y no infiere apps`() {
        val missingEnabled = TitanLauncherPolicyReader.read(
            mapOf(
                TitanLauncherPolicyReader.KEY_SCHEMA_VERSION to "1",
                TitanLauncherPolicyReader.KEY_APPS to "[]"
            )
        )

        assertEquals(TitanLauncherPolicyState.Invalid, missingEnabled.state)

        listOf("", "1", "yes", "enabled").forEach { enabled ->
            val malformed = TitanLauncherPolicyReader.read(
                explicitPolicy(enabled = enabled, apps = "[]")
            )
            assertEquals(
                "enabled=$enabled",
                TitanLauncherPolicyState.Invalid,
                malformed.state
            )
        }
    }

    @Test
    fun `configuracion sin esquema conserva inferencia legacy v1`() {
        val policy = TitanLauncherPolicyReader.read(
            mapOf(
                TitanLauncherPolicyReader.KEY_APPS to
                    """[{"packageName":"com.acme.legacy"}]"""
            )
        )

        assertEquals(TitanLauncherPolicyState.Valid, policy.state)
        assertTrue(policy.launcherEnabled)
        assertEquals(1, policy.schemaVersion)
    }

    @Test
    fun `booleano legacy presente tambien se valida estrictamente`() {
        val policy = TitanLauncherPolicyReader.read(
            mapOf(
                TitanLauncherPolicyReader.KEY_ENABLED to "on",
                TitanLauncherPolicyReader.KEY_APPS to "[]"
            )
        )

        assertEquals(TitanLauncherPolicyState.Invalid, policy.state)
    }

    @Test
    fun `lista vacia y lista solo invalida son validas y equivalentes`() {
        val empty = TitanLauncherPolicyReader.read(explicitPolicy(apps = "[]"))
        val invalidOnly = TitanLauncherPolicyReader.read(
            explicitPolicy(
                apps =
                    """
                    [
                      {"packageName":"sinpunto"},
                      {"packageName":123},
                      "com.acme.texto"
                    ]
                    """.trimIndent()
            )
        )

        assertEquals(TitanLauncherPolicyState.Valid, empty.state)
        assertEquals(TitanLauncherPolicyState.Valid, invalidOnly.state)
        assertTrue(empty.applications.isEmpty())
        assertTrue(invalidOnly.applications.isEmpty())
    }

    @Test
    fun `validacion y deduplicacion ocurren antes del maximo`() {
        val invalidEntries = (0 until 110).map {
            """{"packageName":"invalid$it"}"""
        }
        val validEntries = listOf(
            """{"packageName":"com.acme.app0","label":"Primera"}""",
            """{"packageName":"COM.ACME.APP0","label":"Duplicada"}"""
        ) + (1..104).map {
            """{"packageName":"com.acme.app$it"}"""
        }
        val policy = TitanLauncherPolicyReader.read(
            explicitPolicy(apps = (invalidEntries + validEntries).joinToString(",", "[", "]"))
        )

        assertEquals(TitanLauncherPolicyState.Valid, policy.state)
        assertEquals(100, policy.applications.size)
        assertEquals("com.acme.app0", policy.applications.first().packageName)
        assertEquals("Primera", policy.applications.first().label)
        assertEquals("com.acme.app99", policy.applications.last().packageName)
    }

    @Test
    fun `forceInstalled solo se conserva para configuracion legacy`() {
        val apps = """[{"packageName":"com.acme.required","forceInstalled":true}]"""
        val legacy = TitanLauncherPolicyReader.read(
            mapOf(TitanLauncherPolicyReader.KEY_APPS to apps)
        )
        val explicit = TitanLauncherPolicyReader.read(explicitPolicy(apps = apps))

        assertTrue(legacy.applications.single().required)
        assertFalse(explicit.applications.single().required)
    }

    @Test
    fun `branding mensaje y etiquetas se validan y truncan`() {
        val longCompany = " A\u0000" + "c".repeat(100)
        val longMessage = "  Hola\n equipo  " + "m".repeat(200)
        val policy = TitanLauncherPolicyReader.read(
            explicitPolicy(
                apps =
                    """[{"packageName":"com.acme.app","label":" App\n${"x".repeat(100)}"}]"""
            ) + mapOf(
                TitanLauncherPolicyReader.KEY_COMPANY to longCompany,
                TitanLauncherPolicyReader.KEY_MESSAGE to longMessage,
                TitanLauncherPolicyReader.KEY_PRIMARY_COLOR to "#a1b2c3",
                TitanLauncherPolicyReader.KEY_BACKGROUND_COLOR to "#102030"
            )
        )

        assertEquals(80, policy.companyName.length)
        assertFalse(policy.companyName.any { it.isISOControl() })
        assertEquals(180, policy.welcomeMessage.length)
        assertTrue(policy.welcomeMessage.startsWith("Hola equipo"))
        assertFalse(policy.welcomeMessage.any { it.isISOControl() })
        assertEquals(60, policy.applications.single().label.length)
        assertFalse(policy.applications.single().label.any { it.isISOControl() })
        assertEquals("#A1B2C3", policy.primaryColor)
        assertEquals("#102030", policy.backgroundColor)
    }

    @Test
    fun `colores invalidos usan valores por defecto`() {
        val policy = TitanLauncherPolicyReader.read(
            explicitPolicy(apps = "[]") + mapOf(
                TitanLauncherPolicyReader.KEY_PRIMARY_COLOR to "#12345",
                TitanLauncherPolicyReader.KEY_BACKGROUND_COLOR to "red"
            )
        )

        assertEquals(TitanLauncherPolicy.DEFAULT_PRIMARY_COLOR, policy.primaryColor)
        assertEquals(TitanLauncherPolicy.DEFAULT_BACKGROUND_COLOR, policy.backgroundColor)
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

    @Test
    fun `perfil anterior sin campos nuevos sigue siendo valido`() {
        val policy = TitanLauncherPolicyReader.read(explicitPolicy(apps = "[]"))

        assertEquals(TitanLauncherPolicyState.Valid, policy.state)
        assertEquals(4, policy.gridColumns)
        assertNull(policy.wallpaperUrl)
        assertNull(policy.logoUrl)
        assertNull(policy.configVersion)
    }

    @Test
    fun `esquema 2 acepta recursos y columnas`() {
        val policy = TitanLauncherPolicyReader.read(
            explicitPolicy(apps = "[]") + mapOf(
                TitanLauncherPolicyReader.KEY_SCHEMA_VERSION to "2",
                TitanLauncherPolicyReader.KEY_WALLPAPER_URL to
                    "https://cdn.empresa.com/launcher/fondo.webp",
                TitanLauncherPolicyReader.KEY_LOGO_URL to
                    "https://cdn.empresa.com/launcher/logo.png",
                TitanLauncherPolicyReader.KEY_GRID_COLUMNS to "3",
                TitanLauncherPolicyReader.KEY_CONFIG_VERSION to "8"
            )
        )

        assertEquals(TitanLauncherPolicyState.Valid, policy.state)
        assertEquals(3, policy.gridColumns)
        assertEquals(8, policy.configVersion)
        assertEquals(
            "https://cdn.empresa.com/launcher/fondo.webp",
            policy.wallpaperUrl
        )
    }

    @Test
    fun `columnas fuera de rango invalidan la politica`() {
        val policy = TitanLauncherPolicyReader.read(
            explicitPolicy(apps = "[]") + mapOf(
                TitanLauncherPolicyReader.KEY_GRID_COLUMNS to "9"
            )
        )

        assertEquals(TitanLauncherPolicyState.Invalid, policy.state)
    }

    @Test
    fun `recurso inseguro se rechaza sin tumbar las aplicaciones`() {
        val policy = TitanLauncherPolicyReader.read(
            explicitPolicy(apps = """[{"packageName":"com.acme.mail","label":"Mail"}]""") +
                mapOf(TitanLauncherPolicyReader.KEY_WALLPAPER_URL to "http://10.0.0.8/fondo.png")
        )

        assertEquals(TitanLauncherPolicyState.Valid, policy.state)
        assertNull(policy.wallpaperUrl)
        assertEquals("Recurso HTTPS rechazado.", policy.resourceError)
        assertEquals(1, policy.applications.size)
    }

    @Test
    fun `launcher deshabilitado no conserva una politica anterior`() {
        val policy = TitanLauncherPolicyReader.read(
            explicitPolicy(enabled = "false", apps = "[]") + mapOf(
                TitanLauncherPolicyReader.KEY_CONFIG_VERSION to "4",
                TitanLauncherPolicyReader.KEY_WALLPAPER_URL to
                    "https://cdn.empresa.com/launcher/fondo.webp"
            )
        )

        assertEquals(TitanLauncherPolicyState.Absent, policy.state)
        assertNull(policy.configVersion)
        assertNull(policy.wallpaperUrl)
        assertFalse(policy.managed)
    }

    @Test
    fun `json corrupto sigue siendo invalido`() {
        val policy = TitanLauncherPolicyReader.read(
            mapOf(
                TitanLauncherPolicyReader.KEY_ENABLED to "true",
                TitanLauncherPolicyReader.KEY_APPS to "{no-es-json",
                TitanLauncherPolicyReader.KEY_GRID_COLUMNS to "4"
            )
        )

        assertEquals(TitanLauncherPolicyState.Invalid, policy.state)
    }

    private fun explicitPolicy(
        enabled: String = "true",
        apps: String
    ): Map<String, String> = mapOf(
        TitanLauncherPolicyReader.KEY_SCHEMA_VERSION to "1",
        TitanLauncherPolicyReader.KEY_ENABLED to enabled,
        TitanLauncherPolicyReader.KEY_APPS to apps
    )
}
