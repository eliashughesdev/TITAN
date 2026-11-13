package com.titanmdm.agent.enrollment

import com.titanmdm.agent.enrollment.auto.LocalProvisionFile
import org.junit.Assert.assertEquals
import org.junit.Assert.assertNull
import org.junit.Assert.assertTrue
import org.junit.Test
import java.io.File

class LocalProvisionFileTest {

    private val token = "a".repeat(64)

    @Test
    fun `lee server_url y enrollment_token de sdcard`() {
        val config = LocalProvisionFile.parse(
            """
            { "server_url": " http://172.21.20.14:8020 ", "enrollment_token": " $token " }
            """.trimIndent()
        )

        assertEquals("http://172.21.20.14:8020", config?.serverUrl)
        assertEquals(token, config?.enrollmentToken)
    }

    @Test
    fun `acepta alias del QR y camelCase`() {
        val fromQr = LocalProvisionFile.parse("""{"server":"http://10.0.0.5:8020","token":"$token"}""")
        val camel = LocalProvisionFile.parse("""{"serverUrl":"https://mdm.local","enrollmentToken":"$token"}""")

        assertEquals("http://10.0.0.5:8020", fromQr?.serverUrl)
        assertEquals(token, fromQr?.enrollmentToken)
        assertEquals("https://mdm.local", camel?.serverUrl)
        assertEquals(token, camel?.enrollmentToken)
    }

    @Test
    fun `rechaza token invalido o archivo vacio`() {
        assertNull(LocalProvisionFile.parse("""{"server_url":"http://10.0.0.5:8020","enrollment_token":"corto"}"""))
        assertNull(LocalProvisionFile.parse("""{"enrollment_token":"$token"}"""))
        assertNull(LocalProvisionFile.parse(""))
        assertNull(LocalProvisionFile.parse(null))
        assertNull(LocalProvisionFile.parse("no-json"))
    }

    @Test
    fun `el path de staging es sdcard titan_provision json`() {
        val paths = LocalProvisionFile.candidateFiles(File("/storage/emulated/0"))
            .map { it.path.replace('\\', '/') }
        assertEquals("/sdcard/${LocalProvisionFile.FILE_NAME}", LocalProvisionFile.ABSOLUTE_PATH)
        assertTrue(paths.any { it.endsWith("/sdcard/${LocalProvisionFile.FILE_NAME}") })
        assertTrue(paths.any { it.endsWith("/storage/emulated/0/${LocalProvisionFile.FILE_NAME}") })
    }
}
