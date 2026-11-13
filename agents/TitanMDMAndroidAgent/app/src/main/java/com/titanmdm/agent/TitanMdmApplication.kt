package com.titanmdm.agent

import android.app.Application
import com.titanmdm.agent.core.config.ServerEndpointStore
import com.titanmdm.agent.enrollment.auto.AutoEnrollment
import com.titanmdm.agent.enrollment.auto.LocalProvisionFile
import com.titanmdm.agent.workers.AgentWorkScheduler

class TitanMdmApplication : Application() {

    override fun onCreate() {
        super.onCreate()

        ServerEndpointStore.init(this)

        LocalProvisionFile.consumeIfPresent(this)
        AgentWorkScheduler.schedule(this)
        AutoEnrollment.resumeIfPending(this)
    }
}