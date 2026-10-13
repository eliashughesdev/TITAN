package com.titanmdm.agent.launcher

import androidx.compose.foundation.Image
import androidx.compose.foundation.background
import androidx.compose.foundation.clickable
import androidx.compose.foundation.layout.Arrangement
import androidx.compose.foundation.layout.Box
import androidx.compose.foundation.layout.Column
import androidx.compose.foundation.layout.PaddingValues
import androidx.compose.foundation.layout.Row
import androidx.compose.foundation.layout.fillMaxSize
import androidx.compose.foundation.layout.fillMaxWidth
import androidx.compose.foundation.layout.padding
import androidx.compose.foundation.layout.size
import androidx.compose.foundation.lazy.grid.GridCells
import androidx.compose.foundation.lazy.grid.LazyVerticalGrid
import androidx.compose.foundation.lazy.grid.items
import androidx.compose.foundation.shape.RoundedCornerShape
import androidx.compose.material3.Card
import androidx.compose.material3.CardDefaults
import androidx.compose.material3.CircularProgressIndicator
import androidx.compose.material3.MaterialTheme
import androidx.compose.material3.Scaffold
import androidx.compose.material3.Text
import androidx.compose.runtime.Composable
import androidx.compose.runtime.getValue
import androidx.compose.runtime.produceState
import androidx.compose.ui.Alignment
import androidx.compose.ui.Modifier
import androidx.compose.ui.graphics.Color
import androidx.compose.ui.graphics.ImageBitmap
import androidx.compose.ui.text.font.FontWeight
import androidx.compose.ui.text.style.TextAlign
import androidx.compose.ui.text.style.TextOverflow
import androidx.compose.ui.unit.dp
import kotlinx.coroutines.Dispatchers
import kotlinx.coroutines.withContext

private val TitanGreen = Color(0xFF16A36A)
private val TitanGreenDark = Color(0xFF0E7A4E)
private val TitanDark = Color(0xFF0B1720)
private val TitanSurface = Color(0xFF142831)
private val TitanMuted = Color(0xFFBDCDD1)
private val TitanWarn = Color(0xFFFFC778)
private val TitanDanger = Color(0xFFE5706A)

/**
 * Pantalla principal corporativa del Titan Launcher.
 *
 * Sólo muestra y permite abrir aplicaciones autorizadas por la política.
 * No concede permisos, no lanza componentes privilegiados y no trata la
 * propia pantalla como mecanismo de control: el enforcement real de un
 * dispositivo dedicado pertenece a Android Enterprise / Device Owner.
 */
@Composable
fun TitanLauncherScreen(
    policy: TitanLauncherPolicy,
    adminStatus: TitanLauncherAdminStatus,
    isInstalled: (String) -> Boolean,
    iconProvider: (String) -> ImageBitmap?,
    onLaunch: (String) -> Unit
) {
    Scaffold(containerColor = TitanDark) { padding ->
        Column(
            modifier = Modifier
                .fillMaxSize()
                .padding(padding)
                .padding(horizontal = 20.dp, vertical = 18.dp),
            verticalArrangement = Arrangement.spacedBy(18.dp)
        ) {
            LauncherHeader(policy, adminStatus)

            if (policy.state == TitanLauncherPolicyState.Invalid) {
                StatusBanner(
                    text = policy.error ?: "Configuración del launcher no válida.",
                    color = TitanDanger
                )
            }

            when {
                !policy.managed -> AwaitingPolicy()
                policy.applications.isEmpty() -> EmptyApplications()
                else -> ApplicationGrid(
                    policy = policy,
                    isInstalled = isInstalled,
                    iconProvider = iconProvider,
                    onLaunch = onLaunch
                )
            }
        }
    }
}

