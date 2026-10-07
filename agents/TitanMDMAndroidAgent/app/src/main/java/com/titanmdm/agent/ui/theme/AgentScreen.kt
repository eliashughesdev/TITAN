package com.titanmdm.agent.ui

import androidx.compose.foundation.layout.Arrangement
import androidx.compose.foundation.layout.Column
import androidx.compose.foundation.layout.Row
import androidx.compose.foundation.layout.Spacer
import androidx.compose.foundation.layout.fillMaxSize
import androidx.compose.foundation.layout.fillMaxWidth
import androidx.compose.foundation.layout.height
import androidx.compose.foundation.layout.padding
import androidx.compose.foundation.rememberScrollState
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
import androidx.compose.runtime.Composable
import androidx.compose.runtime.collectAsState
import androidx.compose.runtime.getValue
import androidx.compose.ui.Alignment
import androidx.compose.ui.Modifier
import androidx.compose.ui.text.font.FontWeight
import androidx.compose.ui.text.input.PasswordVisualTransformation
import androidx.compose.ui.unit.dp
import androidx.lifecycle.viewmodel.compose.viewModel

@Composable
fun TitanAgentScreen(
    viewModel: AgentViewModel = viewModel()
) {

    val state by
    viewModel.uiState.collectAsState()

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

            Text(
                text = "TitanMDM",
                style =
                    MaterialTheme.typography.headlineLarge,
                fontWeight = FontWeight.Bold
            )

            Text(
                text = "Android Enterprise Agent",
                style =
                    MaterialTheme.typography.titleMedium
            )

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
                    onTokenChanged =
                        viewModel::updateEnrollmentToken,
                    onRegister =
                        viewModel::register
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
                value = "TitanMDM"
            )

            InformationRow(
                label = "Último heartbeat",
                value =
                    state.lastHeartbeatStatus
            )
        }
    }
}

@Composable
private fun EnrollmentCard(
    state: AgentUiState,
    onTokenChanged: (String) -> Unit,
    onRegister: () -> Unit
) {

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
                    "Introduce el token generado desde el centro de inscripción de TitanMDM."
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
                    Modifier.fillMaxWidth()
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