package com.systemssanity.focus.ui

import androidx.compose.ui.Alignment
import com.systemssanity.focus.data.github.GitHubAccessValidation
import com.systemssanity.focus.data.github.GitHubApiException
import com.systemssanity.focus.data.local.FabSidePreference
import com.systemssanity.focus.data.local.PendingMapOperation
import com.systemssanity.focus.data.local.RepoSettings
import com.systemssanity.focus.data.local.SyncMetadata
import com.systemssanity.focus.data.local.ThemePreference
import com.systemssanity.focus.data.local.UiPreferences
import com.systemssanity.focus.domain.maps.RelatedNodeEntry
import com.systemssanity.focus.domain.maps.AttachmentViewerKind
import com.systemssanity.focus.domain.maps.BlockedPendingMaps
import com.systemssanity.focus.domain.maps.LocalMapRepairs
import com.systemssanity.focus.domain.maps.MapMutation
import com.systemssanity.focus.domain.maps.PendingConflictResolution
import com.systemssanity.focus.domain.maps.PendingConflicts
import com.systemssanity.focus.domain.maps.UnreadableMapEntry
import com.systemssanity.focus.domain.maps.UnreadableMapReason
import com.systemssanity.focus.domain.maps.UnreadableMaps
import com.systemssanity.focus.domain.model.MapSnapshot
import com.systemssanity.focus.domain.model.MindMapDocument
import com.systemssanity.focus.domain.model.Node
import com.systemssanity.focus.domain.model.NodeAttachment
import com.systemssanity.focus.domain.model.NodeMetadata
import com.systemssanity.focus.domain.model.NodeMetadataSources
import com.systemssanity.focus.domain.model.NodeType
import com.systemssanity.focus.domain.model.TaskState
import java.time.ZoneOffset
import kotlin.test.Test
import kotlin.test.assertEquals
import kotlin.test.assertFalse
import kotlin.test.assertNull
import kotlin.test.assertTrue

class FocusedMapViewTest {
    @Test
    fun unreadableRecoveryHelpersRenderLabels() {
        val entry = UnreadableMapEntry(
            filePath = "FocusMaps/Broken.json",
            fileName = "Broken.json",
            mapName = "",
            revision = "rev",
            reason = UnreadableMapReason.InvalidJson,
            message = "Map is not valid JSON.",
            rawText = "{}",
        )

        assertEquals("Broken.json", unreadableMapTitle(entry))
        assertEquals("No queued local changes", unreadablePendingText(0))
        assertEquals("Paused 1 pending change", unreadablePendingText(1))
        assertEquals("Paused 2 pending changes", unreadablePendingText(2))
        assertEquals("View raw file Broken.json", UnreadableMaps.viewRawLabel(entry))
        assertEquals("Download raw file Broken.json", UnreadableMaps.downloadRawLabel(entry))
        assertEquals("Repair locally Broken.json", LocalMapRepairs.repairActionLabel(entry))
        assertEquals("Download repair draft Broken.json", LocalMapRepairs.downloadRepairDraftLabel(entry))
        assertEquals("Reset to GitHub Broken.json", UnreadableMaps.resetToGitHubLabel(entry))
        assertEquals("Invalid JSON was found in Broken.json. Save a repaired local copy to unblock this device.", LocalMapRepairs.helperText(entry))
        assertEquals("Reset \"Broken\" to the GitHub version.", resetStatusMessage(UnreadableMaps.resetSuccessMessage("Broken"), 0))
        assertEquals(
            "Reset \"Broken\" to the GitHub version. 2 queued local changes for this map were discarded on this device.",
            resetStatusMessage(UnreadableMaps.resetSuccessMessage("Broken"), 2),
        )
        assertEquals("\"Broken\" was deleted from GitHub.", UnreadableMaps.resetDeletedMessage("Broken"))
        assertEquals("Invalid JSON was found in Broken.json.", UnreadableMaps.resetStillUnreadableMessage(entry))
        val blocked = BlockedPendingMaps.build("FocusMaps/Broken.json", "Broken")
        assertEquals("Repair locally Broken.json", BlockedPendingMaps.repairActionLabel(blocked))
        assertEquals("Reset to GitHub Broken", BlockedPendingMaps.resetToGitHubLabel(blocked))
        assertEquals("Retry Broken", BlockedPendingMaps.retryLabel(blocked))
        assertEquals("Discard queued changes Broken", BlockedPendingMaps.discardLabel(blocked))
        assertEquals("Discarded 2 queued local changes for \"Broken\".", BlockedPendingMaps.discardStatusMessage(blocked, 2))
        assertEquals(
            "Queued changes for \"Broken\" can't be applied because the repaired map no longer contains the targeted node. Save a repaired local copy to retry the queued changes on this device.",
            BlockedPendingMaps.repairHelperText(blocked),
        )
        assertEquals(
            LocalMapRepairUiState(targetPath = "FocusMaps/Broken.json", draftText = "{}"),
            LocalMapRepairUiState(targetPath = "FocusMaps/Broken.json", draftText = "{}").forTarget("FocusMaps/Broken.json"),
        )
        assertEquals(
            LocalMapRepairUiState(),
            LocalMapRepairUiState(targetPath = "FocusMaps/Broken.json", draftText = "{}").forTarget("FocusMaps/Other.json"),
        )
    }

    @Test
    fun blockedPendingHelpersCountAndFilterQueuedOperations() {
        val blocked = BlockedPendingMaps.build("FocusMaps/Broken.json", "Broken")
        val pending = listOf(
            pendingEdit("broken", "FocusMaps/Broken.json"),
            pendingEdit("other", "FocusMaps/Other.json"),
        )

        assertEquals(mapOf("FocusMaps/Broken.json" to 1), blockedPendingCounts(listOf(blocked), pending))
        assertTrue(hasPendingOperationForBlockedMap(listOf(blocked), pending))
        assertTrue(hasPausedPendingOperation(emptyList(), listOf(blocked), emptyList(), pending))
        assertEquals(listOf(blocked), syncBlockedPendingMaps(listOf(blocked), pending))
        assertEquals(emptyList(), syncBlockedPendingMaps(listOf(blocked), pending.drop(1)))
    }

