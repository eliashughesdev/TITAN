package com.titanmdm.agent.scanner

import org.junit.Assert.assertEquals
import org.junit.Assert.assertFalse
import org.junit.Assert.assertTrue
import org.junit.Test

class HoneywellScannerPropertiesTest {

    @Test
    fun `el claim habilita QR y apaga el pitido de error`() {
        val props = HoneywellScanner.claimProperties("com.titanmdm.agent")

        assertEquals(true, props[HoneywellScanner.DEC_QR_ENABLED])
        assertEquals(false, props[HoneywellScanner.NTF_BAD_READ_ENABLED])
        assertEquals(HoneywellScanner.ACTION_BARCODE_DATA, props["DPR_DATA_INTENT_ACTION"])
        assertEquals("com.honeywell.decode.intent.ACTION_BARCODE_DATA", HoneywellScanner.ACTION_BARCODE_DATA)
        assertEquals(true, props["DPR_DATA_INTENT"])
        assertEquals("com.titanmdm.agent", props["DPR_DATA_INTENT_PACKAGE_NAME"])
        assertEquals(HoneywellScanner.RECEIVER_CLASS, props["DPR_DATA_INTENT_CLASS_NAME"])
        assertFalse(props.containsKey("EXTRA_PROFILE"))
    }

    @Test
    fun `la lectura sale de data o de los extras del perfil wedge`() {
        assertEquals("titanmdm://enroll?token=abc", HoneywellScanner.textFromExtras(mapOf("data" to "titanmdm://enroll?token=abc")))
        assertEquals("json", HoneywellScanner.textFromExtras(mapOf("com.honeywell.decode.intent.extra.DATA" to "json")))
        assertTrue(HoneywellScanner.textFromExtras(mapOf("data" to "  ")) == null)
        assertTrue(HoneywellScanner.textFromExtras(emptyMap()) == null)
    }
}
