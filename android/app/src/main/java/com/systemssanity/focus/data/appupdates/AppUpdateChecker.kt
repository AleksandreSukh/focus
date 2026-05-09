package com.systemssanity.focus.data.appupdates

import com.systemssanity.focus.domain.appupdates.AppUpdateManifest
import com.systemssanity.focus.domain.appupdates.AppUpdates
import kotlinx.coroutines.Dispatchers
import kotlinx.coroutines.withContext
import okhttp3.OkHttpClient
import okhttp3.Request

class AppUpdateChecker(
    private val manifestUrl: String,
    private val httpClient: OkHttpClient = OkHttpClient(),
) {
    suspend fun fetchManifest(): Result<AppUpdateManifest> =
        withContext(Dispatchers.IO) {
            runCatching {
                if (!AppUpdates.manifestChecksEnabled(manifestUrl)) {
                    error("App update checks are not configured.")
                }
                val request = Request.Builder()
                    .url(manifestUrl)
                    .header("Cache-Control", "no-cache")
                    .header("Pragma", "no-cache")
                    .get()
                    .build()
                httpClient.newCall(request).execute().use { response ->
                    if (!response.isSuccessful) {
                        error("App update check failed (HTTP ${response.code}).")
                    }
                    AppUpdates.parseManifest(response.body?.string().orEmpty()).getOrThrow()
                }
            }
        }
}
