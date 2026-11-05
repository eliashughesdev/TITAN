package com.titanmdm.agent

import android.app.Application
import com.titanmdm.agent.core.config.ServerEndpointStore
import com.titanmdm.agent.workers.AgentWorkScheduler

class TitanMdmApplication : Application() {

    override fun onCreate() {
        super.onCreate()

        ServerEndpointStore.init(this)

        AgentWorkScheduler.schedule(this)
    }
}