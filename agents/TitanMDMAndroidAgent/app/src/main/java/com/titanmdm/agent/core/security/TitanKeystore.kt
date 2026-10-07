package com.titanmdm.agent.core.security

import android.security.keystore.KeyGenParameterSpec
import android.security.keystore.KeyProperties
import android.util.Base64
import java.security.KeyStore
import javax.crypto.Cipher
import javax.crypto.KeyGenerator
import javax.crypto.SecretKey
import javax.crypto.spec.GCMParameterSpec

object TitanKeystore {

    private const val ANDROID_KEYSTORE = "AndroidKeyStore"
    private const val KEY_ALIAS = "titanmdm_agent_identity_key"
    private const val TRANSFORMATION = "AES/GCM/NoPadding"
    private const val IV_LENGTH = 12
    private const val TAG_LENGTH_BITS = 128

    private val keyStore: KeyStore
        get() = KeyStore.getInstance(ANDROID_KEYSTORE).apply {
            load(null)
        }

    private fun getOrCreateKey(): SecretKey {
        val existingKey = keyStore.getKey(KEY_ALIAS, null)

        if (existingKey is SecretKey) {
            return existingKey
        }

        val keyGenerator = KeyGenerator.getInstance(
            KeyProperties.KEY_ALGORITHM_AES,
            ANDROID_KEYSTORE
        )

        val spec = KeyGenParameterSpec.Builder(
            KEY_ALIAS,
            KeyProperties.PURPOSE_ENCRYPT or
                    KeyProperties.PURPOSE_DECRYPT
        )
            .setBlockModes(KeyProperties.BLOCK_MODE_GCM)
            .setEncryptionPaddings(
                KeyProperties.ENCRYPTION_PADDING_NONE
            )
            .setKeySize(256)
            .setRandomizedEncryptionRequired(true)
            .build()

        keyGenerator.init(spec)

        return keyGenerator.generateKey()
    }

    fun encrypt(value: String): String {
        require(value.isNotEmpty()) {
            "The value to encrypt cannot be empty."
        }

        val cipher = Cipher.getInstance(TRANSFORMATION)

        cipher.init(
            Cipher.ENCRYPT_MODE,
            getOrCreateKey()
        )

        val encrypted = cipher.doFinal(
            value.toByteArray(Charsets.UTF_8)
        )

        val payload = ByteArray(
            cipher.iv.size + encrypted.size
        )

        System.arraycopy(
            cipher.iv,
            0,
            payload,
            0,
            cipher.iv.size
        )

        System.arraycopy(
            encrypted,
            0,
            payload,
            cipher.iv.size,
            encrypted.size
        )

        return Base64.encodeToString(
            payload,
            Base64.NO_WRAP
        )
    }

    fun decrypt(value: String): String {
        require(value.isNotEmpty()) {
            "The encrypted value cannot be empty."
        }

        val payload = Base64.decode(
            value,
            Base64.NO_WRAP
        )

        require(payload.size > IV_LENGTH) {
            "Invalid encrypted payload."
        }

        val iv = payload.copyOfRange(
            0,
            IV_LENGTH
        )

        val encrypted = payload.copyOfRange(
            IV_LENGTH,
            payload.size
        )

        val cipher = Cipher.getInstance(TRANSFORMATION)

        cipher.init(
            Cipher.DECRYPT_MODE,
            getOrCreateKey(),
            GCMParameterSpec(
                TAG_LENGTH_BITS,
                iv
            )
        )

        return cipher.doFinal(encrypted)
            .toString(Charsets.UTF_8)
    }

    fun deleteKey() {
        val store = keyStore

        if (store.containsAlias(KEY_ALIAS)) {
            store.deleteEntry(KEY_ALIAS)
        }
    }
}