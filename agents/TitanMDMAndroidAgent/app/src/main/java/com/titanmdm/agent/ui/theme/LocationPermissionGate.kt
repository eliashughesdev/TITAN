package com.titanmdm.agent.ui

import android.Manifest
import android.app.Activity
import android.content.ActivityNotFoundException
import android.content.Intent
import android.content.pm.PackageManager
import android.net.Uri
import android.os.Build
import android.provider.Settings
import androidx.activity.compose.rememberLauncherForActivityResult
import androidx.activity.result.contract.ActivityResultContracts
import androidx.compose.foundation.layout.Arrangement
import androidx.compose.foundation.layout.Column
import androidx.compose.foundation.layout.Row
import androidx.compose.foundation.layout.fillMaxSize
import androidx.compose.foundation.layout.fillMaxWidth
import androidx.compose.foundation.layout.padding
import androidx.compose.foundation.rememberScrollState
import androidx.compose.foundation.verticalScroll
import androidx.compose.material3.Button
import androidx.compose.material3.Card
import androidx.compose.material3.MaterialTheme
import androidx.compose.material3.OutlinedButton
import androidx.compose.material3.Surface
import androidx.compose.material3.Text
import androidx.compose.runtime.Composable
import androidx.compose.runtime.DisposableEffect
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
import androidx.core.app.ActivityCompat
import androidx.core.content.ContextCompat
import androidx.lifecycle.Lifecycle
import androidx.lifecycle.LifecycleEventObserver
import androidx.lifecycle.compose.LocalLifecycleOwner
import com.titanmdm.agent.kiosk.KioskController
import com.titanmdm.agent.location.LocationReporter
import com.titanmdm.agent.service.AgentForegroundService

private val GateOk = Color(0xFF16A36A)
private val GatePending = Color(0xFFB7791F)

/**
 * Bloqueo obligatorio de la inscripcion manual hasta que el operador
 * conceda la ubicacion precisa y, en Android 10+, el permiso
 * "Permitir todo el tiempo".
 *
 * Con Device Owner no bloquea: [KioskController.apply] concede los
 * permisos por politica y el enrolamiento por fabrica queda listo.
 */
