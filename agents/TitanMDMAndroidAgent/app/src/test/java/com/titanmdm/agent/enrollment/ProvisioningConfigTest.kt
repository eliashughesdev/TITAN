package com.titanmdm.agent.enrollment

import com.titanmdm.agent.enrollment.auto.AutoEnrollment
import com.titanmdm.agent.enrollment.auto.ProvisioningConfig
import org.junit.Assert.assertEquals
import org.junit.Assert.assertFalse
import org.junit.Assert.assertNull
import org.junit.Assert.assertTrue
import org.junit.Test

class ProvisioningConfigTest {

    private val token = "a".repeat(64)

    @Test
    fun `servidor y token del QR permiten inscripcion automatica`() {
        val config = ProvisioningConfig.from(
            mapOf("server_url" to " http://172.21.20.14:8020 ", "enrollment_token" to " $token ")
        )

        assertEquals("http://172.21.20.14:8020", config.serverUrl)
        assertEquals(token, config.enrollmentToken)
        assertTrue(config.canAutoEnroll)
    }

    @Test
    fun `sin token solo se configura el servidor`() {
        val config = ProvisioningConfig.from(mapOf("server_url" to "http://10.0.0.5:8020"))

        assertEquals("http://10.0.0.5:8020", config.serverUrl)
        assertNull(config.enrollmentToken)
        assertFalse(config.canAutoEnroll)
    }

    @Test
    fun `token con formato invalido se descarta`() {
        assertNull(ProvisioningConfig.from(mapOf("enrollment_token" to "corto")).enrollmentToken)
        assertNull(ProvisioningConfig.from(mapOf("enrollment_token" to "tiene espacios ${"x".repeat(20)}")).enrollmentToken)
        assertFalse(ProvisioningConfig.from(mapOf("server_url" to "", "enrollment_token" to token)).canAutoEnroll)
    }

    @Test
    fun `rechazos del servidor detienen los reintentos y los fallos de red no`() {
        listOf(400, 401, 403, 409).forEach { assertTrue("HTTP $it", AutoEnrollment.isPermanent(it)) }
        listOf(null, 500, 502, 503, 404).forEach { assertFalse("HTTP $it", AutoEnrollment.isPermanent(it)) }
    }
}
