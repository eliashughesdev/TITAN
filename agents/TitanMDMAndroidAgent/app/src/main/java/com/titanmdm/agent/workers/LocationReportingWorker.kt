package com.titanmdm.agent.workers

import android.content.Context
import androidx.work.CoroutineWorker
import androidx.work.WorkerParameters
import com.titanmdm.agent.location.LocationReportOutcome
import com.titanmdm.agent.location.LocationReporter

/*
 * ANDROID-GEO-01
 *
 * Respaldo periodico de WorkManager. La captura y el envio viven en
 * [LocationReporter], que tambien usa el servicio en primer plano: el
 * muestreo no depende de que WorkManager llegue a ejecutarse.
 */
class LocationReportingWorker(
    appContext: Context,
    workerParams: WorkerParameters
) : CoroutineWorker(
    appContext,
    workerParams
) {

    override suspend fun doWork(): Result =
        when (LocationReporter.report(applicationContext)) {
            LocationReportOutcome.Retry -> Result.retry()

            LocationReportOutcome.NotEnrolled,
            LocationReportOutcome.Skipped,
            LocationReportOutcome.Reported,
            LocationReportOutcome.StatusReported -> Result.success()
        }
}
