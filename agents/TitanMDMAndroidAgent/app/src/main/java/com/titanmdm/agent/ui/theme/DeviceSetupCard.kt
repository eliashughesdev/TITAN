package com.titanmdm.agent.ui

import android.Manifest
import android.content.ActivityNotFoundException
import android.os.Build
import androidx.activity.compose.rememberLauncherForActivityResult
import androidx.activity.result.contract.ActivityResultContracts
import androidx.compose.foundation.layout.Arrangement
import androidx.compose.foundation.layout.Column
import androidx.compose.foundation.layout.Row
import androidx.compose.foundation.layout.fillMaxWidth
import androidx.compose.foundation.layout.padding
import androidx.compose.material3.Card
import androidx.compose.material3.HorizontalDivider
import androidx.compose.material3.MaterialTheme
import androidx.compose.material3.OutlinedButton
import androidx.compose.material3.Text
import androidx.compose.runtime.Composable
import androidx.compose.runtime.LaunchedEffect
import androidx.compose.runtime.getValue
import androidx.compose.runtime.mutableIntStateOf
import androidx.compose.runtime.mutableStateOf
import androidx.compose.runtime.remember
import androidx.compose.runtime.setValue
import androidx.compose.ui.Alignment
import androidx.compose.ui.Modifier
import androidx.compose.ui.graphics.Color
import androidx.compose.ui.platform.LocalContext
import androidx.compose.ui.text.font.FontWeight
import androidx.compose.ui.unit.dp
import com.titanmdm.agent.kiosk.KioskController
import com.titanmdm.agent.service.AgentForegroundService
import com.titanmdm.agent.workers.AgentWorkScheduler

private val Ok = Color(0xFF16A36A)
private val Pending = Color(0xFFB7791F)

/**
 * Preparación del equipo tras la inscripción: permisos de ubicación,
 * Titan Launcher como inicio y administrador de dispositivo.
 * Con Device Owner todo se aplica por política y la tarjeta solo informa.
 */
@Composable
fun DeviceSetupCard(enrolled: Boolean) {
    val context = LocalContext.current
    var refresh by remember { mutableIntStateOf(0) }
    var hint by remember { mutableStateOf<String?>(null) }
    val status = remember(refresh) { KioskController.apply(context) }

    fun reload() {
        refresh++
        // Reporte inmediato para que el servidor deje de ver PERMISSION_DENIED.
        if (enrolled) AgentWorkScheduler.syncNow(context.applicationContext)
    }

    val foreground = rememberLauncherForActivityResult(
        ActivityResultContracts.RequestMultiplePermissions()
    ) { reload() }

    val background = rememberLauncherForActivityResult(
        ActivityResultContracts.RequestPermission()
    ) { granted ->
        if (!granted) {
            hint = "Android exige elegir \"Permitir todo el tiempo\" en Ajustes > Ubicación de TitanMDM Agent."
        }
        reload()
    }

    val activity = rememberLauncherForActivityResult(
        ActivityResultContracts.StartActivityForResult()
    ) { reload() }

    fun requestForeground() = foreground.launch(
        arrayOf(Manifest.permission.ACCESS_FINE_LOCATION, Manifest.permission.ACCESS_COARSE_LOCATION)
    )

    // Al inscribirse se solicita la ubicación una vez, sin esperar al operador.
    LaunchedEffect(enrolled) {
        if (enrolled && !status.locationGranted && !status.deviceOwner) requestForeground()
    }

    Card(modifier = Modifier.fillMaxWidth()) {
        Column(
            modifier = Modifier.padding(16.dp),
            verticalArrangement = Arrangement.spacedBy(10.dp)
        ) {
            Text(
                "Preparación del equipo",
                style = MaterialTheme.typography.titleMedium,
                fontWeight = FontWeight.SemiBold
            )

            Text(
                if (status.deviceOwner) "Device Owner: kiosco administrado por TitanMDM."
                else "Sin Device Owner: complete los pasos manualmente.",
                style = MaterialTheme.typography.bodySmall
            )

            HorizontalDivider()

            SetupRow("Ubicación precisa", status.locationGranted, "Conceder") { requestForeground() }

            if (Build.VERSION.SDK_INT >= Build.VERSION_CODES.Q) {
                SetupRow(
                    "Ubicación en segundo plano",
                    status.backgroundLocationGranted,
                    "Permitir siempre",
                    enabled = status.locationGranted
                ) { background.launch(Manifest.permission.ACCESS_BACKGROUND_LOCATION) }
            }

            SetupRow("Titan Launcher como inicio", status.defaultHome, "Fijar") {
                try {
                    activity.launch(KioskController.requestHomeIntent(context))
                } catch (_: ActivityNotFoundException) {
                    hint = "Elija Titan Launcher en Ajustes > Aplicaciones > Aplicación de inicio."
                }
            }

            SetupRow(
                "Sin optimización de batería",
                remember(refresh) { AgentForegroundService.isIgnoringBatteryOptimizations(context) },
                "Excluir"
            ) {
                try {
                    activity.launch(AgentForegroundService.batteryOptimizationIntent(context))
                } catch (_: ActivityNotFoundException) {
                    hint = "Excluya TitanMDM Agent en Ajustes > Batería > Optimización."
                }
            }

            SetupRow("Bloqueo remoto (administrador)", status.adminActive, "Activar") {
                activity.launch(KioskController.adminActivationIntent(context))
            }

            SetupRow(
                "Barra de estado bloqueada (política)",
                status.statusBarLocked,
                actionLabel = null
            ) {}

            SetupRow(
                "Teclas del sistema bloqueadas (política)",
                status.systemKeysLocked,
                actionLabel = null
            ) {}

            if (!status.deviceOwner) {
                Text(
                    "Para bloquear la barra de estado y fijar el inicio sin intervención, " +
                        "convierta el agente en Device Owner (equipo sin cuentas):\n" +
                        "adb shell dpm set-device-owner com.titanmdm.agent/.admin.TitanDeviceAdminReceiver",
                    style = MaterialTheme.typography.bodySmall
                )
            }

            hint?.let { Text(it, style = MaterialTheme.typography.bodySmall, color = Pending) }
        }
    }
}

@Composable
private fun SetupRow(
    label: String,
    done: Boolean,
    actionLabel: String?,
    enabled: Boolean = true,
    onAction: () -> Unit
) {
    Row(
        modifier = Modifier.fillMaxWidth(),
        verticalAlignment = Alignment.CenterVertically,
        horizontalArrangement = Arrangement.SpaceBetween
    ) {
        Column(modifier = Modifier.weight(1f)) {
            Text(label, style = MaterialTheme.typography.bodyMedium)
            Text(
                if (done) "Listo" else "Pendiente",
                style = MaterialTheme.typography.labelSmall,
                color = if (done) Ok else Pending
            )
        }
        if (!done && actionLabel != null) {
            OutlinedButton(onClick = onAction, enabled = enabled) { Text(actionLabel) }
        }
    }
}
