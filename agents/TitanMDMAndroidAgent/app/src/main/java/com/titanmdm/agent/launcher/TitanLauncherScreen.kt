package com.titanmdm.agent.launcher

import androidx.compose.foundation.Image
import androidx.compose.foundation.background
import androidx.compose.foundation.clickable
import androidx.compose.foundation.layout.Arrangement
import androidx.compose.foundation.layout.Box
import androidx.compose.foundation.layout.Column
import androidx.compose.foundation.layout.PaddingValues
import androidx.compose.foundation.layout.Row
import androidx.compose.foundation.layout.Spacer
import androidx.compose.foundation.layout.fillMaxSize
import androidx.compose.foundation.layout.fillMaxWidth
import androidx.compose.foundation.layout.height
import androidx.compose.foundation.layout.heightIn
import androidx.compose.foundation.layout.padding
import androidx.compose.foundation.layout.size
import androidx.compose.foundation.lazy.grid.GridCells
import androidx.compose.foundation.lazy.grid.LazyVerticalGrid
import androidx.compose.foundation.lazy.grid.items
import androidx.compose.foundation.shape.CircleShape
import androidx.compose.foundation.shape.RoundedCornerShape
import androidx.compose.material3.Card
import androidx.compose.material3.CardDefaults
import androidx.compose.material3.CircularProgressIndicator
import androidx.compose.material3.HorizontalDivider
import androidx.compose.material3.MaterialTheme
import androidx.compose.material3.Scaffold
import androidx.compose.material3.Text
import androidx.compose.runtime.Composable
import androidx.compose.runtime.getValue
import androidx.compose.runtime.produceState
import androidx.compose.ui.Alignment
import androidx.compose.ui.Modifier
import androidx.compose.ui.draw.clip
import androidx.compose.ui.graphics.Brush
import androidx.compose.ui.graphics.Color
import androidx.compose.ui.graphics.ImageBitmap
import androidx.compose.ui.layout.ContentScale
import androidx.compose.ui.text.font.FontWeight
import androidx.compose.ui.text.style.TextAlign
import androidx.compose.ui.text.style.TextOverflow
import androidx.compose.ui.unit.dp
import kotlinx.coroutines.Dispatchers
import kotlinx.coroutines.withContext

private val TitanGreen = Color(0xFF16A36A)
private val TitanDark = Color(0xFF0B1720)
private val TitanSurface = Color(0xFF142831)
private val TitanSurfaceSoft = Color(0xFF1B3540)
private val TitanMuted = Color(0xFFBDCDD1)
private val TitanWarn = Color(0xFFFFC778)
private val TitanDanger = Color(0xFFE5706A)

/**
 * Pantalla principal corporativa del Titan Launcher.
 *
 * Sólo muestra y permite abrir aplicaciones autorizadas por la política.
 * No concede permisos, no lanza componentes privilegiados y no trata la
 * propia pantalla como mecanismo de control: el enforcement real de un
 * dispositivo dedicado pertenece a Android Enterprise / Android Device Policy.
 */
@Composable
fun TitanLauncherScreen(
    policy: TitanLauncherPolicy,
    homeStatus: TitanLauncherHomeStatus,
    appState: (String) -> TitanLauncherAppState,
    iconProvider: (String) -> ImageBitmap?,
    onLaunch: (String) -> Unit,
    wallpaper: ImageBitmap? = null,
    logo: ImageBitmap? = null,
    syncStatus: String = "Sin sincronizar"
) {
    val primaryColor = policyColor(policy.primaryColor, TitanGreen)
    val backgroundColor = policyColor(policy.backgroundColor, TitanDark)

    Scaffold(containerColor = backgroundColor) { padding ->
        Box(modifier = Modifier.fillMaxSize()) {
            if (wallpaper != null && policy.state == TitanLauncherPolicyState.Valid) {
                Image(
                    bitmap = wallpaper,
                    contentDescription = null,
                    contentScale = when (policy.imageFit) {
                        "contain" -> ContentScale.Fit
                        "center" -> ContentScale.Inside
                        else -> ContentScale.Crop
                    },
                    modifier = Modifier.fillMaxSize()
                )
            }
        Column(
            modifier = Modifier
                .fillMaxSize()
                .padding(padding)
                .padding(horizontal = 20.dp, vertical = 18.dp),
            verticalArrangement = Arrangement.spacedBy(18.dp)
        ) {
            CorporateHeader(policy, homeStatus, primaryColor, logo, syncStatus)

            if (policy.isLabProfile) {
                StatusBanner(
                    text = "Modo laboratorio — política local",
                    color = TitanWarn
                )
            }

            if (policy.state == TitanLauncherPolicyState.Invalid) {
                StatusBanner(
                    text = policy.error ?: "Configuración del launcher no válida.",
                    color = TitanDanger
                )
            }

            when {
                policy.state == TitanLauncherPolicyState.Valid &&
                    policy.applications.isEmpty() -> EmptyApplications()

                policy.state == TitanLauncherPolicyState.Valid -> ApplicationGrid(
                    policy = policy,
                    appState = appState,
                    iconProvider = iconProvider,
                    onLaunch = onLaunch,
                    primaryColor = primaryColor
                )

                policy.isLabProfile -> LabStandby(primaryColor)

                else -> AwaitingPolicy(primaryColor)
            }

            if (!policy.resourceError.isNullOrBlank() &&
                policy.state == TitanLauncherPolicyState.Valid
            ) {
                StatusBanner(
                    text = policy.resourceError,
                    color = TitanWarn
                )
            }
        }
        }
    }
}

