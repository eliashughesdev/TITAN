package com.titanmdm.agent.launcher

import org.junit.Assert.assertEquals
import org.junit.Assert.assertFalse
import org.junit.Assert.assertNotNull
import org.junit.Assert.assertNull
import org.junit.Assert.assertTrue
import org.junit.Test
import java.io.File

class TitanLauncherResourceTest {

    @Test
    fun `acepta https publico y rechaza destinos inseguros`() {
        assertNotNull(
            TitanLauncherResourceUrl.normalize(
                "https://cdn.empresa.com/launcher/fondo.webp"
            )
        )
        assertNull(TitanLauncherResourceUrl.normalize("http://cdn.empresa.com/fondo.png"))
        assertNull(TitanLauncherResourceUrl.normalize("file:///tmp/fondo.png"))
        assertNull(TitanLauncherResourceUrl.normalize("content://media/fondo.png"))
        assertNull(TitanLauncherResourceUrl.normalize("https://10.1.1.1/fondo.png"))
        assertNull(TitanLauncherResourceUrl.normalize("https://localhost/logo.png"))
        assertNull(TitanLauncherResourceUrl.normalize("https://user:pass@cdn.empresa.com/logo.png"))
        assertNull(TitanLauncherResourceUrl.normalize("data:image/png;base64,AAAA"))
    }

    @Test
    fun `detecta png jpeg webp y rechaza contenido ajeno`() {
        assertEquals(
            TitanLauncherImageFormat.Png,
            TitanLauncherImageSignatures.detect(
                byteArrayOf(0x89.toByte(), 0x50, 0x4E, 0x47, 0, 0, 0, 0, 0, 0, 0, 0)
            )
        )
        assertEquals(
            TitanLauncherImageFormat.Jpeg,
            TitanLauncherImageSignatures.detect(
                byteArrayOf(0xFF.toByte(), 0xD8.toByte(), 0xFF.toByte(), 0, 0, 0, 0, 0, 0, 0, 0, 0)
            )
        )
        assertNull(TitanLauncherImageSignatures.detect("GIF89a......".toByteArray()))
        assertNull(TitanLauncherImageSignatures.detect("<html></html>".toByteArray()))
    }

    @Test
    fun `cache valida integridad y rechaza metadatos alterados`() {
        val directory = File("build/launcher-cache-test").apply {
            deleteRecursively()
            mkdirs()
        }
        val cache = TitanLauncherResourceCache(directory)
        val png = byteArrayOf(0x89.toByte(), 0x50, 0x4E, 0x47, 0, 0, 0, 0, 0, 0, 0, 1)
        val url = "https://cdn.empresa.com/launcher/logo.png"

        assertNotNull(cache.write(url, "logo", png))
        assertNotNull(cache.read(url, "logo"))

        val key = directory.listFiles()?.first { it.name.endsWith(".bin") }
        key?.writeBytes(png + 9)
        assertNull(cache.read(url, "logo"))
    }

    @Test
    fun `descarga fallida conserva la cache de la misma url`() {
        val directory = File("build/launcher-fetch-test").apply {
            deleteRecursively()
            mkdirs()
        }
        val cache = TitanLauncherResourceCache(directory)
        val png = byteArrayOf(0x89.toByte(), 0x50, 0x4E, 0x47, 0, 0, 0, 0, 0, 0, 0, 2)
        val url = "https://cdn.empresa.com/launcher/fondo.png"
        cache.write(url, "wallpaper", png)

        val fetcher = TitanLauncherResourceFetcher(
            transport = { TitanLauncherHttpResponse(503) },
            cache = cache,
            sleep = {}
        )

        val result = fetcher.fetch(url, "wallpaper", 1024)
        assertTrue(result.fromCache)
        assertNotNull(result.bytes)
        assertEquals("HTTP 503 al descargar el recurso.", result.error)
    }

    @Test
    fun `redireccion interna se rechaza`() {
        val directory = File("build/launcher-redirect-test").apply { mkdirs() }
        val fetcher = TitanLauncherResourceFetcher(
            transport = {
                TitanLauncherHttpResponse(
                    statusCode = 302,
                    location = "http://127.0.0.1/fondo.png"
                )
            },
            cache = TitanLauncherResourceCache(directory),
            sleep = {}
        )

        val result = fetcher.fetch(
            "https://cdn.empresa.com/launcher/fondo.png",
            "wallpaper",
            1024
        )
        assertNull(result.bytes)
        assertEquals("Redirección insegura rechazada.", result.error)
        assertFalse(result.fromCache)
    }

    @Test
    fun `laboratorio no se reporta como restriccion administrada`() {
        val policy = TitanLauncherPolicy(state = TitanLauncherPolicyState.Valid, isLabProfile = true)
        val ack = TitanLauncherAcknowledgementStore.fromPolicy(
            policy,
            TitanLauncherPolicyOrigin.LabLocal,
            resourceFailed = false
        )

        assertTrue(ack.lab)
        assertNull(ack.toHeartbeatFields())
    }

    @Test
    fun `revocacion no reutiliza la version aplicada`() {
        val policy = TitanLauncherPolicy(
            state = TitanLauncherPolicyState.Absent,
            configVersion = 9,
            profileId = "11111111-1111-1111-1111-111111111111"
        )
        val ack = TitanLauncherAcknowledgementStore.fromPolicy(
            policy,
            TitanLauncherPolicyOrigin.AndroidDevicePolicy,
            resourceFailed = false
        )

        assertEquals("Revoked", ack.syncStatus)
        assertNull(ack.configVersion)
        assertTrue(ack.restrictionsRead)
    }
}
