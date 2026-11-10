package com.titanmdm.agent.scanner

import com.titanmdm.agent.core.storage.DeviceAliasStore
import org.junit.Assert.assertEquals
import org.junit.Assert.assertNull
import org.junit.Test

class KeystrokeBurstBufferTest {

    private val qr = "titanmdm://enroll?v=1&server=http%3A%2F%2F172.21.20.14%3A8020&token=abcdef0123456789abcdef"

    private fun type(buffer: KeystrokeBurstBuffer, text: String, startMs: Long, gapMs: Long): Long {
        var at = startMs
        text.forEach { char ->
            buffer.add(char, at)
            at += gapMs
        }
        return at
    }

    @Test
    fun `rafaga del lector laser entrega el texto completo`() {
        val buffer = KeystrokeBurstBuffer()
        val end = type(buffer, qr, 1_000, gapMs = 5)

        assertEquals(qr, buffer.finish(end))
    }

    @Test
    fun `sin sufijo enter la lectura se cierra por inactividad`() {
        val buffer = KeystrokeBurstBuffer()
        val end = type(buffer, qr, 1_000, gapMs = 3)

        // KeystrokeScanner llama finish() 150 ms despues de la ultima tecla.
        assertEquals(qr, buffer.finish(end + 150))
    }

    @Test
    fun `tecleo humano lento no se toma como lectura`() {
        val buffer = KeystrokeBurstBuffer()
        val end = type(buffer, "a".repeat(20), 1_000, gapMs = 250)

        assertNull(buffer.finish(end))
    }

    @Test
    fun `una pausa reinicia la lectura y los textos cortos se ignoran`() {
        val buffer = KeystrokeBurstBuffer()
        var at = type(buffer, "basura", 1_000, gapMs = 5)
        at = type(buffer, qr, at + 2_000, gapMs = 5)
        assertEquals(qr, buffer.finish(at))

        val short = KeystrokeBurstBuffer()
        assertNull(short.finish(type(short, "12345", 0, gapMs = 5)))
    }

    @Test
    fun `cabecera muestra alias, luego nombre del servidor y luego el modelo`() {
        assertEquals("HH-ALMACEN-01", DeviceAliasStore.label("HH-ALMACEN-01", "Honeywell CK65", "CK65"))
        assertEquals("Honeywell CK65", DeviceAliasStore.label(null, "Honeywell CK65", "CK65"))
        assertEquals("CK65", DeviceAliasStore.label(" ", null, "CK65"))
    }
}
