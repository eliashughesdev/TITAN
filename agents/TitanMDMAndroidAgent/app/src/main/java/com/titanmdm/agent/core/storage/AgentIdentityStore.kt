package com.titanmdm.agent.core.storage

import android.content.Context
import androidx.datastore.preferences.core.booleanPreferencesKey
import androidx.datastore.preferences.core.edit
import androidx.datastore.preferences.core.stringPreferencesKey
import androidx.datastore.preferences.preferencesDataStore
import com.titanmdm.agent.core.security.TitanKeystore
import kotlinx.coroutines.flow.first

private val Context.agentDataStore by preferencesDataStore(
    name = "titanmdm_agent"
)

data class AgentIdentity(
    val deviceId: String,
    val organizationId: String,
    val deviceName: String,
    val deviceSecret: String
)

class AgentIdentityStore(
    private val context: Context
) {

    private object Keys {
        val deviceId =
            stringPreferencesKey("device_id")

        val organizationId =
            stringPreferencesKey("organization_id")

        val deviceName =
            stringPreferencesKey("device_name")

        val encryptedDeviceSecret =
            stringPreferencesKey("device_secret_encrypted")

        val enrolled =
            booleanPreferencesKey("enrolled")
    }

    suspend fun save(
        identity: AgentIdentity
    ) {
        val encryptedSecret =
            TitanKeystore.encrypt(
                identity.deviceSecret
            )

        context.agentDataStore.edit { preferences ->

            preferences[Keys.deviceId] =
                identity.deviceId

            preferences[Keys.organizationId] =
                identity.organizationId

            preferences[Keys.deviceName] =
                identity.deviceName

            preferences[Keys.encryptedDeviceSecret] =
                encryptedSecret

            preferences[Keys.enrolled] = true
        }
    }

    suspend fun get(): AgentIdentity? {
        val preferences =
            context.agentDataStore.data.first()

        if (preferences[Keys.enrolled] != true) {
            return null
        }

        val deviceId =
            preferences[Keys.deviceId]
                ?: return null

        val organizationId =
            preferences[Keys.organizationId]
                ?: return null

        val deviceName =
            preferences[Keys.deviceName]
                ?: return null

        val encryptedSecret =
            preferences[
                Keys.encryptedDeviceSecret
            ] ?: return null

        return try {
            AgentIdentity(
                deviceId = deviceId,
                organizationId = organizationId,
                deviceName = deviceName,
                deviceSecret =
                    TitanKeystore.decrypt(
                        encryptedSecret
                    )
            )
        } catch (_: Exception) {
            null
        }
    }

    suspend fun isEnrolled(): Boolean =
        get() != null

    suspend fun clear() {
        context.agentDataStore.edit {
            it.clear()
        }

        TitanKeystore.deleteKey()
    }
}