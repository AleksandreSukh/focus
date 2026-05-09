package com.systemssanity.focus.domain.appupdates

import kotlinx.serialization.json.Json
import kotlinx.serialization.json.JsonObject
import kotlinx.serialization.json.contentOrNull
import kotlinx.serialization.json.jsonObject
import kotlinx.serialization.json.jsonPrimitive
import java.net.URI

data class AppUpdateManifest(
    val versionCode: Long,
    val versionName: String = "",
    val url: String = "",
    val message: String = "",
)

object AppUpdates {
    const val CheckIntervalMillis: Long = 10 * 60 * 1000
    const val DefaultUpdateMessage: String = "A new version of Focus is available."
    const val OpenUpdateLabel: String = "Open update"

    private val json = Json { ignoreUnknownKeys = true }

    fun manifestChecksEnabled(manifestUrl: String): Boolean =
        manifestUrl.trim().isNotEmpty()

    fun parseManifest(rawJson: String): Result<AppUpdateManifest> =
        runCatching {
            val obj = json.parseToJsonElement(rawJson).jsonObject
            val versionCode = obj.string("versionCode").toLongOrNull()
                ?: obj.string("version").toLongOrNull()
                ?: error("Update manifest must include a numeric versionCode.")
            AppUpdateManifest(
                versionCode = versionCode,
                versionName = obj.string("versionName"),
                url = obj.string("url").ifBlank { obj.string("updateUrl") },
                message = obj.string("message"),
            )
        }

    fun isNewerVersion(manifest: AppUpdateManifest, currentVersionCode: Long): Boolean =
        manifest.versionCode > currentVersionCode

    fun validUpdateUrl(url: String): Boolean =
        runCatching {
            val uri = URI(url.trim())
            (uri.scheme.equals("http", ignoreCase = true) ||
                uri.scheme.equals("https", ignoreCase = true)) &&
                !uri.host.isNullOrBlank()
        }.getOrDefault(false)

    fun actionableUpdateAvailable(manifest: AppUpdateManifest, currentVersionCode: Long): Boolean =
        isNewerVersion(manifest, currentVersionCode) && validUpdateUrl(manifest.url)

    fun bannerMessage(manifest: AppUpdateManifest): String =
        manifest.message.trim().ifBlank { DefaultUpdateMessage }

    fun shouldCheckForUpdate(
        force: Boolean,
        checking: Boolean,
        lastCheckedAtMillis: Long,
        nowMillis: Long,
    ): Boolean {
        if (checking) return false
        if (force) return true
        return lastCheckedAtMillis <= 0 ||
            nowMillis - lastCheckedAtMillis >= CheckIntervalMillis
    }

    private fun JsonObject.string(name: String): String =
        this[name]?.jsonPrimitive?.contentOrNull?.trim().orEmpty()
}