@Composable
private fun CorporateHeader(
    policy: TitanLauncherPolicy,
    homeStatus: TitanLauncherHomeStatus,
    primaryColor: Color,
    logo: ImageBitmap?,
    syncStatus: String
) {
    Card(
        modifier = Modifier.fillMaxWidth(),
        shape = RoundedCornerShape(22.dp),
        colors = CardDefaults.cardColors(containerColor = TitanSurface)
    ) {
        Column(
            modifier = Modifier
                .fillMaxWidth()
                .padding(20.dp),
            verticalArrangement = Arrangement.spacedBy(16.dp)
        ) {
            Row(
                verticalAlignment = Alignment.CenterVertically,
                horizontalArrangement = Arrangement.spacedBy(14.dp)
            ) {
                if (logo != null) {
                    Image(
                        bitmap = logo,
                        contentDescription = "Logotipo corporativo",
                        modifier = Modifier
                            .size(52.dp)
                            .clip(RoundedCornerShape(16.dp))
                    )
                } else {
                    CompanyMark(policy.companyName, primaryColor)
                }

                Column(
                    modifier = Modifier.weight(1f),
                    verticalArrangement = Arrangement.spacedBy(4.dp)
                ) {
                    Text(
                        text = policy.companyName,
                        color = Color.White,
                        style = MaterialTheme.typography.headlineSmall,
                        fontWeight = FontWeight.Bold,
                        maxLines = 1,
                        overflow = TextOverflow.Ellipsis
                    )

                    Text(
                        text = policy.welcomeMessage,
                        color = TitanMuted,
                        style = MaterialTheme.typography.bodyMedium,
                        maxLines = 2,
                        overflow = TextOverflow.Ellipsis
                    )
                }
            }

            HorizontalDivider(color = TitanSurfaceSoft)

            Row(
                horizontalArrangement = Arrangement.spacedBy(8.dp)
            ) {
                StatusChip(
                    text = when {
                        policy.isLabProfile -> "Modo laboratorio"
                        policy.managed -> "Política HOME administrada"
                        else -> "Sin política HOME"
                    },
                    positive = policy.managed,
                    positiveColor = primaryColor
                )

                StatusChip(
                    text = homeStatus.label,
                    positive = homeStatus.isDefaultHome,
                    positiveColor = primaryColor
                )

                StatusChip(
                    text = syncStatus,
                    positive = syncStatus.startsWith("Aplicado"),
                    positiveColor = primaryColor
                )
            }
        }
    }
}

@Composable
private fun CompanyMark(name: String, primaryColor: Color) {
    Box(
        modifier = Modifier
            .size(52.dp)
            .clip(RoundedCornerShape(16.dp))
            .background(
                Brush.linearGradient(
                    listOf(primaryColor, primaryColor.copy(alpha = 0.68f))
                )
            ),
        contentAlignment = Alignment.Center
    ) {
        Text(
            text = name.take(1).uppercase().ifBlank { "T" },
            color = Color.White,
            style = MaterialTheme.typography.headlineSmall,
            fontWeight = FontWeight.Bold
        )
    }
}

@Composable
private fun StatusChip(
    text: String,
    positive: Boolean,
    positiveColor: Color
) {
    val color = if (positive) positiveColor else TitanWarn

    Row(
        modifier = Modifier
            .clip(RoundedCornerShape(50))
            .background(color.copy(alpha = 0.14f))
            .padding(horizontal = 12.dp, vertical = 6.dp),
        verticalAlignment = Alignment.CenterVertically,
        horizontalArrangement = Arrangement.spacedBy(6.dp)
    ) {
        Box(
            modifier = Modifier
                .size(8.dp)
                .clip(CircleShape)
                .background(color)
        )

        Text(
            text = text,
            color = color,
            style = MaterialTheme.typography.labelMedium,
            maxLines = 1,
            overflow = TextOverflow.Ellipsis
        )
    }
}