@Composable
private fun LauncherHeader(
    policy: TitanLauncherPolicy,
    adminStatus: TitanLauncherAdminStatus
) {
    Column(verticalArrangement = Arrangement.spacedBy(6.dp)) {
        Text(
            text = policy.companyName,
            color = Color.White,
            style = MaterialTheme.typography.headlineMedium,
            fontWeight = FontWeight.Bold,
            maxLines = 1,
            overflow = TextOverflow.Ellipsis
        )

        Text(
            text = policy.welcomeMessage,
            color = TitanMuted,
            style = MaterialTheme.typography.bodyLarge,
            maxLines = 2,
            overflow = TextOverflow.Ellipsis
        )

        Row(horizontalArrangement = Arrangement.spacedBy(8.dp)) {
            StatusChip(
                text = if (policy.managed) "Perfil corporativo activo" else "Sin perfil",
                color = if (policy.managed) TitanGreen else TitanWarn
            )

            StatusChip(
                text = adminStatus.label,
                color = if (adminStatus.isManaged) TitanGreen else TitanWarn
            )
        }
    }
}

@Composable
private fun StatusChip(text: String, color: Color) {
    Text(
        text = text,
        color = color,
        style = MaterialTheme.typography.labelMedium,
        modifier = Modifier
            .background(
                color.copy(alpha = 0.12f),
                RoundedCornerShape(50)
            )
            .padding(horizontal = 10.dp, vertical = 4.dp)
    )
}

@Composable
private fun StatusBanner(text: String, color: Color) {
    Card(
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
private fun AwaitingPolicy() {
    Card(
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
            CircularProgressIndicator(color = TitanGreen)

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
private fun EmptyApplications() {
    Card(
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
    isInstalled: (String) -> Boolean,
    iconProvider: (String) -> ImageBitmap?,
    onLaunch: (String) -> Unit
) {
    Text(
        text = "Aplicaciones de trabajo",
        color = Color.White,
        style = MaterialTheme.typography.titleLarge,
        fontWeight = FontWeight.SemiBold
    )

    LazyVerticalGrid(
        columns = GridCells.Adaptive(minSize = 136.dp),
        contentPadding = PaddingValues(bottom = 30.dp),
        horizontalArrangement = Arrangement.spacedBy(12.dp),
        verticalArrangement = Arrangement.spacedBy(12.dp),
        modifier = Modifier.fillMaxSize()
    ) {
        items(policy.applications, key = { it.packageName }) { app ->
            ApplicationTile(
                app = app,
                installed = isInstalled(app.packageName),
                iconProvider = iconProvider,
                onLaunch = onLaunch
            )
        }
    }
}

@Composable
private fun ApplicationTile(
    app: TitanLauncherApp,
    installed: Boolean,
    iconProvider: (String) -> ImageBitmap?,
    onLaunch: (String) -> Unit
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
            .clickable(enabled = installed) { onLaunch(app.packageName) },
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
            AppIcon(bitmap = icon, label = app.label)

            Text(
                text = app.label,
                color = Color.White,
                textAlign = TextAlign.Center,
                style = MaterialTheme.typography.bodyMedium,
                maxLines = 2,
                overflow = TextOverflow.Ellipsis
            )

            Text(
                text = when {
                    installed -> "Disponible"
                    app.required -> "Pendiente de instalación"
                    else -> "No instalada"
                },
                color = when {
                    installed -> TitanGreen
                    app.required -> TitanWarn
                    else -> TitanMuted
                },
                style = MaterialTheme.typography.labelSmall,
                textAlign = TextAlign.Center
            )
        }
    }
}

@Composable
private fun AppIcon(bitmap: ImageBitmap?, label: String) {
    val shape = RoundedCornerShape(16.dp)

    if (bitmap != null) {
        Image(
            bitmap = bitmap,
            contentDescription = label,
            modifier = Modifier
                .size(56.dp)
                .background(TitanSurface, shape)
        )
    } else {
        Box(
            modifier = Modifier
                .size(56.dp)
                .background(TitanGreenDark, shape),
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