    @Test
    fun conflictHelpersRequireEveryChoiceAndTrackPendingCounts() {
        val conflict = PendingConflicts.build("FocusMaps/Broken.json", "Broken")
        val pending = listOf(
            pendingEdit("broken", "FocusMaps/Broken.json"),
            pendingEdit("other", "FocusMaps/Other.json"),
        )
        val unresolvedState = ConflictResolutionUiState(
            targetPath = conflict.filePath,
            mapName = conflict.mapName,
            remoteDocument = MindMapDocument(rootNode = Node(uniqueIdentifier = "root", name = "Remote")),
            remoteRevision = "rev",
            localDocument = MindMapDocument(rootNode = Node(uniqueIdentifier = "root", name = "Local")),
            items = listOf(ConflictResolutionUiItem("broken", "text change in Broken")),
        )
        val resolvedState = unresolvedState.copy(
            items = listOf(ConflictResolutionUiItem("broken", "text change in Broken", PendingConflictResolution.Local)),
        )

        assertEquals(mapOf("FocusMaps/Broken.json" to 1), pendingConflictCounts(listOf(conflict), pending))
        assertTrue(hasPendingOperationForConflictMap(listOf(conflict), pending))
        assertTrue(hasPausedPendingOperation(emptyList(), emptyList(), listOf(conflict), pending))
        assertFalse(conflictResolutionCanAccept(unresolvedState))
        assertTrue(conflictResolutionCanAccept(resolvedState))
        assertEquals(listOf(conflict), syncPendingConflictMaps(listOf(conflict), pending))
        assertEquals(emptyList(), syncPendingConflictMaps(listOf(conflict), pending.drop(1)))
    }

    @Test
    fun syncStatusHelpersDeriveStateToneAndRetryAction() {
        val pendingInfo = syncStatusPanelInfo(
            FocusUiState(
                pendingCount = 2,
                statusMessage = "Queued changes.",
            ),
        )
        assertEquals("pending", pendingInfo.state)
        assertEquals(SyncStatusTone.Pending, pendingInfo.tone)
        assertEquals("2 pending changes", pendingInfo.pendingText)
        assertEquals(SyncStatusRetryAction.SyncPending, pendingInfo.retryAction)
        assertEquals("Retry queued sync", syncStatusRetryLabel(pendingInfo.retryAction))
        assertTrue(syncStatusIconDescription(pendingInfo).contains("Open sync status"))

        val loadingInfo = syncStatusPanelInfo(FocusUiState(loading = true, statusMessage = "Loading maps..."))
        assertEquals("syncing", loadingInfo.state)
        assertEquals(SyncStatusTone.Pending, loadingInfo.tone)
        assertEquals(SyncStatusRetryAction.None, loadingInfo.retryAction)

        val conflictInfo = syncStatusPanelInfo(
            FocusUiState(
                pendingConflictMaps = listOf(PendingConflicts.build("FocusMaps/Map.json", "Map")),
                statusMessage = "Resolve conflict.",
            ),
        )
        assertEquals("conflict", conflictInfo.state)
        assertEquals(SyncStatusTone.Warning, conflictInfo.tone)
        assertEquals(SyncStatusRetryAction.ReloadWorkspace, conflictInfo.retryAction)

        val blockedInfo = syncStatusPanelInfo(
            FocusUiState(
                unreadableMaps = listOf(
                    UnreadableMapEntry(
                        filePath = "FocusMaps/Broken.json",
                        fileName = "Broken.json",
                        mapName = "Broken",
                        revision = "rev",
                        reason = UnreadableMapReason.InvalidJson,
                        message = "Invalid JSON.",
                        rawText = "{}",
                    ),
                ),
            ),
        )
        assertEquals("blocked", blockedInfo.state)
        assertEquals(SyncStatusTone.Warning, blockedInfo.tone)

        val successInfo = syncStatusPanelInfo(
            FocusUiState(
                syncMetadata = SyncMetadata(
                    lastSyncState = "success",
                    lastMessage = "Synced.",
                ),
                statusMessage = "",
            ),
        )
        assertEquals("success", successInfo.state)
        assertEquals(SyncStatusTone.Success, successInfo.tone)
        assertEquals("Synced.", successInfo.message)
        assertEquals(SyncStatusRetryAction.None, successInfo.retryAction)

        val errorInfo = syncStatusPanelInfo(
            FocusUiState(
                syncMetadata = SyncMetadata(
                    lastSyncState = "error",
                    lastErrorSummary = "Network failed.",
                ),
            ),
        )
        assertEquals("error", errorInfo.state)
        assertEquals(SyncStatusTone.Error, errorInfo.tone)
        assertEquals("Network failed.", errorInfo.lastError)
        assertEquals(SyncStatusRetryAction.ReloadWorkspace, errorInfo.retryAction)
    }

    @Test
    fun syncStatusTimeFormatsWithFallbacks() {
        assertEquals("Never synced", formatSyncStatusTime(null, ZoneOffset.UTC))
        assertEquals("Never synced", formatSyncStatusTime("", ZoneOffset.UTC))
        assertEquals("2026-05-04 10:05", formatSyncStatusTime("2026-05-04T10:05:30Z", ZoneOffset.UTC))
        assertEquals("not-a-date", formatSyncStatusTime("not-a-date", ZoneOffset.UTC))
    }

