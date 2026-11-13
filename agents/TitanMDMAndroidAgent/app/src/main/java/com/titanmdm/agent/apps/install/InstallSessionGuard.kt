package com.titanmdm.agent.apps.install

import java.util.concurrent.ConcurrentHashMap

/** Paquetes cuya confirmación de instalación puede pulsar el servicio de accesibilidad. */
object InstallSessionGuard {
    private val packages = ConcurrentHashMap.newKeySet<String>()

    fun allow(packageName: String) {
        packages += packageName
    }

    fun clear(packageName: String) {
        packages -= packageName
    }

    fun isActive(): Boolean = packages.isNotEmpty()
}
