package com.systemssanity.focus.data.local

import android.content.Context
import androidx.security.crypto.EncryptedSharedPreferences
import androidx.security.crypto.MasterKey

interface TokenStore {
    fun saveToken(scope: String, token: String)
    fun getToken(scope: String): String?
    fun clearToken(scope: String)
}

class SecureTokenStore(context: Context) : TokenStore {
    private val preferences = EncryptedSharedPreferences.create(
        context,
        "focus_secure_tokens",
        MasterKey.Builder(context)
            .setKeyScheme(MasterKey.KeyScheme.AES256_GCM)
            .build(),
        EncryptedSharedPreferences.PrefKeyEncryptionScheme.AES256_SIV,
        EncryptedSharedPreferences.PrefValueEncryptionScheme.AES256_GCM,
    )

    override fun saveToken(scope: String, token: String) {
        preferences.edit().putString(scope.key(), token.trim()).apply()
    }

    override fun getToken(scope: String): String? =
        preferences.getString(scope.key(), null)?.trim()?.takeIf { it.isNotEmpty() }

    override fun clearToken(scope: String) {
        preferences.edit().remove(scope.key()).apply()
    }

    private fun String.key(): String = "token:${lowercase()}"
}