    @Test
    fun topBarRefreshHelpersReflectConnectionAndLoadingState() {
        val settings = RepoSettings(
            repoOwner = "systems",
            repoName = "focus",
            repoBranch = "main",
            repoPath = "FocusMaps",
        )
        val ready = FocusUiState(repoSettings = settings, tokenPresent = true)
        val loading = ready.copy(loading = true)
        val missingToken = ready.copy(tokenPresent = false)
        val incomplete = ready.copy(repoSettings = RepoSettings(repoOwner = "systems"))
        val pendingStatus = syncStatusPanelInfo(ready.copy(pendingCount = 1, statusMessage = "Queued."))

        assertTrue(topBarRefreshAvailable(ready))
        assertTrue(topBarRefreshEnabled(ready))
        assertEquals("Refresh from GitHub", topBarRefreshDescription(ready))
        assertFalse(topBarRefreshAvailable(missingToken))
        assertFalse(topBarRefreshAvailable(incomplete))
        assertTrue(topBarRefreshAvailable(loading))
        assertFalse(topBarRefreshEnabled(loading))
        assertEquals("Refresh from GitHub unavailable", topBarRefreshDescription(loading))
        assertTrue(syncStatusIconSelected(pendingStatus))
        assertTrue(syncStatusIconDescription(pendingStatus).contains("Open sync status"))
        assertFalse(syncStatusIconDescription(pendingStatus).contains("Refresh from GitHub"))
        assertTrue(revalidateAccessAvailable(ready))
        assertFalse(revalidateAccessAvailable(loading))
        assertFalse(revalidateAccessAvailable(missingToken))
        assertFalse(revalidateAccessAvailable(incomplete))
        assertTrue(clearSavedTokenAvailable(ready))
        assertFalse(clearSavedTokenAvailable(loading))
        assertFalse(clearSavedTokenAvailable(missingToken))
        assertTrue(hardResetAvailable(ready))
        assertFalse(hardResetAvailable(loading))
        assertFalse(hardResetAvailable(missingToken))
        assertFalse(hardResetAvailable(incomplete))
    }

    @Test
    fun refreshWorkspaceMessagesAreStable() {
        assertEquals("Refreshed from GitHub. Pending operations: 0.", refreshWorkspaceSuccessMessage(0))
        assertEquals("Refreshed from GitHub. Pending operations: 2.", refreshWorkspaceSuccessMessage(2))
        assertEquals("Could not refresh from GitHub.", refreshWorkspaceFailureMessage(RuntimeException()))
        assertEquals("Network failed.", refreshWorkspaceFailureMessage(RuntimeException("Network failed.")))
        assertEquals("Saved token cleared. Enter a new token to reconnect.", clearSavedTokenStatusMessage())
        assertEquals("Hard reset started. Reloading from GitHub...", hardResetStartingMessage())
        assertEquals("Repository settings and a saved token are required before hard reset.", hardResetUnavailableMessage())
    }

    @Test
    fun clearSavedTokenStateKeepsWorkspaceData() {
        val snapshot = MapSnapshot(
            filePath = "FocusMaps/Map.json",
            fileName = "Map.json",
            mapName = "Map",
            document = MindMapDocument(rootNode = Node(uniqueIdentifier = "root", name = "Map")),
            revision = "rev",
            loadedAtMillis = 0,
        )
        val pending = listOf(pendingEdit("edit", "FocusMaps/Map.json"))
        val before = FocusUiState(
            tokenPresent = true,
            loading = true,
            snapshots = listOf(snapshot),
            selectedMapFilePath = snapshot.filePath,
            pendingCount = pending.size,
            statusMessage = "Connected.",
        )
        val after = before.copy(
            tokenPresent = false,
            loading = false,
            statusMessage = clearSavedTokenStatusMessage(),
        )

        assertEquals(before.snapshots, after.snapshots)
        assertEquals(before.selectedMapFilePath, after.selectedMapFilePath)
        assertEquals(before.pendingCount, after.pendingCount)
        assertFalse(after.tokenPresent)
        assertFalse(after.loading)
    }

    @Test
    fun hardResetStateClearsLocalWorkspaceAndRecoveryUi() {
        val snapshot = MapSnapshot(
            filePath = "FocusMaps/Map.json",
            fileName = "Map.json",
            mapName = "Map",
            document = MindMapDocument(rootNode = Node(uniqueIdentifier = "root", name = "Map")),
            revision = "rev",
            loadedAtMillis = 0,
        )
        val unreadable = UnreadableMapEntry(
            filePath = "FocusMaps/Broken.json",
            fileName = "Broken.json",
            mapName = "Broken",
            revision = "rev-broken",
            reason = UnreadableMapReason.InvalidJson,
            message = "Invalid JSON.",
            rawText = "{}",
        )
        val before = FocusUiState(
            snapshots = listOf(snapshot),
            selectedMapFilePath = snapshot.filePath,
            pendingCount = 2,
            unreadableMaps = listOf(unreadable),
            unreadablePendingCounts = mapOf(unreadable.filePath to 1),
            blockedPendingMaps = listOf(BlockedPendingMaps.build("FocusMaps/Blocked.json", "Blocked")),
            blockedPendingCounts = mapOf("FocusMaps/Blocked.json" to 1),
            pendingConflictMaps = listOf(PendingConflicts.build("FocusMaps/Conflict.json", "Conflict")),
            pendingConflictCounts = mapOf("FocusMaps/Conflict.json" to 1),
            localMapRepairState = LocalMapRepairUiState(targetPath = unreadable.filePath, draftText = "{}"),
            conflictResolutionState = ConflictResolutionUiState(
                targetPath = "FocusMaps/Conflict.json",
                mapName = "Conflict",
                items = listOf(ConflictResolutionUiItem("pending", "text change")),
            ),
            tokenPresent = true,
            statusMessage = "Before",
        )

        val after = before.withHardResetLocalState()

        assertEquals(emptyList(), after.snapshots)
        assertNull(after.selectedMapFilePath)
        assertEquals(0, after.pendingCount)
        assertEquals(emptyList(), after.unreadableMaps)
        assertEquals(emptyMap(), after.unreadablePendingCounts)
        assertEquals(emptyList(), after.blockedPendingMaps)
        assertEquals(emptyMap(), after.blockedPendingCounts)
        assertEquals(emptyList(), after.pendingConflictMaps)
        assertEquals(emptyMap(), after.pendingConflictCounts)
        assertEquals(LocalMapRepairUiState(), after.localMapRepairState)
        assertEquals(ConflictResolutionUiState(), after.conflictResolutionState)
        assertTrue(after.tokenPresent)
        assertEquals("Before", after.statusMessage)
    }

