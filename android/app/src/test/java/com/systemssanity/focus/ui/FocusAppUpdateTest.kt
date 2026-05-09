package com.systemssanity.focus.ui

import com.systemssanity.focus.domain.appupdates.AppUpdateManifest
import com.systemssanity.focus.domain.appupdates.AppUpdates
import kotlin.test.Test
import kotlin.test.assertEquals
import kotlin.test.assertFalse
import kotlin.test.assertTrue

class FocusAppUpdateTest {
    @Test
    fun appUpdateStateShowsOnlyActionableNewerUpdates() {
        val available = appUpdateStateFromManifest(
            manifest = AppUpdateManifest(
                versionCode = 2,
                versionName = "0.2.0",
                url = "https://example.test/focus.apk",
                message = "Install the new build.",
            ),
            currentVersionCode = 1,
            checkedAtMillis = 123,
        )
        val invalidUrl = appUpdateStateFromManifest(
            manifest = AppUpdateManifest(versionCode = 2, url = "focus.apk"),
            currentVersionCode = 1,
            checkedAtMillis = 456,
        )
        val sameVersion = appUpdateStateFromManifest(
            manifest = AppUpdateManifest(versionCode = 1, url = "https://example.test/focus.apk"),
            currentVersionCode = 1,
            checkedAtMillis = 789,
        )

        assertTrue(available.available)
        assertEquals("Install the new build.", available.message)
        assertEquals("0.2.0", available.versionName)
        assertEquals("https://example.test/focus.apk", available.updateUrl)
        assertEquals(123, available.lastCheckedAtMillis)
        assertFalse(invalidUrl.available)
        assertEquals(456, invalidUrl.lastCheckedAtMillis)
        assertFalse(sameVersion.available)
        assertEquals(789, sameVersion.lastCheckedAtMillis)
    }

    @Test
    fun bannerHelpersUseStableFallbacksAndLabels() {
        val state = AppUpdateUiState(
            available = true,
            updateUrl = "https://example.test/release",
            message = " ",
        )

        assertTrue(appUpdateBannerVisible(state))
        assertFalse(appUpdateBannerVisible(state.copy(updateUrl = "")))
        assertEquals(AppUpdates.DefaultUpdateMessage, appUpdateBannerMessage(state))
        assertEquals(AppUpdates.OpenUpdateLabel, appUpdateOpenActionLabel(state))
        assertEquals("Could not open update link.", appUpdateOpenFailedMessage())
    }
}