@Composable
fun LocationPermissionGate(
    enrolled: Boolean,
    content: @Composable () -> Unit
) {
    val context = LocalContext.current
    var refresh by remember { mutableIntStateOf(0) }
    var hint by remember { mutableStateOf<String?>(null) }
    var askedForeground by remember { mutableStateOf(false) }

    // apply() concede permisos si el equipo es Device Owner y devuelve el estado.
    val status = remember(refresh, enrolled) {
        if (enrolled) runCatching { KioskController.apply(context) }.getOrNull() else null
    }

    val fineGranted = ContextCompat.checkSelfPermission(
        context,
        Manifest.permission.ACCESS_FINE_LOCATION
    ) == PackageManager.PERMISSION_GRANTED

    val lifecycleOwner = LocalLifecycleOwner.current
    DisposableEffect(lifecycleOwner, enrolled) {
        val observer = LifecycleEventObserver { _, event ->
            if (event == Lifecycle.Event.ON_RESUME && enrolled) refresh++
        }
        lifecycleOwner.lifecycle.addObserver(observer)
        onDispose { lifecycleOwner.lifecycle.removeObserver(observer) }
    }

    val settingsLauncher = rememberLauncherForActivityResult(
        ActivityResultContracts.StartActivityForResult()
    ) { refresh++ }

    val foregroundLauncher = rememberLauncherForActivityResult(
        ActivityResultContracts.RequestMultiplePermissions()
    ) { refresh++ }

    fun openAppSettings() {
        try {
            settingsLauncher.launch(
                Intent(Settings.ACTION_APPLICATION_DETAILS_SETTINGS)
                    .setData(Uri.fromParts("package", context.packageName, null))
            )
        } catch (_: ActivityNotFoundException) {
            hint = "Abra Ajustes > Aplicaciones > TitanMDM Agent > Permisos > Ubicación."
        }
    }

    val backgroundLauncher = rememberLauncherForActivityResult(
        ActivityResultContracts.RequestPermission()
    ) { granted ->
        if (!granted) {
            hint = "Elija \"Permitir todo el tiempo\" en Permisos > Ubicación."
            openAppSettings()
        }
        refresh++
    }

    fun requestForeground() {
        val permissions = mutableListOf(
            Manifest.permission.ACCESS_FINE_LOCATION,
            Manifest.permission.ACCESS_COARSE_LOCATION
        )
        if (Build.VERSION.SDK_INT >= Build.VERSION_CODES.TIRAMISU) {
            permissions += Manifest.permission.POST_NOTIFICATIONS
        }
        foregroundLauncher.launch(permissions.toTypedArray())
    }

    val ready = status != null &&
        fineGranted &&
        status.backgroundLocationGranted &&
        status.locationServicesEnabled

    LaunchedEffect(enrolled, fineGranted) {
        if (enrolled && status != null && !status.deviceOwner &&
            !fineGranted && !askedForeground
        ) {
            askedForeground = true
            requestForeground()
        }
    }

    // Al completar los permisos: servicio con tipo "location" y reporte inmediato.
    LaunchedEffect(enrolled, ready) {
        if (enrolled && ready) {
            AgentForegroundService.start(context)
            runCatching { LocationReporter.report(context) }
        }
    }

    if (!enrolled || status == null || status.deviceOwner || ready) {
        content()
        return
    }

    Surface(modifier = Modifier.fillMaxSize()) {
        Column(
            modifier = Modifier
                .fillMaxSize()
                .verticalScroll(rememberScrollState())
                .padding(24.dp),
            verticalArrangement = Arrangement.spacedBy(16.dp),
            horizontalAlignment = Alignment.CenterHorizontally
        ) {
            Text(
                text = "Ubicación obligatoria",
                style = MaterialTheme.typography.headlineSmall,
                fontWeight = FontWeight.Bold
            )

            Text(
                text = "TitanMDM debe reportar la ubicación de este equipo para " +
                    "completar la inscripción. Conceda la ubicación precisa y elija " +
                    "\"Permitir todo el tiempo\".",
                style = MaterialTheme.typography.bodyMedium
            )

            GateStep(
                title = "1. Ubicación del sistema activada",
                done = status.locationServicesEnabled,
                action = "Activar"
            ) {
                try {
                    settingsLauncher.launch(Intent(Settings.ACTION_LOCATION_SOURCE_SETTINGS))
                } catch (_: ActivityNotFoundException) {
                    hint = "Active Ajustes > Ubicación."
                }
            }

            GateStep(
                title = "2. Ubicación precisa",
                done = fineGranted,
                action = if (askedForeground) "Reintentar" else "Conceder"
            ) {
                val activityRef = context as? Activity
                val canAsk = activityRef == null ||
                    ActivityCompat.shouldShowRequestPermissionRationale(
                        activityRef,
                        Manifest.permission.ACCESS_FINE_LOCATION
                    ) ||
                    !askedForeground
                askedForeground = true
                if (canAsk) requestForeground() else openAppSettings()
            }

            if (Build.VERSION.SDK_INT >= Build.VERSION_CODES.Q) {
                GateStep(
                    title = "3. Ubicación en segundo plano (\"Permitir todo el tiempo\")",
                    done = status.backgroundLocationGranted,
                    action =
                        if (Build.VERSION.SDK_INT >= Build.VERSION_CODES.R)
                            "Abrir ajustes"
                        else
                            "Permitir siempre",
                    enabled = fineGranted
                ) {
                    if (Build.VERSION.SDK_INT >= Build.VERSION_CODES.R) {
                        hint = "En Permisos > Ubicación elija \"Permitir todo el tiempo\"."
                        openAppSettings()
                    } else {
                        backgroundLauncher.launch(
                            Manifest.permission.ACCESS_BACKGROUND_LOCATION
                        )
                    }
                }
            }

            hint?.let {
                Text(
                    text = it,
                    style = MaterialTheme.typography.bodySmall,
                    color = GatePending
                )
            }

            Button(
                onClick = { refresh++ },
                modifier = Modifier.fillMaxWidth()
            ) {
                Text("Ya concedí los permisos: verificar")
            }

            Text(
                text = "La inscripción no finaliza hasta que la ubicación en segundo " +
                    "plano esté concedida.",
                style = MaterialTheme.typography.bodySmall
            )
        }
    }
}

@Composable
private fun GateStep(
    title: String,
    done: Boolean,
    action: String,
    enabled: Boolean = true,
    onAction: () -> Unit
) {
    Card(modifier = Modifier.fillMaxWidth()) {
        Row(
            modifier = Modifier.padding(16.dp),
            verticalAlignment = Alignment.CenterVertically,
            horizontalArrangement = Arrangement.spacedBy(12.dp)
        ) {
            Column(modifier = Modifier.weight(1f)) {
                Text(title, style = MaterialTheme.typography.bodyMedium)
                Text(
                    text = if (done) "Listo" else "Pendiente",
                    style = MaterialTheme.typography.labelSmall,
                    color = if (done) GateOk else GatePending
                )
            }
            if (!done) {
                OutlinedButton(onClick = onAction, enabled = enabled) {
                    Text(action)
                }
            }
        }
    }
}
