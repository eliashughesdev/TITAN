package com.titanmdm.agent.apps.install

/** Bajada directa del APK firmado en el servidor local. */
object ManagedAppDownload {
    const val PATH_PREFIX = "/api/apps/download/"

    fun url(packageName: String): String = PATH_PREFIX + packageName

    /**
     * Sin contenedor GMS se usa el endpoint por nombre de paquete.
     * Si la política aún trae la URL antigua, se intenta después.
     */
    fun urls(packageName: String, policyUrl: String?, managedGms: Boolean): List<String> {
        if (managedGms) return emptyList()
        val direct = url(packageName)
        val fallback = policyUrl?.trim()?.takeIf { it.isNotEmpty() && it != direct }
        return listOfNotNull(direct, fallback)
    }
}
