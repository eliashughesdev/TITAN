package com.titanmdm.agent

import android.os.Bundle
import androidx.activity.ComponentActivity
import androidx.activity.compose.setContent
import androidx.activity.enableEdgeToEdge
import com.titanmdm.agent.ui.TitanAgentScreen
import com.titanmdm.agent.ui.theme.TitanMDMAndroidAgentTheme

class MainActivity : ComponentActivity() {

    override fun onCreate(
        savedInstanceState: Bundle?
    ) {
        super.onCreate(savedInstanceState)

        enableEdgeToEdge()

        setContent {

            TitanMDMAndroidAgentTheme {

                TitanAgentScreen()
            }
        }
    }
}