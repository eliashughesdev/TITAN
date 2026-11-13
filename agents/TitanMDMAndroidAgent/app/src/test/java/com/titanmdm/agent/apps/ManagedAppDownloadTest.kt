package com.titanmdm.agent.apps

import com.titanmdm.agent.apps.install.ManagedAppDownload
import org.junit.Assert.assertEquals
import org.junit.Assert.assertTrue
import org.junit.Test

class ManagedAppDownloadTest {

    @Test
    fun `sin GMS baja el APK por nombre de paquete y conserva la URL de la politica`() {
        val urls = ManagedAppDownload.urls(
            "com.acme.wms",
            "/api/device/software-packages/abc/download",
            managedGms = false
        )

        assertEquals("/api/apps/download/com.acme.wms", urls.first())
        assertEquals("/api/device/software-packages/abc/download", urls[1])
    }

    @Test
    fun `con contenedor GMS no sideload`() {
        assertTrue(
            ManagedAppDownload.urls("com.acme.wms", "/api/apps/download/com.acme.wms", managedGms = true).isEmpty()
        )
    }
}