    @Test
    fun gitHubValidationHelpersClassifyFailures() {
        val unauthorized = validationError(GitHubApiException.Code.Unauthorized, 401)
        val forbidden = validationError(GitHubApiException.Code.Forbidden, 403)
        val notFound = validationError(GitHubApiException.Code.NotFound, 404)
        val rateLimit = validationError(GitHubApiException.Code.RateLimit, 403)
        val network = validationError(GitHubApiException.Code.Network, null)
        val branchNotFound = validationError(
            code = GitHubApiException.Code.NotFound,
            status = 404,
            contextLabel = "validating branch \"main\"",
        )

        assertTrue(GitHubAccessValidation.shouldClearTokenAfterValidationFailure(unauthorized))
        assertTrue(GitHubAccessValidation.shouldClearTokenAfterValidationFailure(forbidden))
        assertFalse(GitHubAccessValidation.shouldClearTokenAfterValidationFailure(notFound))
        assertFalse(GitHubAccessValidation.shouldClearTokenAfterValidationFailure(rateLimit))
        assertFalse(GitHubAccessValidation.shouldClearTokenAfterValidationFailure(network))
        assertEquals("warning", GitHubAccessValidation.failureState(rateLimit))
        assertEquals("error", GitHubAccessValidation.failureState(network))
        assertEquals(
            "Token was rejected (401 Unauthorized) while validating repository access. Please generate a new token.",
            GitHubAccessValidation.failureMessage(unauthorized),
        )
        assertEquals(
            "Configured branch was not found (404 Not Found) while validating branch \"main\". Check the branch name.",
            GitHubAccessValidation.failureMessage(branchNotFound),
        )
        assertEquals("GitHub access validated.", GitHubAccessValidation.SuccessMessage)
        assertEquals("Could not validate GitHub access.", GitHubAccessValidation.failureMessage(RuntimeException()))
    }

    @Test
    fun focusedVisibleNodesShowsOnlyFocusedSubtreeWithRelativeDepth() {
        val grandchild = Node(uniqueIdentifier = "grandchild", name = "Grandchild")
        val focused = Node(uniqueIdentifier = "focused", name = "Focused", children = listOf(grandchild))
        val sibling = Node(uniqueIdentifier = "sibling", name = "Sibling")
        val document = MindMapDocument(
            rootNode = Node(uniqueIdentifier = "root", name = "Root", children = listOf(focused, sibling)),
        )

        val visible = focusedVisibleNodes(document, "focused")

        assertEquals(listOf("focused", "grandchild"), visible.map { it.node.uniqueIdentifier })
        assertEquals(listOf(0, 1), visible.map { it.depth })
    }

    @Test
    fun focusedVisibleNodesRespectsStoredCollapsedByDefault() {
        val grandchild = Node(uniqueIdentifier = "grandchild", name = "Grandchild")
        val focused = Node(uniqueIdentifier = "focused", name = "Focused", collapsed = true, children = listOf(grandchild))
        val document = MindMapDocument(
            rootNode = Node(uniqueIdentifier = "root", name = "Root", children = listOf(focused)),
        )

        val visible = focusedVisibleNodes(document, "focused")

        assertEquals(listOf("focused"), visible.map { it.node.uniqueIdentifier })
        assertTrue(visible.single().hasVisibleChildren)
        assertTrue(visible.single().collapsed)
    }

    @Test
    fun focusedVisibleNodesCanLocallyExpandStoredCollapsedNode() {
        val grandchild = Node(uniqueIdentifier = "grandchild", name = "Grandchild")
        val focused = Node(uniqueIdentifier = "focused", name = "Focused", collapsed = true, children = listOf(grandchild))
        val document = MindMapDocument(
            rootNode = Node(uniqueIdentifier = "root", name = "Root", children = listOf(focused)),
        )

        val visible = focusedVisibleNodes(document, "focused", mapOf("focused" to false))

        assertEquals(listOf("focused", "grandchild"), visible.map { it.node.uniqueIdentifier })
        assertFalse(visible.first().collapsed)
    }

    @Test
    fun focusedVisibleNodesCanLocallyCollapseExpandedNode() {
        val grandchild = Node(uniqueIdentifier = "grandchild", name = "Grandchild")
        val focused = Node(uniqueIdentifier = "focused", name = "Focused", children = listOf(grandchild))
        val document = MindMapDocument(
            rootNode = Node(uniqueIdentifier = "root", name = "Root", children = listOf(focused)),
        )

        val visible = focusedVisibleNodes(document, "focused", mapOf("focused" to true))

        assertEquals(listOf("focused"), visible.map { it.node.uniqueIdentifier })
        assertTrue(visible.single().collapsed)
    }

    @Test
    fun focusedVisibleNodesRespectsHideDoneForFocusedDescendants() {
        val done = Node(uniqueIdentifier = "done", name = "Done", taskState = TaskState.Done)
        val todo = Node(uniqueIdentifier = "todo", name = "Todo", taskState = TaskState.Todo)
        val focused = Node(
            uniqueIdentifier = "focused",
            name = "Focused",
            hideDoneTasks = true,
            children = listOf(done, todo),
        )
        val document = MindMapDocument(
            rootNode = Node(uniqueIdentifier = "root", name = "Root", children = listOf(focused)),
        )

        val visible = focusedVisibleNodes(document, "focused")

        assertEquals(listOf("focused", "todo"), visible.map { it.node.uniqueIdentifier })
    }

    @Test
    fun focusedVisibleNodesAppliesHideDoneInsideExpandedBranches() {
        val done = Node(uniqueIdentifier = "done", name = "Done", taskState = TaskState.Done)
        val todo = Node(uniqueIdentifier = "todo", name = "Todo", taskState = TaskState.Todo)
        val focused = Node(
            uniqueIdentifier = "focused",
            name = "Focused",
            collapsed = true,
            hideDoneTasks = true,
            children = listOf(done, todo),
        )
        val document = MindMapDocument(
            rootNode = Node(uniqueIdentifier = "root", name = "Root", children = listOf(focused)),
        )

        val visible = focusedVisibleNodes(document, "focused", mapOf("focused" to false))

        assertEquals(listOf("focused", "todo"), visible.map { it.node.uniqueIdentifier })
    }

