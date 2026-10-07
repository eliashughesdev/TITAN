package com.titanmdm.agent.workers

import android.content.Context
import androidx.work.CoroutineWorker
import androidx.work.WorkerParameters
import com.titanmdm.agent.heartbeat.HeartbeatRepository
import com.titanmdm.agent.heartbeat.HeartbeatResult

class HeartbeatWorker(
    appContext: Context,
    workerParams: WorkerParameters
) : CoroutineWorker(
    appContext,
    workerParams
) {

    override suspend fun doWork(): Result {

        return when (
            val result =
                HeartbeatRepository(
                    applicationContext
                ).send()
        ) {

            HeartbeatResult.Success ->
                Result.success()

            HeartbeatResult.NotEnrolled ->
                Result.success()

            is HeartbeatResult.Failure -> {

                if (result.retryable) {
                    Result.retry()
                } else {
                    Result.failure()
                }
            }
        }
    }
}