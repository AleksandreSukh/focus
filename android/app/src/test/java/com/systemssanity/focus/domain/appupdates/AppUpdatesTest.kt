package com.systemssanity.focus.domain.appupdates

import kotlin.test.Test
import kotlin.test.assertEquals
import kotlin.test.assertFalse
import kotlin.test.assertTrue

class AppUpdatesTest {
    @Test
    fun parsesVersionCodeManifest() {
        val manifest = AppUpdates.parseManifest(
            """
            {
              "versionCode": 12,
              "versionName": "1.2.0",
              "url": "https://example.test/focus.apk",
              "message": "New build ready."
            }
            """.trimIndent(),
        ).getOrThrow()

        assertEquals(12, manifest.versionCode)
        assertEquals("1.2.0", manifest.versionName)
        assertEquals("https://example.test/focus.apk", manifest.url)
        assertEquals("New build ready.", manifest.message)
    }

    @Test
    fun parsesPwaStyleVersionFallback() {
        val manifest = AppUpdates.parseManifest("""{"version": 6, "url": "https://example.test"}""").getOrThrow()

        assertEquals(6, manifest.versionCode)
    }

    @Test
    fun rejectsMalformedManifests() {
        assertTrue(AppUpdates.parseManifest("not json").isFailure)
        assertTrue(AppUpdates.parseManifest("""{"url": "https://example.test"}""").isFailure)
    }

    @Test
    fun newerVersionRequiresActionableHttpUrl() {
        val newer = AppUpdateManifest(versionCode = 2, url = "https://example.test/release")
        val same = newer.copy(versionCode = 1)
        val invalidUrl = newer.copy(url = "ftp://example.test/release")
        val missingHost = newer.copy(url = "https:release")

        assertTrue(AppUpdates.actionableUpdateAvailable(newer, currentVersionCode = 1))
        assertFalse(AppUpdates.actionableUpdateAvailable(same, currentVersionCode = 1))
        assertFalse(AppUpdates.actionableUpdateAvailable(invalidUrl, currentVersionCode = 1))
        assertFalse(AppUpdates.actionableUpdateAvailable(missingHost, currentVersionCode = 1))
    }

    @Test
    fun blankManifestUrlDisablesChecksAndThrottleHonorsInterval() {
        assertFalse(AppUpdates.manifestChecksEnabled(" "))
        assertTrue(AppUpdates.manifestChecksEnabled("https://example.test/version.json"))

        assertTrue(
            AppUpdates.shouldCheckForUpdate(
                force = true,
                checking = false,
                lastCheckedAtMillis = 100,
                nowMillis = 101,
            ),
        )
        assertFalse(
            AppUpdates.shouldCheckForUpdate(
                force = false,
                checking = true,
                lastCheckedAtMillis = 0,
                nowMillis = 0,
            ),
        )
        assertFalse(
            AppUpdates.shouldCheckForUpdate(
                force = false,
                checking = false,
                lastCheckedAtMillis = 1_000,
                nowMillis = 1_000 + AppUpdates.CheckIntervalMillis - 1,
            ),
        )
        assertTrue(
            AppUpdates.shouldCheckForUpdate(
                force = false,
                checking = false,
                lastCheckedAtMillis = 1_000,
                nowMillis = 1_000 + AppUpdates.CheckIntervalMillis,
            ),
        )
    }

    @Test
    fun bannerMessageFallsBackToDefault() {
        assertEquals(
            AppUpdates.DefaultUpdateMessage,
            AppUpdates.bannerMessage(AppUpdateManifest(versionCode = 2, url = "https://example.test", message = " ")),
        )
        assertEquals(
            "Update ready.",
            AppUpdates.bannerMessage(AppUpdateManifest(versionCode = 2, url = "https://example.test", message = "Update ready.")),
        )
    }
}