    @Test
    fun missingFocusedNodeFallsBackToRoot() {
        val document = MindMapDocument(rootNode = Node(uniqueIdentifier = "root", name = "Root"))

        val record = resolveFocusedNodeRecord(document, "missing")

        assertEquals("root", record.node.uniqueIdentifier)
        assertNull(record.parent)
    }

    @Test
    fun focusAfterDeletingFocusedNodeMovesToParentOtherwiseStaysPut() {
        val child = Node(uniqueIdentifier = "child", name = "Child")
        val sibling = Node(uniqueIdentifier = "sibling", name = "Sibling")
        val document = MindMapDocument(
            rootNode = Node(uniqueIdentifier = "root", name = "Root", children = listOf(child, sibling)),
        )

        assertEquals("root", focusAfterDeletingNode(document, focusedNodeId = "child", deletedNodeId = "child"))
        assertEquals("child", focusAfterDeletingNode(document, focusedNodeId = "child", deletedNodeId = "sibling"))
    }

    @Test
    fun hideDoneFloatingActionAppearsForEffectiveHideDoneOrDoneDescendants() {
        val done = Node(uniqueIdentifier = "done", name = "Done", taskState = TaskState.Done)
        val withDone = Node(uniqueIdentifier = "with-done", name = "With done", children = listOf(done))
        val hidden = Node(uniqueIdentifier = "hidden", name = "Hidden", hideDoneTasks = true)
        val plain = Node(uniqueIdentifier = "plain", name = "Plain")
        val idea = Node(
            uniqueIdentifier = "idea",
            name = "Idea",
            nodeType = NodeType.IdeaBagItem,
            children = listOf(done),
        )
        val document = MindMapDocument(
            rootNode = Node(uniqueIdentifier = "root", name = "Root", children = listOf(withDone, hidden, plain, idea)),
        )

        assertTrue(shouldShowFocusedHideDoneAction(document, "with-done"))
        assertTrue(shouldShowFocusedHideDoneAction(document, "hidden"))
        assertFalse(shouldShowFocusedHideDoneAction(document, "plain"))
        assertFalse(shouldShowFocusedHideDoneAction(document, "idea"))
    }

    @Test
    fun focusedAddTargetUsesEditableFocusedNodeOnly() {
        val editable = Node(uniqueIdentifier = "editable", name = "Editable")
        val idea = Node(uniqueIdentifier = "idea", name = "Idea", nodeType = NodeType.IdeaBagItem)
        val document = MindMapDocument(
            rootNode = Node(uniqueIdentifier = "root", name = "Root", children = listOf(editable, idea)),
        )

        assertEquals("editable", focusedAddTargetNode(document, "editable")?.uniqueIdentifier)
        assertNull(focusedAddTargetNode(document, "idea"))
        assertEquals("root", focusedAddTargetNode(document, "missing")?.uniqueIdentifier)
        assertEquals("Add task under Editable", addChildActionLabel(editable, asTask = true))
        assertEquals("Add note under Editable", addChildActionLabel(editable, asTask = false))
    }

    @Test
    fun fabSideHelpersLabelPositionAndPreserveOtherPreferences() {
        val current = UiPreferences(theme = ThemePreference.Dark, fabSide = FabSidePreference.Right)
        val updated = uiPreferencesWithFabSide(current, FabSidePreference.Left)

        assertEquals(ThemePreference.Dark, updated.theme)
        assertEquals(FabSidePreference.Left, updated.fabSide)
        assertEquals("Left", fabSideLabel(FabSidePreference.Left))
        assertEquals("Right", fabSideLabel(FabSidePreference.Right))
        assertEquals("Floating buttons on left, selected", fabSideContentDescription(FabSidePreference.Left, selected = true))
        assertEquals("Floating buttons on right", fabSideContentDescription(FabSidePreference.Right, selected = false))
        assertEquals(Alignment.BottomStart, fabAlignment(FabSidePreference.Left))
        assertEquals(Alignment.BottomEnd, fabAlignment(FabSidePreference.Right))
    }

    @Test
    fun mapEditorFabOrderingMirrorsForSelectedSide() {
        assertEquals(
            listOf(MapEditorFabAction.HideDone, MapEditorFabAction.AddTask, MapEditorFabAction.AddNote),
            mapEditorFabActionsForSide(
                side = FabSidePreference.Right,
                showHideDone = true,
                showAddActions = true,
            ),
        )
        assertEquals(
            listOf(MapEditorFabAction.AddNote, MapEditorFabAction.AddTask, MapEditorFabAction.HideDone),
            mapEditorFabActionsForSide(
                side = FabSidePreference.Left,
                showHideDone = true,
                showAddActions = true,
            ),
        )
        assertEquals(
            listOf(MapEditorFabAction.HideDone),
            mapEditorFabActionsForSide(
                side = FabSidePreference.Right,
                showHideDone = true,
                showAddActions = false,
            ),
        )
        assertEquals(
            listOf(MapEditorFabAction.AddNote, MapEditorFabAction.AddTask),
            mapEditorFabActionsForSide(
                side = FabSidePreference.Left,
                showHideDone = false,
                showAddActions = true,
            ),
        )
    }

    @Test
    fun addChildDialogBehaviorKeepsOnlyNoteComposerOpen() {
        assertFalse(shouldDismissAfterAddChild(asTask = false))
        assertTrue(shouldDismissAfterAddChild(asTask = true))
        assertEquals("Add child notes", addChildDialogTitle(asTask = false))
        assertEquals("Add child task", addChildDialogTitle(asTask = true))
        assertEquals("Child note text", addChildTextFieldLabel(asTask = false))
        assertEquals("Child task text", addChildTextFieldLabel(asTask = true))
        assertEquals("Add a child note", addChildTextFieldPlaceholder(asTask = false))
        assertEquals("Describe the new task", addChildTextFieldPlaceholder(asTask = true))
    }

