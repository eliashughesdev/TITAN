package com.titanmdm.agent.ui

import android.app.Application
import androidx.lifecycle.AndroidViewModel
import androidx.lifecycle.viewModelScope
import com.titanmdm.agent.commands.CommandPollingResult
import com.titanmdm.agent.commands.CommandRepository
import com.titanmdm.agent.core.config.AgentConfig
import com.titanmdm.agent.core.storage.AgentIdentity
import com.titanmdm.agent.core.storage.AgentIdentityStore
import com.titanmdm.agent.core.storage.DeviceAliasStore
import com.titanmdm.agent.enrollment.EnrollmentPayloadParser
import com.titanmdm.agent.enrollment.registration.RegistrationRepository
import com.titanmdm.agent.enrollment.registration.RegistrationResult
import com.titanmdm.agent.heartbeat.HeartbeatRepository
import com.titanmdm.agent.heartbeat.HeartbeatResult
import com.titanmdm.agent.inventory.DeviceInventoryProvider
import com.titanmdm.agent.kiosk.KioskController
import com.titanmdm.agent.service.AgentForegroundService
import kotlinx.coroutines.Job
import kotlinx.coroutines.delay
import kotlinx.coroutines.flow.MutableStateFlow
import kotlinx.coroutines.flow.StateFlow
import kotlinx.coroutines.flow.asStateFlow
import kotlinx.coroutines.launch

data class AgentUiState(
    val loading: Boolean = true,
    val registering: Boolean = false,
    val synchronizing: Boolean = false,
    val enrolled: Boolean = false,

    val enrollmentToken: String = "",

    /** Servidor leido del QR; null = el configurado en el agente. */
    val enrollmentServer: String? = null,

    val activeServer: String? = null,

    val identity: AgentIdentity? = null,

    val deviceName: String = "",
    /** Alias de la consola; la cabecera lo muestra en lugar del modelo. */
    val deviceAlias: String? = null,
    val manufacturer: String = "",
    val model: String = "",
    val androidVersion: String = "",
    val apiLevel: Int = 0,
    val serialNumber: String = "",
    val batteryLevel: Int? = null,
    val ipAddress: String? = null,

    val agentVersion: String =
        AgentConfig.AGENT_VERSION,

    val lastHeartbeatStatus: String =
        "Sin sincronizar",

    val lastCommandStatus: String =
        "Sin consultar",

    val lastProcessedCommands: Int = 0,

    val message: String? = null,
    val error: String? = null
)

/** Pausa tras la que se da por terminada una lectura en modo teclado. */
private const val WEDGE_IDLE_MS = 300L

