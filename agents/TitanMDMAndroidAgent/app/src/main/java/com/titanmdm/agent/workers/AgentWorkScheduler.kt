package com.titanmdm.agent.workers

import android.content.Context
import androidx.work.BackoffPolicy
import androidx.work.Constraints
import androidx.work.ExistingPeriodicWorkPolicy
import androidx.work.ExistingWorkPolicy
import androidx.work.NetworkType
import androidx.work.OneTimeWorkRequestBuilder
import androidx.work.PeriodicWorkRequestBuilder
import androidx.work.WorkManager
import com.titanmdm.agent.core.config.AgentConfig
import java.util.concurrent.TimeUnit

object AgentWorkScheduler {

    private const val IMMEDIATE_HEARTBEAT_WORK_NAME =
        "titanmdm-immediate-heartbeat"

    private const val IMMEDIATE_COMMAND_WORK_NAME =
        "titanmdm-immediate-command-sync"

    fun schedule(
        context: Context
    ) {
        val appContext =
            context.applicationContext

        scheduleHeartbeat(
            appContext
        )

        scheduleCommands(
            appContext
        )
    }

    fun scheduleAndSyncNow(
        context: Context
    ) {
        val appContext =
            context.applicationContext

        schedule(
            appContext
        )

        syncNow(
            appContext
        )
    }

    fun syncNow(
        context: Context
    ) {
        val appContext =
            context.applicationContext

        enqueueImmediateHeartbeat(
            appContext
        )

        enqueueImmediateCommandSync(
            appContext
        )
    }

    private fun networkConstraints():
            Constraints {
        return Constraints.Builder()
            .setRequiredNetworkType(
                NetworkType.CONNECTED
            )
            .build()
    }

    private fun scheduleHeartbeat(
        context: Context
    ) {
        val request =
            PeriodicWorkRequestBuilder<
                    HeartbeatWorker
                    >(
                AgentConfig
                    .HEARTBEAT_INTERVAL_MINUTES,
                TimeUnit.MINUTES
            )
                .setConstraints(
                    networkConstraints()
                )
                .setBackoffCriteria(
                    BackoffPolicy.EXPONENTIAL,
                    30,
                    TimeUnit.SECONDS
                )
                .addTag(
                    AgentConfig
                        .HEARTBEAT_WORK_NAME
                )
                .build()

        WorkManager
            .getInstance(context)
            .enqueueUniquePeriodicWork(
                AgentConfig
                    .HEARTBEAT_WORK_NAME,
                ExistingPeriodicWorkPolicy.UPDATE,
                request
            )
    }

    private fun scheduleCommands(
        context: Context
    ) {
        val request =
            PeriodicWorkRequestBuilder<
                    CommandWorker
                    >(
                AgentConfig
                    .COMMAND_POLL_INTERVAL_MINUTES,
                TimeUnit.MINUTES
            )
                .setConstraints(
                    networkConstraints()
                )
                .setBackoffCriteria(
                    BackoffPolicy.EXPONENTIAL,
                    30,
                    TimeUnit.SECONDS
                )
                .addTag(
                    AgentConfig
                        .COMMAND_WORK_NAME
                )
                .build()

        WorkManager
            .getInstance(context)
            .enqueueUniquePeriodicWork(
                AgentConfig
                    .COMMAND_WORK_NAME,
                ExistingPeriodicWorkPolicy.UPDATE,
                request
            )
    }

    private fun enqueueImmediateHeartbeat(
        context: Context
    ) {
        val request =
            OneTimeWorkRequestBuilder<
                    HeartbeatWorker
                    >()
                .setConstraints(
                    networkConstraints()
                )
                .setBackoffCriteria(
                    BackoffPolicy.EXPONENTIAL,
                    15,
                    TimeUnit.SECONDS
                )
                .addTag(
                    IMMEDIATE_HEARTBEAT_WORK_NAME
                )
                .build()

        WorkManager
            .getInstance(context)
            .enqueueUniqueWork(
                IMMEDIATE_HEARTBEAT_WORK_NAME,
                ExistingWorkPolicy.REPLACE,
                request
            )
    }

    private fun enqueueImmediateCommandSync(
        context: Context
    ) {
        val request =
            OneTimeWorkRequestBuilder<
                    CommandWorker
                    >()
                .setConstraints(
                    networkConstraints()
                )
                .setBackoffCriteria(
                    BackoffPolicy.EXPONENTIAL,
                    15,
                    TimeUnit.SECONDS
                )
                .addTag(
                    IMMEDIATE_COMMAND_WORK_NAME
                )
                .build()

        WorkManager
            .getInstance(context)
            .enqueueUniqueWork(
                IMMEDIATE_COMMAND_WORK_NAME,
                ExistingWorkPolicy.REPLACE,
                request
            )
    }
}