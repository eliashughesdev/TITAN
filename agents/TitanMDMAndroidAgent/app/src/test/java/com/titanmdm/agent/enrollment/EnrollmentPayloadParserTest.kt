package com.titanmdm.agent.enrollment

import com.titanmdm.agent.core.config.ServerEndpointPolicy
import org.junit.Assert.assertEquals
import org.junit.Assert.assertFalse
import org.junit.Assert.assertNull
import org.junit.Assert.assertTrue
import org.junit.Test

class EnrollmentPayloadParserTest {

    private val token = "tmdm_AbCdEf0123456789-_xyz"

    @Test
    fun parsea_uri_del_qr_de_la_consola() {
        val payload = EnrollmentPayloadParser.parse(
            "titanmdm://enroll?v=1&server=http%3A%2F%2F192.168.1.20%3A8020%2F&token=$token"
        )

        assertEquals(EnrollmentPayload("http://192.168.1.20:8020/", token), payload)
    }

    @Test
    fun tolera_sufijos_del_lector_en_modo_teclado() {
        val payload = EnrollmentPayloadParser.parse("titanmdm://enroll?token=$token&server=https%3A%2F%2Fmdm.cisa.local\r\n")

        assertEquals("https://mdm.cisa.local", payload?.serverUrl)
        assertEquals(token, payload?.token)
    }

    @Test
    fun parsea_json_y_token_plano() {
        assertEquals(
            EnrollmentPayload("https://mdm.cisa.local", token),
            EnrollmentPayloadParser.parse("""{"server":"https://mdm.cisa.local","token":"$token"}""")
        )
        assertEquals(EnrollmentPayload(null, token), EnrollmentPayloadParser.parse("  $token \n"))
    }

    @Test
    fun rechaza_contenido_que_no_es_de_inscripcion() {
        assertNull(EnrollmentPayloadParser.parse(""))
        assertNull(EnrollmentPayloadParser.parse("CISA-TI|TI-0001|abc"))
        assertNull(EnrollmentPayloadParser.parse("titanmdm://otra?token=$token"))
        assertNull(EnrollmentPayloadParser.parse("titanmdm://enroll?server=https%3A%2F%2Fx"))
        assertNull(EnrollmentPayloadParser.parse("""{"token":"corto"}"""))
    }

    @Test
    fun politica_https_siempre_y_http_solo_en_lan() {
        val https = ServerEndpointPolicy.normalize("https://mdm.cisa.com.do", allowLanHttp = false, debug = false)
        val lan = ServerEndpointPolicy.normalize("http://192.168.10.5:8020", allowLanHttp = true, debug = false)
        val lanDenied = ServerEndpointPolicy.normalize("http://192.168.10.5:8020", allowLanHttp = false, debug = false)
        val internet = ServerEndpointPolicy.normalize("http://8.8.8.8:8020", allowLanHttp = true, debug = false)

        assertEquals("https://mdm.cisa.com.do/", https.getOrNull())
        assertEquals("http://192.168.10.5:8020/", lan.getOrNull())
        assertTrue(lanDenied.isFailure)
        assertTrue(internet.isFailure)
        assertTrue(ServerEndpointPolicy.normalize("ftp://x", true, true).isFailure)
        assertTrue(ServerEndpointPolicy.normalize("https://user:pw@x/", true, true).isFailure)
    }

    @Test
    fun detecta_hosts_privados() {
        listOf("10.1.2.3", "172.20.0.1", "192.168.0.10", "mdm-server", "titan.cisa.local").forEach {
            assertTrue(it, ServerEndpointPolicy.isPrivateHost(it))
        }
        listOf("172.32.0.1", "8.8.8.8", "mdm.cisa.com.do").forEach {
            assertFalse(it, ServerEndpointPolicy.isPrivateHost(it))
        }
    }
}