class AgentViewModel(
    application: Application
) : AndroidViewModel(application) {

    private val appContext =
        application.applicationContext

    /*
     * ========================================================
     * STORAGE
     * ========================================================
     */

    private val identityStore =
        AgentIdentityStore(appContext)

    /*
     * ========================================================
     * REPOSITORIES
     * ========================================================
     */

    private val registrationRepository =
        RegistrationRepository(appContext)

    private val heartbeatRepository =
        HeartbeatRepository(appContext)

    private val commandRepository =
        CommandRepository(appContext)

    /*
     * ========================================================
     * DEVICE INVENTORY
     * ========================================================
     */

    private val inventoryProvider =
        DeviceInventoryProvider(appContext)

    /*
     * ========================================================
     * UI STATE
     * ========================================================
     */

    private val _uiState =
        MutableStateFlow(
            AgentUiState()
        )

    val uiState: StateFlow<AgentUiState> =
        _uiState.asStateFlow()

    init {
        loadState()
    }

    /*
     * ========================================================
     * ENROLLMENT TOKEN
     * ========================================================
     */

    fun updateEnrollmentToken(
        value: String
    ) {

        _uiState.value =
            _uiState.value.copy(
                enrollmentToken = value,
                error = null,
                message = null
            )

        // Lector Honeywell en modo teclado: el QR completo se "escribe" en
        // el campo, a veces caracter a caracter. Se interpreta cuando la
        // rafaga termina, no con cada letra (un QR a medias no es valido).
        wedgeParseJob?.cancel()
        if (EnrollmentPayloadParser.isStructured(value)) {
            wedgeParseJob = viewModelScope.launch {
                delay(WEDGE_IDLE_MS)
                applyScannedPayload(value, autoRegister = true)
            }
        }
    }

    private var wedgeParseJob: Job? = null

    /**
     * QR leido por camara o por el lector Honeywell.
     * @param autoRegister true solo para la camara del propio agente; las
     * lecturas que llegan por broadcast exigen tocar "Inscribir".
     */
    fun applyScannedPayload(
        raw: String,
        autoRegister: Boolean
    ): Boolean {

        // El gatillo puede entregar la misma lectura por dos vias (Intent y
        // teclado); una inscripcion en curso o hecha no se repite.
        if (_uiState.value.registering || _uiState.value.enrolled) return true
        wedgeParseJob?.cancel()

        val payload = EnrollmentPayloadParser.parse(raw)

        if (payload == null) {
            _uiState.value =
                _uiState.value.copy(
                    error = "El código leído no es un QR de inscripción TitanMDM.",
                    message = null
                )
            return false
        }

        val server = payload.serverUrl?.let { url ->
            AgentConfig.validateServerUrl(url).getOrElse {
                _uiState.value =
                    _uiState.value.copy(
                        error = it.message,
                        message = null
                    )
                return false
            }
        }

        _uiState.value =
            _uiState.value.copy(
                enrollmentToken = payload.token,
                enrollmentServer = server,
                error = null,
                message = server?.let { "QR leído. Servidor: $it" } ?: "Token leído."
            )

        if (autoRegister) register()
        return true
    }

    /*
     * ========================================================
     * LOAD LOCAL AGENT STATE
     * ========================================================
     */

    fun loadState() {

        viewModelScope.launch {

            _uiState.value =
                _uiState.value.copy(
                    loading = true,
                    error = null
                )

            try {

                val inventory =
                    inventoryProvider.collect()

                val identity =
                    identityStore.get()

                if (identity != null) {
                    AgentForegroundService.start(appContext)
                }

                _uiState.value =
                    _uiState.value.copy(
                        loading = false,

                        enrolled =
                            identity != null,

                        identity =
                            identity,

                        deviceName =
                            identity?.deviceName
                                ?: inventory.deviceName,

                        deviceAlias =
                            DeviceAliasStore.alias(appContext),

                        manufacturer =
                            inventory.manufacturer,

                        model =
                            inventory.model,

                        androidVersion =
                            inventory.operatingSystemVersion,

                        apiLevel =
                            inventory.apiLevel,

                        serialNumber =
                            inventory.serialNumber,

                        batteryLevel =
                            inventory.batteryLevel,

                        ipAddress =
                            inventory.ipAddress,

                        activeServer =
                            runCatching { AgentConfig.normalizedBaseUrl() }.getOrNull()
                    )

            } catch (exception: Exception) {

                _uiState.value =
                    _uiState.value.copy(
                        loading = false,

                        error =
                            exception.message
                                ?: "No fue posible cargar el estado del agente."
                    )
            }
        }
    }

    /*
     * ========================================================
     * DEVICE REGISTRATION
     * ========================================================
     */

    fun register() {

        val token =
            _uiState.value
                .enrollmentToken
                .trim()

        if (token.isBlank()) {

            _uiState.value =
                _uiState.value.copy(
                    error =
                        "Introduce un token de inscripción."
                )

            return
        }

        if (_uiState.value.registering) {
            return
        }

        viewModelScope.launch {

            _uiState.value =
                _uiState.value.copy(
                    registering = true,
                    error = null,
                    message = null
                )

            when (
                val result =
                    registrationRepository
                        .register(
                            token,
                            _uiState.value.enrollmentServer
                        )
            ) {

                is RegistrationResult.Success -> {

                    _uiState.value =
                        _uiState.value.copy(
                            registering = false,

                            enrolled = true,

                            enrollmentToken = "",

                            enrollmentServer = null,

                            activeServer =
                                runCatching { AgentConfig.normalizedBaseUrl() }.getOrNull(),

                            identity =
                                result.identity,

                            deviceName =
                                result.identity.deviceName,

                            message =
                                "Dispositivo inscrito correctamente en TitanMDM.",

                            error = null
                        )

                    // Con Device Owner: HOME fijo, barra bloqueada y
                    // permisos de ubicación concedidos al inscribirse.
                    runCatching {
                        KioskController.apply(appContext)
                    }

                    AgentForegroundService.start(appContext)

                    synchronize()
                }

                is RegistrationResult.Failure -> {

                    _uiState.value =
                        _uiState.value.copy(
                            registering = false,
                            error = result.message
                        )
                }
            }
        }
    }

    /*
     * ========================================================
     * MANUAL TITANMDM SYNCHRONIZATION
     * ========================================================
     *
     * Esta operación ejecuta:
     *
     * 1. Heartbeat.
     * 2. Consulta de comandos.
     * 3. Delivered.
     * 4. Executing.
     * 5. Handler local.
     * 6. Success / Failed.
     *
     * De esta manera el botón "Sincronizar con TitanMDM"
     * permite ejecutar comandos inmediatamente sin esperar
     * el intervalo periódico de WorkManager.
     * ========================================================
     */

    fun synchronize() {

        if (_uiState.value.synchronizing) {
            return
        }

        if (!_uiState.value.enrolled) {

            _uiState.value =
                _uiState.value.copy(
                    error =
                        "El dispositivo todavía no está inscrito."
                )

            return
        }

        viewModelScope.launch {

            _uiState.value =
                _uiState.value.copy(
                    synchronizing = true,
                    error = null,
                    message = null
                )

            /*
             * ------------------------------------------------
             * STEP 1
             * HEARTBEAT
             * ------------------------------------------------
             */

            when (
                val heartbeatResult =
                    heartbeatRepository.send()
            ) {

                HeartbeatResult.Success -> {

                    _uiState.value =
                        _uiState.value.copy(
                            lastHeartbeatStatus =
                                "Sincronizado correctamente",

                            deviceAlias =
                                DeviceAliasStore.alias(appContext)
                        )
                }

                HeartbeatResult.NotEnrolled -> {

                    _uiState.value =
                        _uiState.value.copy(
                            synchronizing = false,

                            enrolled = false,

                            identity = null,

                            lastHeartbeatStatus =
                                "Dispositivo no inscrito",

                            lastCommandStatus =
                                "No disponible",

                            error =
                                "TitanMDM no reconoce la inscripción del dispositivo."
                        )

                    return@launch
                }

                is HeartbeatResult.Failure -> {

                    _uiState.value =
                        _uiState.value.copy(
                            synchronizing = false,

                            lastHeartbeatStatus =
                                "Error de sincronización",

                            error =
                                heartbeatResult.message
                        )

                    return@launch
                }
            }

            /*
             * ------------------------------------------------
             * STEP 2
             * COMMAND ENGINE
             * ------------------------------------------------
             */

            when (
                val commandResult =
                    commandRepository
                        .pollAndExecute()
            ) {

                is CommandPollingResult.Success -> {

                    val processed =
                        commandResult
                            .processedCommands

                    _uiState.value =
                        _uiState.value.copy(
                            synchronizing = false,

                            lastCommandStatus =
                                if (processed > 0) {
                                    "$processed comando(s) procesado(s)"
                                } else {
                                    "Sin comandos pendientes"
                                },

                            lastProcessedCommands =
                                processed,

                            message =
                                if (processed > 0) {

                                    "TitanMDM sincronizado. " +
                                            "$processed comando(s) procesado(s)."

                                } else {

                                    "TitanMDM sincronizado correctamente. " +
                                            "No hay comandos pendientes."
                                },

                            error = null
                        )
                }

                CommandPollingResult.NotEnrolled -> {

                    _uiState.value =
                        _uiState.value.copy(
                            synchronizing = false,

                            enrolled = false,

                            identity = null,

                            lastCommandStatus =
                                "Dispositivo no inscrito",

                            error =
                                "No fue posible consultar comandos porque el dispositivo no está inscrito."
                        )
                }

                is CommandPollingResult.Failure -> {

                    _uiState.value =
                        _uiState.value.copy(
                            synchronizing = false,

                            lastCommandStatus =
                                "Error consultando comandos",

                            error =
                                commandResult.message
                        )
                }
            }
        }
    }

    /*
     * ========================================================
     * BACKWARD COMPATIBILITY
     * ========================================================
     *
     * AgentScreen actualmente puede estar llamando
     * sendHeartbeat().
     *
     * Conservamos esta función para no romper la UI existente.
     *
     * Desde K3 una sincronización manual significa:
     *
     * Heartbeat + Command Engine
     * ========================================================
     */

    fun sendHeartbeat() {
        synchronize()
    }

    /*
     * ========================================================
     * REFRESH LOCAL STATE
     * ========================================================
     */

    fun refresh() {
        loadState()
    }
}