@Composable
private fun StatusBanner(text: String, color: Color) {
    Card(
        modifier = Modifier.fillMaxWidth(),
        colors = CardDefaults.cardColors(
            containerColor = color.copy(alpha = 0.14f)
        ),
        shape = RoundedCornerShape(12.dp)
    ) {
        Text(
            text = text,
            color = Color.White,
            style = MaterialTheme.typography.bodyMedium,
            modifier = Modifier.padding(14.dp)
        )
    }
}

@Composable
private fun AwaitingPolicy(primaryColor: Color) {
    Card(
        modifier = Modifier.fillMaxWidth(),
        colors = CardDefaults.cardColors(containerColor = TitanSurface),
        shape = RoundedCornerShape(16.dp)
    ) {
        Column(
            modifier = Modifier
                .fillMaxWidth()
                .padding(24.dp),
            verticalArrangement = Arrangement.spacedBy(10.dp),
            horizontalAlignment = Alignment.CenterHorizontally
        ) {
            CircularProgressIndicator(color = primaryColor)

            Text(
                text = "Esperando configuración administrada",
                color = Color.White,
                style = MaterialTheme.typography.titleMedium,
                fontWeight = FontWeight.SemiBold
            )

            Text(
                text = "El administrador todavía no ha publicado un perfil de Titan Launcher para este dispositivo.",
                color = TitanMuted,
                style = MaterialTheme.typography.bodyMedium,
                textAlign = TextAlign.Center
            )
        }
    }
}

@Composable
private fun LabStandby(primaryColor: Color) {
    Card(
        modifier = Modifier.fillMaxWidth(),
        colors = CardDefaults.cardColors(containerColor = TitanSurface),
        shape = RoundedCornerShape(16.dp)
    ) {
        Column(
            modifier = Modifier
                .fillMaxWidth()
                .padding(24.dp),
            verticalArrangement = Arrangement.spacedBy(10.dp),
            horizontalAlignment = Alignment.CenterHorizontally
        ) {
            Text(
                text = "Modo laboratorio — política local",
                color = TitanWarn,
                style = MaterialTheme.typography.titleMedium,
                fontWeight = FontWeight.SemiBold,
                textAlign = TextAlign.Center
            )

            Text(
                text = "No hay un perfil de laboratorio cargado. " +
                    "Cargue titan_lab_policy.json en el almacenamiento " +
                    "interno de la app o use el perfil empaquetado de debug.",
                color = TitanMuted,
                style = MaterialTheme.typography.bodyMedium,
                textAlign = TextAlign.Center
            )
        }
    }
}

@Composable
private fun EmptyApplications() {
    Card(
        modifier = Modifier.fillMaxWidth(),
        colors = CardDefaults.cardColors(containerColor = TitanSurface),
        shape = RoundedCornerShape(16.dp)
    ) {
        Text(
            text = "El perfil está activo pero todavía no contiene aplicaciones de trabajo autorizadas.",
            color = Color.White,
            style = MaterialTheme.typography.bodyMedium,
            modifier = Modifier.padding(24.dp)
        )
    }
}

@Composable
private fun ApplicationGrid(
    policy: TitanLauncherPolicy,
    appState: (String) -> TitanLauncherAppState,
    iconProvider: (String) -> ImageBitmap?,
    onLaunch: (String) -> Unit,
    primaryColor: Color
) {
    Column(
        modifier = Modifier.fillMaxSize(),
        verticalArrangement = Arrangement.spacedBy(12.dp)
    ) {
        Row(
            modifier = Modifier.fillMaxWidth(),
            verticalAlignment = Alignment.CenterVertically,
            horizontalArrangement = Arrangement.SpaceBetween
        ) {
            Text(
                text = "Aplicaciones de trabajo",
                color = Color.White,
                style = MaterialTheme.typography.titleLarge,
                fontWeight = FontWeight.SemiBold
            )

            Text(
                text = "${policy.applications.size} autorizadas",
                color = TitanMuted,
                style = MaterialTheme.typography.labelMedium
            )
        }

        LazyVerticalGrid(
            columns = GridCells.Fixed(policy.gridColumns.coerceIn(3, 5)),
            contentPadding = PaddingValues(bottom = 30.dp),
            horizontalArrangement = Arrangement.spacedBy(policy.gridGap.dp),
            verticalArrangement = Arrangement.spacedBy(policy.gridGap.dp),
            modifier = Modifier.fillMaxSize()
        ) {
            items(policy.applications, key = { it.packageName }) { app ->
                ApplicationTile(
                    app = app,
                    state = appState(app.packageName),
                    iconProvider = iconProvider,
                    onLaunch = onLaunch,
                    primaryColor = primaryColor
                )
            }
        }
    }
}

