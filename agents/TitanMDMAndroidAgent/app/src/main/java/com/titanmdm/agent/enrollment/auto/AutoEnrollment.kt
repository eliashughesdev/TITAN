package com.titanmdm.agent.enrollment.auto

import android.content.Context
import android.util.Log
import androidx.work.BackoffPolicy
import androidx.work.Constraints
import androidx.work.CoroutineWorker
import androidx.work.ExistingWorkPolicy
import androidx.work.NetworkType
import androidx.work.OneTimeWorkRequestBuilder
import androidx.work.WorkManager
import androidx.work.WorkerParameters
import com.titanmdm.agent.core.config.AgentConfig
import com.titanmdm.agent.core.config.ServerEndpointStore
import com.titanmdm.agent.core.storage.AgentIdentityStore
import com.titanmdm.agent.enrollment.registration.RegistrationRepository
import com.titanmdm.agent.enrollment.registration.RegistrationResult
import com.titanmdm.agent.kiosk.KioskController
import com.titanmdm.agent.service.AgentForegroundService
import com.titanmdm.agent.workers.AgentWorkScheduler
import java.util.concurrent.TimeUnit

/**
 * Inscripción sin intervención: al terminar el aprovisionamiento por QR
 * el agente guarda servidor y token y este trabajo los registra en cuanto
 * hay red, reintentando con espera exponencial (el Wi-Fi del QR puede
 * tardar en conectar o el servidor estar caído). Sobrevive a reinicios.
 */
object AutoEnrollment {

    private const val TAG = "TitanAutoEnroll"
    private const val UNIQUE_WORK = "titan-auto-enrollment"

    /** Guarda los datos del QR y lanza el registro. Devuelve false si faltan datos. */
    fun accept(context: Context, config: ProvisioningConfig): Boolean {
        val app = context.applicationContext
        ServerEndpointStore.init(app)

        val server = config.serverUrl
            ?.let { AgentConfig.validateServerUrl(it).getOrNull() }
            ?: return false
        ServerEndpointStore.save(server)

        val token = config.enrollmentToken ?: return false
        PendingEnrollmentStore(app).save(server, token)
        enqueue(app)
        return true
    }

    /** Arranque / reinicio: reanuda una inscripción pendiente. */
    fun resumeIfPending(context: Context) {
        val pending = PendingEnrollmentStore(context).get() ?: return
        if (!pending.failedPermanently) enqueue(context)
    }

    fun enqueue(context: Context) {
        val request = OneTimeWorkRequestBuilder<AutoEnrollmentWorker>()
            .setConstraints(
                Constraints.Builder()
                    .setRequiredNetworkType(NetworkType.CONNECTED)
                    .build()
            )
            .setBackoffCriteria(BackoffPolicy.EXPONENTIAL, 30, TimeUnit.SECONDS)
            .build()

        WorkManager.getInstance(context.applicationContext)
            .enqueueUniqueWork(UNIQUE_WORK, ExistingWorkPolicy.KEEP, request)
    }

    /** Resultado del intento, separado del Worker para poder probarlo. */
    enum class Outcome { Enrolled, Retry, Stop }

    /** 400/401/409: token inválido, vencido, agotado o datos rechazados. */
    fun isPermanent(httpCode: Int?): Boolean = httpCode in setOf(400, 401, 403, 409)

    suspend fun attempt(context: Context): Outcome {
        val app = context.applicationContext
        val store = PendingEnrollmentStore(app)
        ServerEndpointStore.init(app)

        if (AgentIdentityStore(app).isEnrolled()) {
            store.clear()
            return Outcome.Enrolled
        }

        val pending = store.get() ?: return Outcome.Stop

        return when (val result = RegistrationRepository(app).register(pending.token, pending.serverUrl)) {
            is RegistrationResult.Success -> {
                store.clear()
                Log.i(TAG, "Equipo inscrito automáticamente: ${result.identity.deviceId}")
                runCatching { KioskController.apply(app) }
                AgentForegroundService.start(app)
                AgentWorkScheduler.scheduleAndSyncNow(app)
                Outcome.Enrolled
            }

            is RegistrationResult.Failure -> {
                val permanent = isPermanent(result.httpCode)
                store.recordFailure(result.message, permanent)
                Log.w(TAG, "Inscripción automática fallida (permanente=$permanent): ${result.message}")
                if (permanent) Outcome.Stop else Outcome.Retry
            }
        }
    }
}

class AutoEnrollmentWorker(
    context: Context,
    params: WorkerParameters
) : CoroutineWorker(context, params) {

    override suspend fun doWork(): Result =
        when (AutoEnrollment.attempt(applicationContext)) {
            AutoEnrollment.Outcome.Enrolled -> Result.success()
            AutoEnrollment.Outcome.Retry -> Result.retry()
            AutoEnrollment.Outcome.Stop -> Result.failure()
        }
}