    @Test
    fun collapseOverridesAndLabelsReflectEffectiveState() {
        val node = Node(uniqueIdentifier = "node", name = "Branch", collapsed = true)

        assertTrue(effectiveCollapsed(node, emptyMap()))
        assertFalse(effectiveCollapsed(node, mapOf("node" to false)))
        assertEquals(mapOf("node" to false), toggleCollapsed(node, emptyMap()))
        assertEquals(mapOf("node" to true), toggleCollapsed(node, mapOf("node" to false)))
        assertEquals("Expand Branch", collapseActionLabel(node, collapsed = true))
        assertEquals("Collapse Branch", collapseActionLabel(node, collapsed = false))
    }

    @Test
    fun focusedNodeSecondClickEditsOnlyEditableFocusedNode() {
        assertTrue(shouldEditFocusedNode(clickedNodeId = "node", focusedNodeId = "node", canEditNode = true))
        assertFalse(shouldEditFocusedNode(clickedNodeId = "child", focusedNodeId = "node", canEditNode = true))
        assertFalse(shouldEditFocusedNode(clickedNodeId = "node", focusedNodeId = "node", canEditNode = false))
    }

    @Test
    fun clipboardAttachmentNodeHelpersDetectSourcesAndPrimaryAttachment() {
        val blankPath = NodeAttachment(id = "blank", relativePath = " ", displayName = "Blank")
        val openable = NodeAttachment(id = "openable", relativePath = "note.txt", displayName = "Note", mediaType = "text/plain")
        val clipboardText = Node(
            uniqueIdentifier = "clip-text",
            name = "Clipboard text",
            metadata = NodeMetadata(
                source = NodeMetadataSources.ClipboardText,
                attachments = listOf(blankPath, openable),
            ),
        )
        val clipboardImage = clipboardText.copy(metadata = clipboardText.metadata?.copy(source = NodeMetadataSources.ClipboardImage))
        val manual = clipboardText.copy(metadata = clipboardText.metadata?.copy(source = NodeMetadataSources.Manual))

        assertTrue(isClipboardAttachmentNode(clipboardText))
        assertTrue(isClipboardAttachmentNode(clipboardImage))
        assertFalse(isClipboardAttachmentNode(manual))
        assertEquals(openable, primaryNodeAttachment(clipboardText))
        assertEquals(openable, openablePrimaryAttachment(clipboardText))
        assertNull(openablePrimaryAttachment(manual))
    }

    @Test
    fun focusedClipboardNodeClickOpensPrimaryAttachmentWithEditFallback() {
        val openable = NodeAttachment(id = "openable", relativePath = "note.txt", displayName = "Note", mediaType = "text/plain")
        val clipboardWithAttachment = Node(
            uniqueIdentifier = "clip",
            name = "Clipboard",
            metadata = NodeMetadata(
                source = NodeMetadataSources.ClipboardText,
                attachments = listOf(openable),
            ),
        )
        val clipboardWithoutAttachment = clipboardWithAttachment.copy(
            uniqueIdentifier = "clip-empty",
            metadata = clipboardWithAttachment.metadata?.copy(attachments = emptyList()),
        )
        val manual = Node(uniqueIdentifier = "manual", name = "Manual")

        assertEquals(
            FocusedNodeClickAction.OpenPrimaryAttachment,
            focusedNodeClickAction("clip", "clip", clipboardWithAttachment),
        )
        assertEquals(
            FocusedNodeClickAction.EditText,
            focusedNodeClickAction("clip-empty", "clip-empty", clipboardWithoutAttachment),
        )
        assertEquals(
            FocusedNodeClickAction.EditText,
            focusedNodeClickAction("manual", "manual", manual),
        )
        assertEquals(
            FocusedNodeClickAction.FocusNode,
            focusedNodeClickAction("child", "manual", manual),
        )
    }

    @Test
    fun taskMarkerQuickMarksOnlyNonRootPlainNodes() {
        val plain = Node(uniqueIdentifier = "plain", name = "Plain", taskState = TaskState.None)
        val todo = Node(uniqueIdentifier = "todo", name = "Todo", taskState = TaskState.Todo)
        val idea = Node(uniqueIdentifier = "idea", name = "Idea", nodeType = NodeType.IdeaBagItem)

        assertTrue(canQuickMarkTodo(isRoot = false, node = plain))
        assertFalse(canQuickMarkTodo(isRoot = true, node = plain))
        assertFalse(canQuickMarkTodo(isRoot = false, node = todo))
        assertFalse(canQuickMarkTodo(isRoot = false, node = idea))
    }

    @Test
    fun taskStateModifiersShowOnlyAfterNodeHasTaskState() {
        val plain = Node(uniqueIdentifier = "plain", name = "Plain", taskState = TaskState.None)
        val todo = Node(uniqueIdentifier = "todo", name = "Todo", taskState = TaskState.Todo)
        val doing = Node(uniqueIdentifier = "doing", name = "Doing", taskState = TaskState.Doing)
        val done = Node(uniqueIdentifier = "done", name = "Done", taskState = TaskState.Done)
        val idea = Node(uniqueIdentifier = "idea", name = "Idea", nodeType = NodeType.IdeaBagItem, taskState = TaskState.Todo)

        assertFalse(shouldShowTaskStateModifiers(isRoot = false, node = plain, isFocusedNode = true))
        assertTrue(shouldShowTaskStateModifiers(isRoot = false, node = todo, isFocusedNode = true))
        assertTrue(shouldShowTaskStateModifiers(isRoot = false, node = doing, isFocusedNode = true))
        assertTrue(shouldShowTaskStateModifiers(isRoot = false, node = done, isFocusedNode = true))
        assertFalse(shouldShowTaskStateModifiers(isRoot = true, node = todo, isFocusedNode = true))
        assertFalse(shouldShowTaskStateModifiers(isRoot = false, node = idea, isFocusedNode = true))
        assertFalse(shouldShowTaskStateModifiers(isRoot = false, node = todo, isFocusedNode = false))
    }

