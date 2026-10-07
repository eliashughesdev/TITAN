package com.titanmdm.agent

import android.app.Application
import com.titanmdm.agent.workers.AgentWorkScheduler

class TitanMdmApplication : Application() {

    override fun onCreate() {
        super.onCreate()

        AgentWorkScheduler.schedule(this)
    }
}