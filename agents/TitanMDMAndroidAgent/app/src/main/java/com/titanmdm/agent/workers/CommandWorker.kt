package com.titanmdm.agent.workers

import android.content.Context
import androidx.work.CoroutineWorker
import androidx.work.WorkerParameters
import com.titanmdm.agent.commands.CommandPollingResult
import com.titanmdm.agent.commands.CommandRepository

class CommandWorker(
    appContext: Context,
    workerParams: WorkerParameters
) : CoroutineWorker(
    appContext,
    workerParams
) {

    override suspend fun doWork(): Result {

        return when (
            val result =
                CommandRepository(
                    applicationContext
                ).pollAndExecute()
        ) {

            is CommandPollingResult.Success ->
                Result.success()

            CommandPollingResult.NotEnrolled ->
                Result.success()

            is CommandPollingResult.Failure -> {

                if (result.retryable) {
                    Result.retry()
                } else {
                    Result.failure()
                }
            }
        }
    }
}