    @Test
    fun starToggleEligibilityAndLabelsMatchNodeState() {
        val plain = Node(uniqueIdentifier = "plain", name = "Plain", starred = false)
        val starred = Node(uniqueIdentifier = "starred", name = "Important", starred = true)
        val idea = Node(uniqueIdentifier = "idea", name = "Idea", nodeType = NodeType.IdeaBagItem)

        assertTrue(canToggleStar(isRoot = false, node = plain))
        assertFalse(canToggleStar(isRoot = true, node = plain))
        assertFalse(canToggleStar(isRoot = false, node = idea))
        assertEquals("Star Plain", starActionLabel(plain))
        assertEquals("Unstar Important", starActionLabel(starred))
    }

    @Test
    fun relatedNodeGroupsMergeByLabelAndSortOutgoingBeforeBacklinks() {
        val outgoingRelates = relatedEntry("relates", RelatedNodeEntry.Direction.Outgoing, "Relates")
        val outgoingPrerequisite = relatedEntry("prerequisite", RelatedNodeEntry.Direction.Outgoing, "Prerequisite")
        val backlinkCauses = relatedEntry("backlink: causes", RelatedNodeEntry.Direction.Backlink, "Causes")
        val secondRelates = relatedEntry("relates", RelatedNodeEntry.Direction.Outgoing, "Second relates")

        val groups = relatedNodeGroups(
            outgoing = listOf(outgoingRelates, outgoingPrerequisite, secondRelates),
            backlinks = listOf(backlinkCauses),
        )

        assertEquals(listOf("prerequisite", "relates", "backlink: causes"), groups.map { it.relationLabel })
        assertEquals(listOf(outgoingRelates, secondRelates), groups.first { it.relationLabel == "relates" }.entries)
    }

    @Test
    fun relatedNodeKeysLabelsAndActionLabelsAreStable() {
        val entry = relatedEntry("backlink: causes", RelatedNodeEntry.Direction.Backlink, "[red]Source[!]")
        val group = RelatedNodeGroup("backlink: causes", listOf(entry))

        assertEquals("FocusMaps/Map.json::node-1::backlink: causes", relatedNodeGroupKey("FocusMaps/Map.json", "node-1", "backlink: causes"))
        assertEquals("Show backlink: causes", relatedNodeToggleLabel(group, expanded = false))
        assertEquals("Hide backlink: causes", relatedNodeToggleLabel(group, expanded = true))
        assertEquals("Open Source in Map via backlink: causes", relatedNodeEntryActionLabel(entry))
    }

    @Test
    fun focusRequestFallsBackToRootWhenTargetIsMissing() {
        val child = Node(uniqueIdentifier = "child", name = "Child")
        val document = MindMapDocument(rootNode = Node(uniqueIdentifier = "root", name = "Root", children = listOf(child)))

        assertEquals("child", resolveFocusRequestNodeId(document, "child"))
        assertEquals("root", resolveFocusRequestNodeId(document, "missing"))
    }

    @Test
    fun attachmentHelpersUsePwaDisplayFallbacksAndTypeLabels() {
        val named = NodeAttachment(displayName = "Camera photo", relativePath = "camera.jpg", mediaType = "image/jpeg")
        val pathOnly = NodeAttachment(displayName = " ", relativePath = "notes.md", mediaType = "text/markdown")
        val empty = NodeAttachment(displayName = " ", relativePath = " ", mediaType = "audio/webm")
        val binary = NodeAttachment(displayName = "scan.pdf", relativePath = "scan.pdf", mediaType = "application/pdf")

        assertEquals("Camera photo", attachmentDisplayName(named))
        assertEquals("notes.md", attachmentDisplayName(pathOnly))
        assertEquals("Attachment", attachmentDisplayName(empty))
        assertEquals("IMG", attachmentTypeLabel(named))
        assertEquals("TXT", attachmentTypeLabel(pathOnly))
        assertEquals("AUD", attachmentTypeLabel(empty))
        assertEquals("IMG", attachmentTypeLabel(binary))
    }

    @Test
    fun attachmentHelpersReturnMetadataAttachmentsAndAccessibilityLabel() {
        val attachment = NodeAttachment(displayName = "[red]Plan[!]", relativePath = "plan.txt", mediaType = "text/plain")
        val nodeWithAttachments = Node(
            uniqueIdentifier = "node",
            name = "Node",
            metadata = NodeMetadata(attachments = listOf(attachment)),
        )
        val nodeWithoutMetadata = Node(uniqueIdentifier = "plain", name = "Plain")

        assertEquals(listOf(attachment), nodeAttachments(nodeWithAttachments))
        assertEquals(emptyList(), nodeAttachments(nodeWithoutMetadata))
        assertEquals("Attachment [red]Plan[!], TXT", attachmentAccessibilityLabel(attachment))
    }

    @Test
    fun attachmentRowsAreViewableOnlyWhenRelativePathExists() {
        assertTrue(canViewAttachment(NodeAttachment(relativePath = "photo.jpg", displayName = "Photo")))
        assertFalse(canViewAttachment(NodeAttachment(relativePath = " ", displayName = "Photo")))
    }

    @Test
    fun attachmentDeleteHelpersScopeTargetsAndLabels() {
        val attachment = NodeAttachment(id = "attachment-1", relativePath = "photo.jpg", displayName = "Photo")
        val editable = Node(uniqueIdentifier = "editable", name = "Editable", metadata = NodeMetadata(attachments = listOf(attachment)))
        val idea = Node(uniqueIdentifier = "idea", name = "Idea", nodeType = NodeType.IdeaBagItem, metadata = NodeMetadata(attachments = listOf(attachment)))
        val state = AttachmentDeleteUiState(
            targetKey = attachmentDeleteTargetKey("FocusMaps/Map.json", "editable", "attachment-1"),
            deleting = true,
            errorMessage = "Could not delete",
        )

        assertTrue(canDeleteAttachmentsFromNode(editable))
        assertFalse(canDeleteAttachmentsFromNode(idea))
        assertTrue(canDeleteAttachment(attachment))
        assertFalse(canDeleteAttachment(attachment.copy(id = "")))
        assertFalse(canDeleteAttachment(attachment.copy(relativePath = " ")))
        assertEquals("Delete Photo", attachmentDeleteActionLabel(attachment))
        assertEquals(
            "Delete \"Photo\"? This removes the file from the node and cannot be undone.",
            deleteAttachmentConfirmationText("Photo"),
        )
        assertEquals("Photo", attachmentDeleteTarget("FocusMaps/Map.json", "editable", attachment, "sha-1").displayName)
        assertEquals("sha-1", attachmentDeleteTarget("FocusMaps/Map.json", "editable", attachment, "sha-1").expectedRevision)
        assertEquals(state, state.forTarget("FocusMaps/Map.json", "editable", "attachment-1"))
        assertEquals(AttachmentDeleteUiState(), state.forTarget("FocusMaps/Map.json", "editable", "other"))
    }

