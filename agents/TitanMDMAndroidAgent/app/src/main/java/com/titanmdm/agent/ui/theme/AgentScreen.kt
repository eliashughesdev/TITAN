package com.titanmdm.agent.ui

import androidx.compose.foundation.background
import androidx.compose.foundation.layout.Box
import androidx.compose.foundation.shape.RoundedCornerShape
import androidx.compose.ui.draw.clip
import androidx.compose.ui.graphics.Color
import androidx.compose.ui.text.style.TextOverflow
import com.titanmdm.agent.core.storage.DeviceAliasStore
import androidx.compose.foundation.layout.Arrangement
import androidx.compose.foundation.layout.Column
import androidx.compose.foundation.layout.Row
import androidx.compose.foundation.layout.Spacer
import androidx.compose.foundation.layout.fillMaxSize
import androidx.compose.foundation.layout.fillMaxWidth
import androidx.compose.foundation.layout.height
import androidx.compose.foundation.layout.padding
import androidx.compose.foundation.rememberScrollState
import androidx.compose.foundation.text.KeyboardOptions
import androidx.compose.foundation.verticalScroll
import androidx.compose.material3.Button
import androidx.compose.material3.Card
import androidx.compose.material3.CircularProgressIndicator
import androidx.compose.material3.HorizontalDivider
import androidx.compose.material3.MaterialTheme
import androidx.compose.material3.OutlinedButton
import androidx.compose.material3.OutlinedTextField
import androidx.compose.material3.Scaffold
import androidx.compose.material3.Text
import android.Manifest
import android.content.pm.PackageManager
import android.os.Build
import androidx.activity.result.contract.ActivityResultContracts
import androidx.core.content.ContextCompat
import androidx.compose.runtime.Composable
import androidx.compose.runtime.DisposableEffect
import androidx.compose.runtime.LaunchedEffect
import androidx.compose.ui.focus.FocusRequester
import androidx.compose.ui.focus.focusRequester
import androidx.lifecycle.Lifecycle
import androidx.lifecycle.LifecycleEventObserver
import androidx.lifecycle.compose.LocalLifecycleOwner
import androidx.compose.runtime.remember
import androidx.compose.ui.platform.LocalContext
import androidx.activity.compose.rememberLauncherForActivityResult
import com.journeyapps.barcodescanner.ScanContract
import com.journeyapps.barcodescanner.ScanOptions
import com.titanmdm.agent.enrollment.EnrollmentPayloadParser
import com.titanmdm.agent.scanner.HoneywellScanner
import com.titanmdm.agent.scanner.KeystrokeScanner
import androidx.compose.runtime.collectAsState
import androidx.compose.runtime.getValue
import androidx.compose.ui.Alignment
import androidx.compose.ui.Modifier
import androidx.compose.ui.text.font.FontWeight
import androidx.compose.ui.text.input.KeyboardType
import androidx.compose.ui.text.input.PasswordVisualTransformation
import androidx.compose.ui.unit.dp
import androidx.lifecycle.viewmodel.compose.viewModel

