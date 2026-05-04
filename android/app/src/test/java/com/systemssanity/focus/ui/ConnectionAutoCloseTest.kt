package com.systemssanity.focus.ui

import com.systemssanity.focus.data.local.PendingMapOperation
import com.systemssanity.focus.domain.maps.AttachmentViewerKind
import com.systemssanity.focus.domain.maps.MapMutation
import com.systemssanity.focus.domain.maps.UnreadableMapEntry
import com.systemssanity.focus.domain.maps.UnreadableMapReason
import com.systemssanity.focus.domain.maps.UnreadableMaps
import kotlin.test.Test
import kotlin.test.assertEquals
import kotlin.test.assertFalse
import kotlin.test.assertNull
import kotlin.test.assertTrue

class ConnectionAutoCloseTest {
    @Test
    fun workspaceLoadedMessageIncludesUnreadableCount() {
        assertEquals("Loaded 2 maps.", workspaceLoadedMessage(mapCount = 2, unreadableCount = 0))
        assertEquals("Loaded 1 map. 1 map needs repair.", workspaceLoadedMessage(mapCount = 1, unreadableCount = 1))
        assertEquals("Loaded 0 maps. 2 maps need repair.", workspaceLoadedMessage(mapCount = 0, unreadableCount = 2))
    }

    @Test
    fun unreadablePendingCountsMatchOperationsThatTouchFilePath() {
        val unreadable = listOf(unreadableEntry("FocusMaps/Broken.json"))
        val pending = listOf(
            PendingMapOperation(
                id = "edit",
                scope = "scope",
                operation = MapMutation.EditNodeText(
                    filePath = "FocusMaps/Broken.json",
                    nodeId = "node",
                    text = "Edited",
                    timestamp = "2026-05-04T10:00:00Z",
                    commitMessage = "map:edit Broken node",
                ),
                enqueuedAtMillis = 1,
            ),
            PendingMapOperation(
                id = "other",
                scope = "scope",
                operation = MapMutation.EditNodeText(
                    filePath = "FocusMaps/Other.json",
                    nodeId = "node",
                    text = "Edited",
                    timestamp = "2026-05-04T10:00:00Z",
                    commitMessage = "map:edit Other node",
                ),
                enqueuedAtMillis = 2,
            ),
        )

        assertEquals(mapOf("FocusMaps/Broken.json" to 1), unreadablePendingCounts(unreadable, pending))
        assertTrue(hasPendingOperationForUnreadableMap(unreadable, pending))
        assertFalse(hasPendingOperationForUnreadableMap(unreadable, pending.drop(1)))
    }

    @Test
    fun unreadableMapViewerStateUsesLoadedTextViewer() {
        val entry = unreadableEntry("FocusMaps/Broken.json").copy(rawText = "{\"rootNode\":")

        val state = unreadableMapViewerState(entry)

        assertEquals(false, state.loading)
        assertEquals(AttachmentViewerKind.Text, state.request?.kind)
        assertEquals("", state.request?.nodeId)
        assertEquals("Broken.json", state.request?.attachment?.displayName)
        assertEquals(UnreadableMaps.RawMapMediaType, state.mediaType)
        assertEquals("{\"rootNode\":", state.bytes?.toString(Charsets.UTF_8))
    }

    @Test
    fun initialScreenWaitsForConnectionState() {
        assertNull(
            resolveInitialAppScreen(
                connectionStateLoaded = false,
                repoConfigured = true,
                tokenPresent = true,
            ),
        )
    }

    @Test
    fun initialScreenUsesMapsWhenConnectionIsConfigured() {
        assertEquals(
            AppScreen.Maps,
            resolveInitialAppScreen(
                connectionStateLoaded = true,
                repoConfigured = true,
                tokenPresent = true,
            ),
        )
    }

    @Test
    fun initialScreenUsesConnectionWhenConnectionIsIncomplete() {
        assertEquals(
            AppScreen.Connection,
            resolveInitialAppScreen(
                connectionStateLoaded = true,
                repoConfigured = false,
                tokenPresent = true,
            ),
        )
        assertEquals(
            AppScreen.Connection,
            resolveInitialAppScreen(
                connectionStateLoaded = true,
                repoConfigured = true,
                tokenPresent = false,
            ),
        )
    }

    @Test
    fun closesOnlyAfterPendingSuccessfulWorkspaceLoadResult() {
        assertFalse(
            shouldCloseConnectionAfterWorkspaceLoad(
                closeConnectionAfterLoad = false,
                workspaceLoadResultVersion = 1,
                workspaceLoadSucceeded = true,
            ),
        )
        assertFalse(
            shouldCloseConnectionAfterWorkspaceLoad(
                closeConnectionAfterLoad = true,
                workspaceLoadResultVersion = 0,
                workspaceLoadSucceeded = true,
            ),
        )
        assertFalse(
            shouldCloseConnectionAfterWorkspaceLoad(
                closeConnectionAfterLoad = true,
                workspaceLoadResultVersion = 1,
                workspaceLoadSucceeded = false,
            ),
        )
        assertTrue(
            shouldCloseConnectionAfterWorkspaceLoad(
                closeConnectionAfterLoad = true,
                workspaceLoadResultVersion = 1,
                workspaceLoadSucceeded = true,
            ),
        )
    }

    private fun unreadableEntry(filePath: String): UnreadableMapEntry =
        UnreadableMapEntry(
            filePath = filePath,
            fileName = filePath.substringAfterLast('/'),
            mapName = filePath.substringAfterLast('/').removeSuffix(".json"),
            revision = "rev",
            reason = UnreadableMapReason.InvalidJson,
            message = "Broken",
            rawText = "{}",
        )
}