    @Test
    fun viewerDeleteTargetRequiresLoadedEditableAttachment() {
        val attachment = NodeAttachment(id = "attachment-1", relativePath = "photo.jpg", displayName = "Photo", mediaType = "image/jpeg")
        val node = Node(
            uniqueIdentifier = "node",
            name = "Node",
            metadata = NodeMetadata(attachments = listOf(attachment)),
        )
        val snapshot = MapSnapshot(
            filePath = "FocusMaps/Map.json",
            fileName = "Map.json",
            mapName = "Map",
            document = MindMapDocument(rootNode = Node(uniqueIdentifier = "root", name = "Root", children = listOf(node))),
            revision = "map-sha",
            loadedAtMillis = 1L,
        )
        val request = AttachmentViewerRequest(
            filePath = snapshot.filePath,
            nodeId = node.uniqueIdentifier,
            attachment = attachment,
            kind = AttachmentViewerKind.Image,
        )
        val loadedState = AttachmentViewerUiState(
            request = request,
            bytes = byteArrayOf(1, 2),
            mediaType = "image/jpeg",
            versionToken = "attachment-sha",
        )

        val target = viewerAttachmentDeleteTarget(listOf(snapshot), loadedState)

        assertEquals("attachment-1", target?.attachmentId)
        assertEquals("attachment-sha", target?.expectedRevision)
        assertNull(viewerAttachmentDeleteTarget(listOf(snapshot), loadedState.copy(bytes = null)))
        assertNull(
            viewerAttachmentDeleteTarget(
                listOf(snapshot.copy(document = MindMapDocument(rootNode = snapshot.document.rootNode.copy(children = listOf(node.copy(nodeType = NodeType.IdeaBagItem)))))),
                loadedState,
            ),
        )
    }

    @Test
    fun attachmentUploadHelpersLimitUploadToEditableIdleNodes() {
        val editable = Node(uniqueIdentifier = "editable", name = "Editable")
        val idea = Node(uniqueIdentifier = "idea", name = "Idea", nodeType = NodeType.IdeaBagItem)
        val idle = AttachmentUploadUiState()
        val uploading = AttachmentUploadUiState(
            targetKey = attachmentUploadTargetKey("FocusMaps/Map.json", "editable"),
            uploading = true,
        )

        assertTrue(canAttachFiles(editable))
        assertFalse(canAttachFiles(idea))
        assertTrue(canStartAttachmentUpload(editable, idle))
        assertFalse(canStartAttachmentUpload(editable, uploading))
        assertFalse(canStartAttachmentUpload(idea, idle))
        assertTrue(canStartVoiceRecording(editable, idle))
        assertFalse(canStartVoiceRecording(editable, uploading))
        assertFalse(canStartVoiceRecording(idea, idle))
    }

    @Test
    fun attachmentUploadStateScopesToTheEditedNode() {
        val state = AttachmentUploadUiState(
            targetKey = attachmentUploadTargetKey("FocusMaps/Map.json", "node"),
            uploading = true,
            errorMessage = "Uploading",
        )

        assertEquals(state, state.forTarget("FocusMaps/Map.json", "node"))
        assertEquals(AttachmentUploadUiState(), state.forTarget("FocusMaps/Map.json", "other"))
    }

    @Test
    fun voiceRecordingStateDisablesAttachmentAndVoiceStartWhileActive() {
        val editable = Node(uniqueIdentifier = "editable", name = "Editable")
        val recording = VoiceRecordingUiState(status = VoiceRecordingStatus.Recording, elapsedMillis = 1_000)
        val saving = VoiceRecordingUiState(status = VoiceRecordingStatus.Saving, elapsedMillis = 1_000)

        assertTrue(recording.isActive)
        assertFalse(canStartAttachmentUpload(editable, AttachmentUploadUiState(), recording))
        assertFalse(canStartVoiceRecording(editable, AttachmentUploadUiState(), recording))
        assertFalse(canStartAttachmentUpload(editable, AttachmentUploadUiState(), saving))
        assertFalse(canStartVoiceRecording(editable, AttachmentUploadUiState(), saving))
    }
}

private fun relatedEntry(
    relationLabel: String,
    direction: RelatedNodeEntry.Direction,
    nodeName: String,
): RelatedNodeEntry =
    RelatedNodeEntry(
        direction = direction,
        mapPath = "FocusMaps/Map.json",
        mapName = "Map",
        nodeId = nodeName.lowercase().replace(" ", "-"),
        nodeName = nodeName,
        nodePathSegments = listOf("Map", nodeName),
        relationLabel = relationLabel,
    )

private fun pendingEdit(id: String, filePath: String): PendingMapOperation =
    PendingMapOperation(
        id = id,
        scope = "scope",
        operation = MapMutation.EditNodeText(
            filePath = filePath,
            nodeId = "node",
            text = "Edited",
            timestamp = "2026-05-04T10:00:00Z",
            commitMessage = "map:edit",
        ),
        enqueuedAtMillis = 1,
    )

private fun validationError(
    code: GitHubApiException.Code,
    status: Int?,
    contextLabel: String = "validating repository access",
): GitHubApiException =
    GitHubApiException(
        code = code,
        status = status,
        contextLabel = contextLabel,
        message = "raw",
    )