@Composable
fun TitanAgentScreen(
    viewModel: AgentViewModel = viewModel()
) {

    val state by
    viewModel.uiState.collectAsState()

    if (!state.enrolled) {
        HoneywellEnrollmentCapture(
            onScanned = viewModel::applyScannedPayload,
            onResume = viewModel::consumeLocalProvision
        )
    }

    LocationPermissionGate(enrolled = state.enrolled) {
        Scaffold { paddingValues ->

        Column(
            modifier = Modifier
                .fillMaxSize()
                .padding(paddingValues)
                .verticalScroll(
                    rememberScrollState()
                )
                .padding(20.dp),
            verticalArrangement =
                Arrangement.spacedBy(16.dp)
        ) {

            AgentHeader(state)

            if (state.loading) {

                Spacer(
                    modifier =
                        Modifier.height(32.dp)
                )

                CircularProgressIndicator(
                    modifier =
                        Modifier.align(
                            Alignment.CenterHorizontally
                        )
                )

                return@Column
            }

            StatusCard(state)

            if (!state.enrolled) {

                EnrollmentCard(
                    state = state,
                    onServerChanged =
                        viewModel::updateServerUrl,
                    onTokenChanged =
                        viewModel::updateEnrollmentToken,
                    onRegister =
                        viewModel::register,
                    onScanned =
                        viewModel::applyScannedPayload
                )

            } else {

                ManagedDeviceCard(state)

                Button(
                    onClick =
                        viewModel::sendHeartbeat,
                    enabled =
                        !state.synchronizing,
                    modifier =
                        Modifier.fillMaxWidth()
                ) {

                    if (state.synchronizing) {
                        Text("Sincronizando...")
                    } else {
                        Text("Sincronizar con TitanMDM")
                    }
                }
            }

            DeviceSetupCard(enrolled = state.enrolled)

            DeviceInformationCard(state)

            state.message?.let {

                Card(
                    modifier =
                        Modifier.fillMaxWidth()
                ) {

                    Text(
                        text = it,
                        modifier =
                            Modifier.padding(16.dp),
                        color =
                            MaterialTheme.colorScheme.primary
                    )
                }
            }

            state.error?.let {

                Card(
                    modifier =
                        Modifier.fillMaxWidth()
                ) {

                    Text(
                        text = it,
                        modifier =
                            Modifier.padding(16.dp),
                        color =
                            MaterialTheme.colorScheme.error
                    )
                }
            }

            OutlinedButton(
                onClick =
                    viewModel::refresh,
                modifier =
                    Modifier.fillMaxWidth()
            ) {
                Text("Actualizar información")
            }

            Spacer(
                modifier =
                    Modifier.height(12.dp)
            )

            Text(
                text =
                    "TitanMDM Agent ${state.agentVersion}",
                style =
                    MaterialTheme.typography.bodySmall,
                modifier =
                    Modifier.align(
                        Alignment.CenterHorizontally
                    )
            )
        }
    }
    }
}

@Composable
private fun StatusCard(
    state: AgentUiState
) {

    Card(
        modifier =
            Modifier.fillMaxWidth()
    ) {

        Column(
            modifier =
                Modifier.padding(18.dp),
            verticalArrangement =
                Arrangement.spacedBy(8.dp)
        ) {

            Text(
                text = "Estado del agente",
                style =
                    MaterialTheme.typography.titleLarge,
                fontWeight = FontWeight.SemiBold
            )

            InformationRow(
                label = "Inscripción",
                value =
                    if (state.enrolled)
                        "Administrado"
                    else
                        "No inscrito"
            )

            InformationRow(
                label = "Servidor",
                value =
                    state.activeServer
                        ?: "Sin configurar (escanee el QR)"
            )

            InformationRow(
                label = "Último heartbeat",
                value =
                    state.lastHeartbeatStatus
            )
        }
    }
}

/** Cabecera verde: alias de la consola (o nombre del equipo) y estado. */
@Composable
private fun AgentHeader(state: AgentUiState) {
    val context = LocalContext.current
    // Sin alias (o borrado en la consola) vuelve al nombre del equipo.
    val alias by remember { DeviceAliasStore.observe(context) }
        .collectAsState(initial = DeviceAliasStore.alias(context))

    val title = if (state.enrolled) {
        DeviceAliasStore.label(alias, state.deviceName, "TitanMDM")
    } else {
        "TitanMDM"
    }
    val subtitle = when {
        !state.enrolled -> "Agente de inscripción interna"
        !alias.isNullOrBlank() -> "${state.manufacturer} ${state.model}".trim().ifBlank { state.deviceName }
        else -> "Equipo administrado por TitanMDM"
    }

    Box(
        modifier = Modifier
            .fillMaxWidth()
            .clip(RoundedCornerShape(18.dp))
            .background(HeaderGreen)
            .padding(horizontal = 20.dp, vertical = 18.dp)
    ) {
        Column(verticalArrangement = Arrangement.spacedBy(4.dp)) {
            Text(
                text = title,
                color = Color.White,
                style = MaterialTheme.typography.headlineMedium,
                fontWeight = FontWeight.Bold,
                maxLines = 1,
                overflow = TextOverflow.Ellipsis
            )
            Text(
                text = subtitle,
                color = Color.White.copy(alpha = 0.85f),
                style = MaterialTheme.typography.titleSmall
            )
        }
    }
}

private val HeaderGreen = Color(0xFF16A36A)

