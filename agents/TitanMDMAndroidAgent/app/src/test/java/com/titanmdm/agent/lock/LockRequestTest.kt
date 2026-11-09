package com.titanmdm.agent.lock

import org.junit.Assert.assertEquals
import org.junit.Assert.assertFalse
import org.junit.Assert.assertNull
import org.junit.Assert.assertTrue
import org.junit.Test

class LockRequestTest {

    private fun valid(json: String?): LockRequest =
        (LockRequest.parse(json) as LockRequest.Parsed.Valid).request

    @Test
    fun `payload vacio es un bloqueo simple`() {
        listOf(null, "", "{}", "no-json").forEach {
            val request = valid(it)
            assertFalse(request.isLostMode)
            assertNull(request.lockScreenText())
        }
    }

    @Test
    fun `pin y mensaje activan el modo extravio`() {
        val request = valid("""{"pin":" 2468 ","message":"Equipo de Almacén. Llame a TI","phone":"+52 (55) 1234-5678"}""")
        assertEquals("2468", request.pin)
        assertTrue(request.isLostMode)
        assertEquals("Equipo de Almacén. Llame a TI\nTel. +52 (55) 1234-5678", request.lockScreenText())
    }

    @Test
    fun `pin invalido se rechaza sin bloquear`() {
        listOf("123", "12345678901234567", "12a4", "١٢٣٤").forEach { pin ->
            val parsed = LockRequest.parse("""{"pin":"$pin"}""")
            assertTrue(pin, parsed is LockRequest.Parsed.Invalid)
            assertEquals("INVALID_PIN", (parsed as LockRequest.Parsed.Invalid).code)
        }
    }

    @Test
    fun `mensaje largo se recorta y pin numerico json no cuenta`() {
        val request = valid("""{"message":"${"x".repeat(400)}","pin":1234}""")
        assertEquals(LockRequest.MAX_MESSAGE, request.message?.length)
        assertNull(request.pin)
    }
}