@Composable
private fun ApplicationTile(
    app: TitanLauncherApp,
    state: TitanLauncherAppState,
    iconProvider: (String) -> ImageBitmap?,
    onLaunch: (String) -> Unit,
    primaryColor: Color
) {
    val icon by produceState<ImageBitmap?>(
        initialValue = null,
        key1 = app.packageName
    ) {
        value = withContext(Dispatchers.IO) {
            iconProvider(app.packageName)
        }
    }

    Card(
        modifier = Modifier
            .fillMaxWidth()
            .heightIn(min = 132.dp)
            .clickable(
                enabled = state == TitanLauncherAppState.Launchable,
                onClickLabel = "Abrir ${app.label}"
            ) { onLaunch(app.packageName) },
        shape = RoundedCornerShape(18.dp),
        colors = CardDefaults.cardColors(containerColor = TitanSurface)
    ) {
        Column(
            modifier = Modifier
                .fillMaxWidth()
                .padding(16.dp),
            horizontalAlignment = Alignment.CenterHorizontally,
            verticalArrangement = Arrangement.spacedBy(10.dp)
        ) {
            AppIcon(
                bitmap = icon,
                label = app.label,
                primaryColor = primaryColor
            )

            Text(
                text = app.label,
                color = Color.White,
                textAlign = TextAlign.Center,
                style = MaterialTheme.typography.bodyMedium,
                fontWeight = FontWeight.Medium,
                maxLines = 2,
                overflow = TextOverflow.Ellipsis
            )

            Spacer(modifier = Modifier.height(2.dp))

            StatusPill(
                text = when {
                    state == TitanLauncherAppState.Launchable -> "Disponible"
                    state == TitanLauncherAppState.Disabled -> "Deshabilitada"
                    state == TitanLauncherAppState.NotLaunchable -> "Sin inicio disponible"
                    state == TitanLauncherAppState.Unavailable -> "No disponible"
                    app.required -> "Pendiente de instalación"
                    else -> "No instalada"
                },
                color = when {
                    state == TitanLauncherAppState.Launchable -> primaryColor
                    app.required -> TitanWarn
                    else -> TitanMuted
                }
            )

            if (app.required) {
                RequiredBadge()
            }
        }
    }
}

@Composable
private fun StatusPill(text: String, color: Color) {
    Row(
        verticalAlignment = Alignment.CenterVertically,
        horizontalArrangement = Arrangement.spacedBy(6.dp)
    ) {
        Box(
            modifier = Modifier
                .size(7.dp)
                .clip(CircleShape)
                .background(color)
        )

        Text(
            text = text,
            color = color,
            style = MaterialTheme.typography.labelSmall,
            textAlign = TextAlign.Center
        )
    }
}

@Composable
private fun RequiredBadge() {
    Text(
        text = "OBLIGATORIA",
        color = TitanWarn,
        style = MaterialTheme.typography.labelSmall,
        fontWeight = FontWeight.Bold,
        modifier = Modifier
            .clip(RoundedCornerShape(50))
            .background(TitanWarn.copy(alpha = 0.14f))
            .padding(horizontal = 8.dp, vertical = 3.dp)
    )
}

@Composable
private fun AppIcon(
    bitmap: ImageBitmap?,
    label: String,
    primaryColor: Color
) {
    val shape = RoundedCornerShape(16.dp)

    if (bitmap != null) {
        Image(
            bitmap = bitmap,
            contentDescription = label,
            modifier = Modifier
                .size(56.dp)
                .clip(shape)
                .background(TitanSurfaceSoft)
        )
    } else {
        Box(
            modifier = Modifier
                .size(56.dp)
                .clip(shape)
                .background(primaryColor.copy(alpha = 0.76f)),
            contentAlignment = Alignment.Center
        ) {
            Text(
                text = label.take(1).uppercase(),
                color = Color.White,
                style = MaterialTheme.typography.headlineMedium,
                fontWeight = FontWeight.Bold
            )
        }
    }
}

private fun policyColor(value: String, fallback: Color): Color {
    return runCatching {
        Color(0xFF000000L or value.removePrefix("#").toLong(16))
    }.getOrDefault(fallback)
}