/**
 * Gatillo láser en la pantalla de token: ACTION_BARCODE_DATA (lectura
 * directa o wedge con Data Intent) y ráfaga de teclado. El claim se hace
 * ya en RESUMED: el observer no recibe el ON_RESUME que ya ocurrió, y sin
 * claim el CK65 pita error porque el QR sigue apagado.
 */
@Composable
private fun HoneywellEnrollmentCapture(
    onScanned: (String, Boolean) -> Boolean,
    onResume: () -> Unit
) {
    val context = LocalContext.current
    val honeywell = remember { HoneywellScanner.info(context) }
    val lifecycleOwner = LocalLifecycleOwner.current
    val storagePermission = rememberLauncherForActivityResult(
        ActivityResultContracts.RequestPermission()
    ) { onResume() }

    DisposableEffect(honeywell.shouldClaim, lifecycleOwner) {
        val onLaser: (String) -> Unit = { data ->
            onScanned(data, EnrollmentPayloadParser.isStructured(data))
        }
        val stop = HoneywellScanner.register(context, onLaser)
        val stopKeystroke = KeystrokeScanner.listen(onLaser)

        val claim = {
            if (honeywell.shouldClaim) HoneywellScanner.claim(context)
        }
        val observer = LifecycleEventObserver { _, event ->
            when (event) {
                Lifecycle.Event.ON_RESUME -> {
                    claim()
                    onResume()
                }
                Lifecycle.Event.ON_PAUSE -> if (honeywell.shouldClaim) HoneywellScanner.release(context)
                else -> Unit
            }
        }
        lifecycleOwner.lifecycle.addObserver(observer)
        if (lifecycleOwner.lifecycle.currentState.isAtLeast(Lifecycle.State.RESUMED)) {
            claim()
        }

        onDispose {
            lifecycleOwner.lifecycle.removeObserver(observer)
            if (honeywell.shouldClaim) HoneywellScanner.release(context)
            stop()
            stopKeystroke()
        }
    }

    LaunchedEffect(honeywell.vendorDevice) {
        if (!honeywell.vendorDevice || Build.VERSION.SDK_INT > 32) return@LaunchedEffect
        if (ContextCompat.checkSelfPermission(context, Manifest.permission.READ_EXTERNAL_STORAGE) !=
            PackageManager.PERMISSION_GRANTED
        ) {
            storagePermission.launch(Manifest.permission.READ_EXTERNAL_STORAGE)
        }
    }
}

@Composable
private fun EnrollmentCard(
    state: AgentUiState,
    onServerChanged: (String) -> Unit,
    onTokenChanged: (String) -> Unit,
    onRegister: () -> Unit,
    onScanned: (String, Boolean) -> Boolean
) {

    val context = LocalContext.current
    val honeywell = remember { HoneywellScanner.info(context) }

    // Camara del agente: origen confiable, inscribe al leer.
    val cameraScanner =
        rememberLauncherForActivityResult(ScanContract()) { result ->
            result.contents?.let { onScanned(it, true) }
        }

    // Lector en modo teclado que "escribe" en el campo con el foco: el
    // campo del token lo toma desde el inicio.
    val tokenFocus = remember { FocusRequester() }
    LaunchedEffect(Unit) { runCatching { tokenFocus.requestFocus() } }

    Card(
        modifier =
            Modifier.fillMaxWidth()
    ) {

        Column(
            modifier =
                Modifier.padding(18.dp),
            verticalArrangement =
                Arrangement.spacedBy(14.dp)
        ) {

            Text(
                text = "Inscribir dispositivo",
                style =
                    MaterialTheme.typography.titleLarge,
                fontWeight = FontWeight.SemiBold
            )

            Text(
                text =
                    if (honeywell.available)
                        "Presiona el gatillo y apunta al QR de inscripción: el equipo se inscribe solo. También puedes usar la cámara, escribir el token o copiar titan_provision.json en /sdcard."
                    else
                        "Escanea el QR, escribe el token, o copia titan_provision.json en /sdcard para inscribir sin cámara."
            )

            Button(
                onClick = {
                    cameraScanner.launch(
                        ScanOptions()
                            .setDesiredBarcodeFormats(ScanOptions.QR_CODE)
                            .setPrompt("Apunta al QR de inscripción TitanMDM")
                            .setBeepEnabled(true)
                            .setOrientationLocked(false)
                    )
                },
                enabled = !state.registering,
                modifier = Modifier.fillMaxWidth()
            ) {
                Text("Escanear QR con la cámara")
            }

            state.enrollmentServer?.let { server ->
                InformationRow(
                    label = "Servidor del QR",
                    value = server
                )
            }

            OutlinedTextField(
                value =
                    state.serverUrlInput,
                onValueChange =
                    onServerChanged,
                label = {
                    Text("Servidor TitanMDM")
                },
                placeholder = {
                    Text("http://172.21.20.14:8020")
                },
                singleLine = true,
                keyboardOptions =
                    KeyboardOptions(
                        keyboardType = KeyboardType.Uri
                    ),
                enabled =
                    !state.registering,
                modifier =
                    Modifier.fillMaxWidth()
            )

            OutlinedTextField(
                value =
                    state.enrollmentToken,
                onValueChange =
                    onTokenChanged,
                label = {
                    Text("Token de inscripción")
                },
                singleLine = true,
                visualTransformation =
                    PasswordVisualTransformation(),
                enabled =
                    !state.registering,
                modifier =
                    Modifier
                        .fillMaxWidth()
                        .focusRequester(tokenFocus)
            )

            Button(
                onClick =
                    onRegister,
                enabled =
                    !state.registering &&
                            state.enrollmentToken
                                .isNotBlank(),
                modifier =
                    Modifier.fillMaxWidth()
            ) {

                if (state.registering) {
                    Text("Inscribiendo...")
                } else {
                    Text("Inscribir en TitanMDM")
                }
            }
        }
    }
}

