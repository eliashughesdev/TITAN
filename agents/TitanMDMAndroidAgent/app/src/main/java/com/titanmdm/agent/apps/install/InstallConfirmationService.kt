package com.titanmdm.agent.apps.install

import android.accessibilityservice.AccessibilityService
import android.view.accessibility.AccessibilityEvent
import android.view.accessibility.AccessibilityNodeInfo

/**
 * Confirma el diálogo del instalador del sistema solo mientras el agente
 * tiene una sesión PackageInstaller abierta. No pulsa otros diálogos.
 *
 * Solo para inscripción manual (sin Device Owner, que ya instala en
 * silencio): el operador lo activa en Ajustes > Accesibilidad. El agente
 * no lo activa por su cuenta ni restringe otros servicios (TalkBack).
 */
class InstallConfirmationService : AccessibilityService() {

    override fun onAccessibilityEvent(event: AccessibilityEvent?) {
        if (!InstallSessionGuard.isActive()) return
        val packageName = event?.packageName?.toString() ?: return
        if (packageName !in INSTALLER_PACKAGES) return
        val root = rootInActiveWindow ?: return
        clickInstall(root)
    }

    override fun onInterrupt() = Unit

    private fun clickInstall(node: AccessibilityNodeInfo) {
        val label = node.text?.toString()?.trim().orEmpty()
        if (node.isClickable && label in CONFIRM_LABELS) {
            node.performAction(AccessibilityNodeInfo.ACTION_CLICK)
            return
        }
        for (index in 0 until node.childCount) {
            node.getChild(index)?.let(::clickInstall)
        }
    }

    companion object {
        private val INSTALLER_PACKAGES = setOf(
            "com.android.packageinstaller",
            "com.google.android.packageinstaller"
        )
        private val CONFIRM_LABELS = setOf(
            "Install",
            "Instalar",
            "Update",
            "Actualizar"
        )
        // Nunca "Instalar de todas formas": eso salta el aviso de Play Protect.
    }
}