@Composable
private fun ManagedDeviceCard(
    state: AgentUiState
) {

    val identity =
        state.identity ?: return

    Card(
        modifier =
            Modifier.fillMaxWidth()
    ) {

        Column(
            modifier =
                Modifier.padding(18.dp),
            verticalArrangement =
                Arrangement.spacedBy(8.dp)
        ) {

            Text(
                text = "Identidad administrada",
                style =
                    MaterialTheme.typography.titleLarge,
                fontWeight = FontWeight.SemiBold
            )

            InformationRow(
                label = "Nombre",
                value = identity.deviceName
            )

            InformationRow(
                label = "Device ID",
                value = identity.deviceId
            )

            InformationRow(
                label = "Organization ID",
                value = identity.organizationId
            )

            /*
             * Nunca mostrar deviceSecret.
             */
        }
    }
}

@Composable
private fun DeviceInformationCard(
    state: AgentUiState
) {

    Card(
        modifier =
            Modifier.fillMaxWidth()
    ) {

        Column(
            modifier =
                Modifier.padding(18.dp),
            verticalArrangement =
                Arrangement.spacedBy(8.dp)
        ) {

            Text(
                text = "Información del dispositivo",
                style =
                    MaterialTheme.typography.titleLarge,
                fontWeight = FontWeight.SemiBold
            )

            InformationRow(
                label = "Dispositivo",
                value = state.deviceName
            )

            InformationRow(
                label = "Fabricante",
                value = state.manufacturer
            )

            InformationRow(
                label = "Modelo",
                value = state.model
            )

            InformationRow(
                label = "Android",
                value = state.androidVersion
            )

            InformationRow(
                label = "API Level",
                value = state.apiLevel.toString()
            )

            InformationRow(
                label = "Identificador",
                value = state.serialNumber
            )

            InformationRow(
                label = "Batería",
                value =
                    state.batteryLevel
                        ?.let { "$it%" }
                        ?: "No disponible"
            )

            InformationRow(
                label = "IP",
                value =
                    state.ipAddress
                        ?: "No disponible"
            )
        }
    }
}

@Composable
private fun InformationRow(
    label: String,
    value: String
) {

    Column(
        modifier =
            Modifier.fillMaxWidth()
    ) {

        Row(
            modifier =
                Modifier.fillMaxWidth(),
            horizontalArrangement =
                Arrangement.SpaceBetween
        ) {

            Text(
                text = label,
                fontWeight = FontWeight.Medium,
                modifier =
                    Modifier.weight(0.42f)
            )

            Text(
                text = value,
                modifier =
                    Modifier.weight(0.58f)
            )
        }

        HorizontalDivider(
            modifier =
                Modifier.padding(top = 8.dp)
        )
    }
}