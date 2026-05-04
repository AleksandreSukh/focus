package com.systemssanity.focus.ui

import android.app.Application
import android.content.ContentResolver
import android.net.Uri
import android.provider.OpenableColumns
import androidx.compose.runtime.getValue
import androidx.compose.runtime.mutableStateOf
import androidx.compose.runtime.setValue
import androidx.lifecycle.AndroidViewModel
import androidx.lifecycle.viewModelScope
import com.systemssanity.focus.FocusApplication
import com.systemssanity.focus.data.local.PendingMapOperation
import com.systemssanity.focus.data.local.RepoSettings
import com.systemssanity.focus.data.local.ThemePreference
import com.systemssanity.focus.data.local.UiPreferences
import com.systemssanity.focus.domain.maps.AttachmentUploads
import com.systemssanity.focus.domain.maps.AttachmentViewerKind
import com.systemssanity.focus.domain.maps.AttachmentViewers
import com.systemssanity.focus.domain.maps.BlockedPendingMapEntry
import com.systemssanity.focus.domain.maps.BlockedPendingMaps
import com.systemssanity.focus.domain.maps.CommitMessages
import com.systemssanity.focus.domain.maps.LocalMapRepairs
import com.systemssanity.focus.domain.maps.MapFilePaths
import com.systemssanity.focus.domain.maps.MapMutation
import com.systemssanity.focus.domain.maps.MapQueries
import com.systemssanity.focus.domain.maps.PendingConflictMapEntry
import com.systemssanity.focus.domain.maps.PendingConflictResolution
import com.systemssanity.focus.domain.maps.PendingConflicts
import com.systemssanity.focus.domain.maps.TaskFilter
import com.systemssanity.focus.domain.maps.UnreadableMapEntry
import com.systemssanity.focus.domain.maps.UnreadableMaps
import com.systemssanity.focus.domain.maps.resultFilePath
import com.systemssanity.focus.domain.maps.touchesFilePath
import com.systemssanity.focus.domain.model.ClockProvider
import com.systemssanity.focus.domain.model.MapSnapshot
import com.systemssanity.focus.domain.model.MindMapDocument
import com.systemssanity.focus.domain.model.NodeAttachment
import com.systemssanity.focus.domain.model.TaskState
import com.systemssanity.focus.domain.sync.FocusSyncWorker
import com.systemssanity.focus.domain.sync.WorkspaceConflictResolutionItem
import com.systemssanity.focus.domain.sync.WorkspaceMapResetResult
import com.systemssanity.focus.domain.sync.WorkspaceSyncCoordinator
import kotlinx.coroutines.Dispatchers
import kotlinx.coroutines.Job
import kotlinx.coroutines.flow.first
import kotlinx.coroutines.launch
import kotlinx.coroutines.withContext
import java.io.ByteArrayOutputStream
import java.util.Base64
import java.util.UUID

data class FocusUiState(
    val repoSettings: RepoSettings = RepoSettings(),
    val tokenPresent: Boolean = false,
    val connectionStateLoaded: Boolean = false,
    val snapshots: List<MapSnapshot> = emptyList(),
    val selectedMapFilePath: String? = null,
    val pendingCount: Int = 0,
    val unreadableMaps: List<UnreadableMapEntry> = emptyList(),
    val unreadablePendingCounts: Map<String, Int> = emptyMap(),
    val blockedPendingMaps: List<BlockedPendingMapEntry> = emptyList(),
    val blockedPendingCounts: Map<String, Int> = emptyMap(),
    val pendingConflictMaps: List<PendingConflictMapEntry> = emptyList(),
    val pendingConflictCounts: Map<String, Int> = emptyMap(),
    val taskFilter: TaskFilter = TaskFilter.Open,
    val uiPreferences: UiPreferences = UiPreferences(),
    val statusMessage: String = "Connection required.",
    val loading: Boolean = false,
    val workspaceLoadResultVersion: Long = 0,
    val workspaceLoadSucceeded: Boolean = false,
    val attachmentUploadState: AttachmentUploadUiState = AttachmentUploadUiState(),
    val attachmentDeleteState: AttachmentDeleteUiState = AttachmentDeleteUiState(),
    val attachmentViewerState: AttachmentViewerUiState = AttachmentViewerUiState(),
    val localMapRepairState: LocalMapRepairUiState = LocalMapRepairUiState(),
    val conflictResolutionState: ConflictResolutionUiState = ConflictResolutionUiState(),
)

data class ConflictResolutionUiState(
    val targetPath: String = "",
    val mapName: String = "",
    val loading: Boolean = false,
    val errorMessage: String = "",
    val remoteDocument: MindMapDocument? = null,
    val remoteRevision: String = "",
    val localDocument: MindMapDocument? = null,
    val items: List<ConflictResolutionUiItem> = emptyList(),
)

data class ConflictResolutionUiItem(
    val pendingOperationId: String,
    val description: String,
    val choice: PendingConflictResolution? = null,
)

private fun WorkspaceConflictResolutionItem.toUiItem(): ConflictResolutionUiItem =
    ConflictResolutionUiItem(
        pendingOperationId = pendingOperationId,
        description = description,
    )

internal fun conflictResolutionCanAccept(state: ConflictResolutionUiState): Boolean =
    !state.loading &&
        state.remoteDocument != null &&
        state.remoteRevision.isNotBlank() &&
        state.items.isNotEmpty() &&
        state.items.all { it.choice != null }

data class LocalMapRepairUiState(
    val targetPath: String = "",
    val draftText: String = "",
    val entry: UnreadableMapEntry? = null,
    val helperText: String = "",
    val saving: Boolean = false,
    val errorMessage: String = "",
)

internal fun LocalMapRepairUiState.forTarget(filePath: String): LocalMapRepairUiState =
    if (targetPath == filePath) this else LocalMapRepairUiState()

internal fun LocalMapRepairUiState.clearIfTarget(filePath: String): LocalMapRepairUiState =
    if (targetPath == filePath) LocalMapRepairUiState() else this

internal fun resetStatusMessage(primary: String, discardedPendingCount: Int): String =
    if (discardedPendingCount > 0) {
        "$primary ${UnreadableMaps.discardedPendingText(discardedPendingCount)}"
    } else {
        primary
    }

data class AttachmentUploadUiState(
    val targetKey: String = "",
    val uploading: Boolean = false,
    val errorMessage: String = "",
)

internal fun attachmentUploadTargetKey(filePath: String, nodeId: String): String =
    "$filePath::$nodeId"

internal fun AttachmentUploadUiState.forTarget(filePath: String, nodeId: String): AttachmentUploadUiState =
    if (targetKey == attachmentUploadTargetKey(filePath, nodeId)) this else AttachmentUploadUiState()

data class AttachmentDeleteUiState(
    val targetKey: String = "",
    val deleting: Boolean = false,
    val errorMessage: String = "",
)

internal fun attachmentDeleteTargetKey(filePath: String, nodeId: String, attachmentId: String): String =
    "$filePath::$nodeId::$attachmentId"

internal fun AttachmentDeleteUiState.isForTarget(filePath: String, nodeId: String, attachmentId: String): Boolean =
    targetKey == attachmentDeleteTargetKey(filePath, nodeId, attachmentId)

internal fun AttachmentDeleteUiState.forTarget(filePath: String, nodeId: String, attachmentId: String): AttachmentDeleteUiState =
    if (isForTarget(filePath, nodeId, attachmentId)) this else AttachmentDeleteUiState()

internal fun unreadablePendingCounts(
    unreadableMaps: List<UnreadableMapEntry>,
    pendingOperations: List<PendingMapOperation>,
): Map<String, Int> =
    unreadableMaps.associate { entry ->
        entry.filePath to pendingOperations.count { pending -> pending.operation.touchesFilePath(entry.filePath) }
    }

internal fun blockedPendingCounts(
    blockedPendingMaps: List<BlockedPendingMapEntry>,
    pendingOperations: List<PendingMapOperation>,
): Map<String, Int> =
    blockedPendingMaps.associate { entry ->
        entry.filePath to pendingOperations.count { pending -> pending.operation.touchesFilePath(entry.filePath) }
    }

internal fun pendingConflictCounts(
    pendingConflictMaps: List<PendingConflictMapEntry>,
    pendingOperations: List<PendingMapOperation>,
): Map<String, Int> =
    pendingConflictMaps.associate { entry ->
        entry.filePath to pendingOperations.count { pending -> pending.operation.touchesFilePath(entry.filePath) }
    }

internal fun hasPendingOperationForUnreadableMap(
    unreadableMaps: List<UnreadableMapEntry>,
    pendingOperations: List<PendingMapOperation>,
): Boolean =
    unreadableMaps.any { entry -> pendingOperations.any { pending -> pending.operation.touchesFilePath(entry.filePath) } }

internal fun hasPendingOperationForBlockedMap(
    blockedPendingMaps: List<BlockedPendingMapEntry>,
    pendingOperations: List<PendingMapOperation>,
): Boolean =
    blockedPendingMaps.any { entry -> pendingOperations.any { pending -> pending.operation.touchesFilePath(entry.filePath) } }

internal fun hasPendingOperationForConflictMap(
    pendingConflictMaps: List<PendingConflictMapEntry>,
    pendingOperations: List<PendingMapOperation>,
): Boolean =
    pendingConflictMaps.any { entry -> pendingOperations.any { pending -> pending.operation.touchesFilePath(entry.filePath) } }

internal fun hasPausedPendingOperation(
    unreadableMaps: List<UnreadableMapEntry>,
    blockedPendingMaps: List<BlockedPendingMapEntry>,
    pendingConflictMaps: List<PendingConflictMapEntry>,
    pendingOperations: List<PendingMapOperation>,
): Boolean =
    hasPendingOperationForUnreadableMap(unreadableMaps, pendingOperations) ||
        hasPendingOperationForBlockedMap(blockedPendingMaps, pendingOperations) ||
        hasPendingOperationForConflictMap(pendingConflictMaps, pendingOperations)

internal fun syncBlockedPendingMaps(
    existing: List<BlockedPendingMapEntry>,
    pendingOperations: List<PendingMapOperation>,
    newEntry: BlockedPendingMapEntry? = null,
): List<BlockedPendingMapEntry> =
    (existing + listOfNotNull(newEntry))
        .filter { entry -> pendingOperations.any { pending -> pending.operation.touchesFilePath(entry.filePath) } }
        .distinctBy { it.filePath }
        .sortedBy { it.fileName.lowercase() }

internal fun syncPendingConflictMaps(
    existing: List<PendingConflictMapEntry>,
    pendingOperations: List<PendingMapOperation>,
    newEntry: PendingConflictMapEntry? = null,
): List<PendingConflictMapEntry> =
    (existing + listOfNotNull(newEntry))
        .filter { entry -> pendingOperations.any { pending -> pending.operation.touchesFilePath(entry.filePath) } }
        .distinctBy { it.filePath }
        .sortedBy { it.fileName.lowercase() }

internal fun workspaceLoadedMessage(mapCount: Int, unreadableCount: Int): String {
    val loaded = "Loaded $mapCount map${if (mapCount == 1) "" else "s"}."
    return if (unreadableCount > 0) {
        "$loaded $unreadableCount map${if (unreadableCount == 1) "" else "s"} need${if (unreadableCount == 1) "s" else ""} repair."
    } else {
        loaded
    }
}

data class AttachmentViewerRequest(
    val filePath: String,
    val nodeId: String,
    val attachment: NodeAttachment,
    val kind: AttachmentViewerKind,
)

data class AttachmentViewerUiState(
    val request: AttachmentViewerRequest? = null,
    val loading: Boolean = false,
    val errorMessage: String = "",
    val bytes: ByteArray? = null,
    val mediaType: String = "",
    val versionToken: String = "",
)

internal fun unreadableMapViewerState(entry: UnreadableMapEntry): AttachmentViewerUiState {
    val attachment = UnreadableMaps.rawAttachment(entry)
    return AttachmentViewerUiState(
        request = AttachmentViewerRequest(
            filePath = entry.filePath,
            nodeId = "",
            attachment = attachment,
            kind = AttachmentViewerKind.Text,
        ),
        loading = false,
        bytes = UnreadableMaps.rawBytes(entry),
        mediaType = UnreadableMaps.RawMapMediaType,
    )
}

class FocusViewModel(application: Application) : AndroidViewModel(application) {
    private val container = (application as FocusApplication).appContainer
    private var workspaceJob: Job? = null
    private var workspaceScope: String? = null

    var uiState by mutableStateOf(FocusUiState())
        private set

    init {
        viewModelScope.launch {
            container.preferencesStore.repoSettings.collect { settings ->
                val tokenPresent = container.tokenStore.getToken(settings.scope) != null
                uiState = uiState.copy(
                    repoSettings = settings,
                    tokenPresent = tokenPresent,
                    connectionStateLoaded = true,
                    unreadableMaps = emptyList(),
                    unreadablePendingCounts = emptyMap(),
                    blockedPendingMaps = emptyList(),
                    blockedPendingCounts = emptyMap(),
                    pendingConflictMaps = emptyList(),
                    pendingConflictCounts = emptyMap(),
                    conflictResolutionState = ConflictResolutionUiState(),
                    statusMessage = if (settings.isComplete && tokenPresent) {
                        "Ready to load workspace."
                    } else {
                        "Connection required."
                    },
                )
                bindWorkspace(settings.scope)
            }
        }
        viewModelScope.launch {
            container.preferencesStore.uiPreferences.collect { preferences ->
                uiState = uiState.copy(uiPreferences = preferences)
            }
        }
    }

    fun saveConnection(settings: RepoSettings, token: String) {
        viewModelScope.launch {
            uiState = uiState.copy(loading = true, statusMessage = "Saving connection...")
            container.preferencesStore.saveRepoSettings(settings)
            if (token.isNotBlank()) {
                container.tokenStore.saveToken(settings.scope, token)
            }
            uiState = uiState.copy(
                repoSettings = settings,
                tokenPresent = container.tokenStore.getToken(settings.scope) != null,
                loading = false,
                statusMessage = "Connection saved.",
            )
            loadWorkspace(forceRefresh = true)
        }
    }

    fun loadWorkspace(forceRefresh: Boolean = true) {
        val settings = uiState.repoSettings
        val token = container.tokenStore.getToken(settings.scope)
        if (!settings.isComplete) {
            uiState = uiState
                .copy(statusMessage = "Repository owner, repository name, and branch are required.")
                .withWorkspaceLoadResult(succeeded = false)
            return
        }
        if (token == null) {
            uiState = uiState
                .copy(statusMessage = "A GitHub personal access token is required.")
                .withWorkspaceLoadResult(succeeded = false)
            return
        }

        viewModelScope.launch {
            uiState = uiState.copy(loading = true, statusMessage = "Loading maps from GitHub...")
            val coordinator = container.createWorkspaceSyncCoordinator(settings, token)
            coordinator.loadWorkspace(forceRefresh)
                .onSuccess { result ->
                    val workspace = result.workspace
                    val selectedFilePath = uiState.selectedMapFilePath
                        ?.takeIf { filePath -> workspace.snapshots.any { it.filePath == filePath } }
                        ?: workspace.snapshots.firstOrNull()?.filePath
                    val blockedPendingMaps = syncBlockedPendingMaps(
                        existing = result.blockedPendingMaps,
                        pendingOperations = workspace.pendingOperations,
                    )
                    val pendingConflictMaps = syncPendingConflictMaps(
                        existing = result.pendingConflictMaps,
                        pendingOperations = workspace.pendingOperations,
                    )
                    uiState = uiState.copy(
                        snapshots = workspace.snapshots,
                        selectedMapFilePath = selectedFilePath,
                        pendingCount = workspace.pendingOperations.size,
                        unreadableMaps = result.unreadableMaps,
                        unreadablePendingCounts = unreadablePendingCounts(result.unreadableMaps, workspace.pendingOperations),
                        blockedPendingMaps = blockedPendingMaps,
                        blockedPendingCounts = blockedPendingCounts(blockedPendingMaps, workspace.pendingOperations),
                        pendingConflictMaps = pendingConflictMaps,
                        pendingConflictCounts = pendingConflictCounts(pendingConflictMaps, workspace.pendingOperations),
                        tokenPresent = true,
                        loading = false,
                        statusMessage = workspaceLoadedMessage(workspace.snapshots.size, result.unreadableMaps.size),
                    ).withWorkspaceLoadResult(succeeded = true)
                }
                .onFailure { error ->
                    uiState = uiState.copy(
                        loading = false,
                        statusMessage = error.message ?: "Could not load maps.",
                    ).withWorkspaceLoadResult(succeeded = false)
                }
        }
    }

    fun setTaskFilter(filter: TaskFilter) {
        uiState = uiState.copy(taskFilter = filter)
    }

    fun setThemePreference(theme: ThemePreference) {
        viewModelScope.launch {
            val preferences = uiState.uiPreferences.copy(theme = theme)
            uiState = uiState.copy(uiPreferences = preferences)
            container.preferencesStore.saveUiPreferences(preferences)
        }
    }

    fun openMap(snapshot: MapSnapshot) {
        uiState = uiState.copy(selectedMapFilePath = snapshot.filePath)
    }

    fun createMap(mapName: String) {
        val trimmedName = mapName.trim()
        if (trimmedName.isBlank()) {
            uiState = uiState.copy(statusMessage = "Map name is required.")
            return
        }
        val connection = activeConnectionOrStatus() ?: return
        viewModelScope.launch {
            uiState = uiState.copy(loading = true, statusMessage = "Creating map...")
            val filePath = MapFilePaths.build(connection.settings.repoPath, trimmedName)
            container.createMindMapService(connection.settings, connection.token)
                .createMap(
                    filePath = filePath,
                    mapName = trimmedName,
                    commitMessage = CommitMessages.mapCreate(trimmedName),
                )
                .onSuccess { snapshot ->
                    val current = container.localStore.observeWorkspace(connection.settings.scope).first()
                    val next = current.copy(
                        snapshots = (current.snapshots.filterNot { it.filePath == snapshot.filePath } + snapshot)
                            .sortedBy { it.mapName.lowercase() },
                    )
                    container.localStore.saveWorkspace(connection.settings.scope, next)
                    uiState = uiState.copy(
                        loading = false,
                        snapshots = next.snapshots,
                        selectedMapFilePath = snapshot.filePath,
                        statusMessage = "Created ${snapshot.mapName}.",
                    )
                }
                .onFailure { error ->
                    uiState = uiState.copy(
                        loading = false,
                        statusMessage = error.message ?: "Could not create the map.",
                    )
                }
        }
    }

    fun deleteMap(filePath: String) {
        val snapshot = snapshotOrStatus(filePath) ?: return
        val connection = activeConnectionOrStatus() ?: return
        viewModelScope.launch {
            val current = container.localStore.observeWorkspace(connection.settings.scope).first()
            if (current.pendingOperations.any { it.operation.touchesFilePath(filePath) }) {
                uiState = uiState.copy(statusMessage = "Sync pending changes before deleting this map.")
                return@launch
            }

            uiState = uiState.copy(loading = true, statusMessage = "Deleting ${snapshot.mapName}...")
            container.createMindMapService(connection.settings, connection.token)
                .deleteMap(filePath, CommitMessages.mapDelete(snapshot.mapName))
                .onSuccess {
                    val latest = container.localStore.observeWorkspace(connection.settings.scope).first()
                    val next = latest.copy(snapshots = latest.snapshots.filterNot { it.filePath == filePath })
                    val selectedFilePath = uiState.selectedMapFilePath
                        ?.takeIf { selected -> next.snapshots.any { it.filePath == selected } }
                        ?: next.snapshots.firstOrNull()?.filePath
                    container.localStore.saveWorkspace(connection.settings.scope, next)
                    uiState = uiState.copy(
                        loading = false,
                        snapshots = next.snapshots,
                        selectedMapFilePath = selectedFilePath,
                        statusMessage = "Deleted ${snapshot.mapName}.",
                    )
                }
                .onFailure { error ->
                    uiState = uiState.copy(
                        loading = false,
                        statusMessage = error.message ?: "Could not delete the map.",
                    )
                }
        }
    }

    fun syncPendingNow() {
        val coordinator = coordinatorOrStatus() ?: return
        viewModelScope.launch {
            uiState = uiState.copy(loading = true, statusMessage = "Syncing queued changes...")
            coordinator.processPendingOperations()
                .onSuccess { result ->
                    val workspace = result.workspace
                    val nextUnreadableMaps = (uiState.unreadableMaps + listOfNotNull(result.pausedUnreadableMap))
                        .distinctBy { it.filePath }
                        .sortedBy { it.fileName.lowercase() }
                    val nextBlockedMaps = syncBlockedPendingMaps(
                        existing = uiState.blockedPendingMaps,
                        pendingOperations = workspace.pendingOperations,
                        newEntry = result.blockedPendingMap,
                    )
                    val nextConflictMaps = syncPendingConflictMaps(
                        existing = uiState.pendingConflictMaps,
                        pendingOperations = workspace.pendingOperations,
                        newEntry = result.pendingConflictMap,
                    )
                    if (result.pendingConflictMap != null) {
                        container.preferencesStore.recordSyncFailure(
                            PendingConflicts.statusMessage(result.pendingConflictMap),
                            state = "conflict",
                        )
                    } else if (result.blockedPendingMap != null) {
                        container.preferencesStore.recordSyncFailure(
                            "Queued changes for ${result.blockedPendingMap.mapName} need review.",
                            state = "blocked",
                        )
                    } else if (result.pausedUnreadableMap != null) {
                        container.preferencesStore.recordSyncFailure(
                            "Repair ${result.pausedUnreadableMap.mapName.ifBlank { result.pausedUnreadableMap.fileName }} to resume queued changes.",
                            state = "blocked",
                        )
                    } else {
                        container.preferencesStore.recordSyncSuccess(
                            "Synced queued changes. Pending operations: ${workspace.pendingOperations.size}.",
                        )
                    }
                    uiState = uiState.copy(
                        loading = false,
                        snapshots = workspace.snapshots,
                        pendingCount = workspace.pendingOperations.size,
                        unreadableMaps = nextUnreadableMaps,
                        unreadablePendingCounts = unreadablePendingCounts(nextUnreadableMaps, workspace.pendingOperations),
                        blockedPendingMaps = nextBlockedMaps,
                        blockedPendingCounts = blockedPendingCounts(nextBlockedMaps, workspace.pendingOperations),
                        pendingConflictMaps = nextConflictMaps,
                        pendingConflictCounts = pendingConflictCounts(nextConflictMaps, workspace.pendingOperations),
                        statusMessage = when {
                            result.pendingConflictMap != null -> PendingConflicts.statusMessage(result.pendingConflictMap)
                            result.blockedPendingMap != null ->
                                "Queued changes for \"${result.blockedPendingMap.mapName}\" need review."
                            result.pausedUnreadableMap != null ->
                                "Repair \"${result.pausedUnreadableMap.mapName.ifBlank { result.pausedUnreadableMap.fileName }}\" to resume queued changes."
                            else -> "Synced queued changes."
                        },
                    )
                }
                .onFailure { error ->
                    container.preferencesStore.recordSyncFailure(error.message ?: "Queued sync failed.")
                    uiState = uiState.copy(
                        loading = false,
                        statusMessage = error.message ?: "Queued sync failed. Background retry scheduled.",
                    )
                    FocusSyncWorker.enqueue(getApplication<Application>())
                }
        }
    }

    fun setTaskState(filePath: String, nodeId: String, taskState: TaskState) {
        val snapshot = snapshotOrStatus(filePath) ?: return
        enqueueMutation(
            MapMutation.SetTaskState(
                filePath = filePath,
                nodeId = nodeId,
                taskState = taskState,
                timestamp = ClockProvider.nowIsoSeconds(),
                commitMessage = CommitMessages.nodeTaskState(snapshot.mapName, nodeId, taskState.wireValue),
            ),
            successMessage = "Queued task state change.",
        )
    }

    fun editNodeText(filePath: String, nodeId: String, text: String) {
        val snapshot = snapshotOrStatus(filePath) ?: return
        val record = MapQueries.findNode(snapshot.document, nodeId)
        if (record == null) {
            uiState = uiState.copy(statusMessage = "The selected node is no longer loaded.")
            return
        }

        if (record.parent == null) {
            val newFilePath = MapFilePaths.rename(snapshot.filePath, text)
            if (newFilePath != snapshot.filePath) {
                if (uiState.snapshots.any { it.filePath == newFilePath }) {
                    uiState = uiState.copy(statusMessage = "A map named ${MapFilePaths.mapName(newFilePath)} already exists.")
                    return
                }
                enqueueMutation(
                    MapMutation.RenameMap(
                        filePath = filePath,
                        newFilePath = newFilePath,
                        nodeId = nodeId,
                        text = text,
                        timestamp = ClockProvider.nowIsoSeconds(),
                        commitMessage = CommitMessages.mapRename(snapshot.mapName, MapFilePaths.mapName(newFilePath)),
                    ),
                    successMessage = "Queued map rename.",
                )
                return
            }
        }

        enqueueMutation(
            MapMutation.EditNodeText(
                filePath = filePath,
                nodeId = nodeId,
                text = text,
                timestamp = ClockProvider.nowIsoSeconds(),
                commitMessage = CommitMessages.nodeEdit(snapshot.mapName, nodeId),
            ),
            successMessage = "Queued text edit.",
        )
    }

    fun addChild(filePath: String, parentNodeId: String, text: String, asTask: Boolean) {
        val snapshot = snapshotOrStatus(filePath) ?: return
        val timestamp = ClockProvider.nowIsoSeconds()
        val newNodeId = UUID.randomUUID().toString()
        val commitMessage = CommitMessages.nodeAdd(snapshot.mapName, text, if (asTask) "task" else "note")
        val mutation = if (asTask) {
            MapMutation.AddChildTask(
                filePath = filePath,
                parentNodeId = parentNodeId,
                newNodeId = newNodeId,
                text = text,
                timestamp = timestamp,
                commitMessage = commitMessage,
            )
        } else {
            MapMutation.AddChildNote(
                filePath = filePath,
                parentNodeId = parentNodeId,
                newNodeId = newNodeId,
                text = text,
                timestamp = timestamp,
                commitMessage = commitMessage,
            )
        }
        enqueueMutation(mutation, successMessage = if (asTask) "Queued child task." else "Queued child note.")
    }

    fun deleteNode(filePath: String, nodeId: String) {
        val snapshot = snapshotOrStatus(filePath) ?: return
        enqueueMutation(
            MapMutation.DeleteNode(
                filePath = filePath,
                nodeId = nodeId,
                timestamp = ClockProvider.nowIsoSeconds(),
                commitMessage = CommitMessages.nodeDelete(snapshot.mapName, nodeId),
            ),
            successMessage = "Queued node delete.",
        )
    }

    fun toggleHideDone(filePath: String, nodeId: String) {
        val snapshot = snapshotOrStatus(filePath) ?: return
        val current = MapQueries.getNodeHideDoneState(snapshot.document, nodeId)
        enqueueMutation(
            MapMutation.SetHideDoneTasks(
                filePath = filePath,
                nodeId = nodeId,
                hideDoneTasks = !current,
                timestamp = ClockProvider.nowIsoSeconds(),
                commitMessage = CommitMessages.nodeHideDone(snapshot.mapName, nodeId, !current),
            ),
            successMessage = if (current) "Queued show done tasks." else "Queued hide done tasks.",
        )
    }

    fun toggleStarred(filePath: String, nodeId: String) {
        val snapshot = snapshotOrStatus(filePath) ?: return
        val record = MapQueries.findNode(snapshot.document, nodeId)
        if (record == null) {
            uiState = uiState.copy(statusMessage = "The selected node is no longer loaded.")
            return
        }
        if (record.parent == null) {
            uiState = uiState.copy(statusMessage = "Root node cannot be starred.")
            return
        }
        if (record.node.isIdeaTag) {
            uiState = uiState.copy(statusMessage = "Idea-tag nodes cannot be starred.")
            return
        }

        val starred = !record.node.starred
        enqueueMutation(
            MapMutation.SetStarred(
                filePath = filePath,
                nodeId = nodeId,
                starred = starred,
                timestamp = ClockProvider.nowIsoSeconds(),
                commitMessage = CommitMessages.nodeStar(snapshot.mapName, nodeId, starred),
            ),
            successMessage = if (starred) "Queued star change." else "Queued unstar change.",
        )
    }

    fun uploadAttachment(filePath: String, nodeId: String, uri: Uri) {
        val snapshot = snapshotOrStatus(filePath) ?: return
        val record = MapQueries.findNode(snapshot.document, nodeId)
        if (record == null) {
            uiState = uiState.copy(statusMessage = "The selected node is no longer loaded.")
            return
        }
        if (!record.node.canEditText) {
            uiState = uiState.copy(statusMessage = "Attachments are not supported for this node.")
            return
        }
        val connection = activeConnectionOrStatus() ?: return
        val targetKey = attachmentUploadTargetKey(filePath, nodeId)

        viewModelScope.launch {
            uiState = uiState.copy(
                attachmentUploadState = AttachmentUploadUiState(targetKey = targetKey, uploading = true),
                statusMessage = "Uploading attachment...",
            )

            val selectedAttachment = readSelectedAttachment(uri)
                .getOrElse { error ->
                    val message = error.message ?: "Could not read the selected file."
                    uiState = uiState.copy(
                        attachmentUploadState = AttachmentUploadUiState(targetKey = targetKey, errorMessage = message),
                        statusMessage = message,
                    )
                    return@launch
                }

            uploadPreparedAttachmentBytes(
                snapshot = snapshot,
                connection = connection,
                nodeId = nodeId,
                attachment = selectedAttachment.attachment,
                bytes = selectedAttachment.bytes,
                targetKey = targetKey,
            )
        }
    }

    fun uploadPreparedAttachment(
        filePath: String,
        nodeId: String,
        displayName: String,
        fileName: String,
        mediaType: String,
        bytes: ByteArray,
    ) {
        val snapshot = snapshotOrStatus(filePath) ?: return
        val record = MapQueries.findNode(snapshot.document, nodeId)
        if (record == null) {
            uiState = uiState.copy(statusMessage = "The selected node is no longer loaded.")
            return
        }
        if (!record.node.canEditText) {
            uiState = uiState.copy(statusMessage = "Attachments are not supported for this node.")
            return
        }
        val connection = activeConnectionOrStatus() ?: return
        val targetKey = attachmentUploadTargetKey(filePath, nodeId)

        viewModelScope.launch {
            uiState = uiState.copy(
                attachmentUploadState = AttachmentUploadUiState(targetKey = targetKey, uploading = true),
                statusMessage = "Uploading attachment...",
            )

            val cleanDisplayName = AttachmentUploads.displayName(displayName)
            val cleanFileName = AttachmentUploads.displayName(fileName)
            val normalizedMediaType = AttachmentUploads.normalizedMediaType(mediaType, cleanFileName)
            AttachmentUploads.validationError(bytes.size.toLong(), normalizedMediaType, cleanFileName)?.let { message ->
                uiState = uiState.copy(
                    attachmentUploadState = AttachmentUploadUiState(targetKey = targetKey, errorMessage = message),
                    statusMessage = message,
                )
                return@launch
            }

            val timestamp = ClockProvider.nowIsoSeconds()
            val attachment = AttachmentUploads.attachment(
                attachmentId = UUID.randomUUID().toString(),
                displayName = cleanDisplayName,
                mediaType = normalizedMediaType,
                createdAtUtc = timestamp,
                fileName = cleanFileName,
            )
            uploadPreparedAttachmentBytes(
                snapshot = snapshot,
                connection = connection,
                nodeId = nodeId,
                attachment = attachment,
                bytes = bytes,
                targetKey = targetKey,
            )
        }
    }

    fun openAttachmentViewer(filePath: String, nodeId: String, attachment: NodeAttachment) {
        val snapshot = snapshotOrStatus(filePath) ?: return
        val record = MapQueries.findNode(snapshot.document, nodeId)
        if (record == null) {
            uiState = uiState.copy(statusMessage = "The selected node is no longer loaded.")
            return
        }
        val currentAttachment = record.node.metadata?.attachments.orEmpty()
            .firstOrNull { candidate ->
                candidate.id == attachment.id ||
                    (candidate.relativePath == attachment.relativePath && candidate.relativePath.isNotBlank())
            } ?: attachment
        if (!AttachmentViewers.canView(currentAttachment)) {
            uiState = uiState.copy(statusMessage = "Attachment file path is missing.")
            return
        }
        val connection = activeConnectionOrStatus() ?: return
        val request = AttachmentViewerRequest(
            filePath = filePath,
            nodeId = nodeId,
            attachment = currentAttachment,
            kind = AttachmentViewers.viewerKind(currentAttachment),
        )
        uiState = uiState.copy(
            attachmentViewerState = AttachmentViewerUiState(request = request, loading = true),
            statusMessage = "Loading attachment...",
        )

        viewModelScope.launch {
            container.createMindMapService(connection.settings, connection.token)
                .loadAttachment(
                    nodeId = nodeId,
                    relativePath = currentAttachment.relativePath,
                    mediaType = currentAttachment.mediaType,
                )
                .onSuccess { loaded ->
                    if (uiState.attachmentViewerState.request == request) {
                        uiState = uiState.copy(
                            attachmentViewerState = AttachmentViewerUiState(
                                request = request,
                                loading = false,
                                bytes = loaded.bytes,
                                mediaType = loaded.mediaType,
                                versionToken = loaded.versionToken,
                            ),
                            statusMessage = "Loaded ${AttachmentViewers.title(currentAttachment)}.",
                        )
                    }
                }
                .onFailure { error ->
                    if (uiState.attachmentViewerState.request == request) {
                        uiState = uiState.copy(
                            attachmentViewerState = AttachmentViewerUiState(
                                request = request,
                                loading = false,
                                errorMessage = error.message ?: "Failed to load attachment.",
                            ),
                            statusMessage = error.message ?: "Failed to load attachment.",
                        )
                    }
                }
        }
    }

    fun openUnreadableMapViewer(filePath: String) {
        val entry = uiState.unreadableMaps.firstOrNull { it.filePath == filePath }
        if (entry == null) {
            uiState = uiState.copy(statusMessage = "The unreadable map is no longer available.")
            return
        }
        uiState = uiState.copy(
            attachmentViewerState = unreadableMapViewerState(entry),
            statusMessage = "Loaded ${UnreadableMaps.rawFileName(entry)}.",
        )
    }

    fun openLocalMapRepair(filePath: String) {
        val blocked = uiState.blockedPendingMaps.firstOrNull { it.filePath == filePath }
        val baselineSnapshot = uiState.snapshots.firstOrNull { it.filePath == filePath }
        val entry = uiState.unreadableMaps.firstOrNull { it.filePath == filePath }
            ?: blocked?.let { BlockedPendingMaps.toRepairEntry(it, baselineSnapshot) }
        if (entry == null) {
            uiState = uiState.copy(statusMessage = "The recovery item is no longer available.")
            return
        }
        val draft = LocalMapRepairs.buildDraft(entry, baselineSnapshot)
        if (draft.isBlank()) {
            uiState = uiState.copy(statusMessage = LocalMapRepairs.noDraftMessage(entry))
            return
        }
        val helperText = blocked?.let(BlockedPendingMaps::repairHelperText)
            ?: LocalMapRepairs.helperText(entry)
        uiState = uiState.copy(
            localMapRepairState = LocalMapRepairUiState(
                targetPath = filePath,
                draftText = draft,
                entry = entry,
                helperText = helperText,
            ),
            statusMessage = "Repairing ${entry.mapName.ifBlank { entry.fileName.ifBlank { entry.filePath } }}.",
        )
    }

    fun closeLocalMapRepair() {
        uiState = uiState.copy(localMapRepairState = LocalMapRepairUiState())
    }

    fun saveLocalMapRepair(filePath: String, rawJson: String) {
        val blocked = uiState.blockedPendingMaps.firstOrNull { it.filePath == filePath }
        val baselineSnapshot = uiState.snapshots.firstOrNull { it.filePath == filePath }
        val entry = uiState.localMapRepairState.entry?.takeIf { it.filePath == filePath }
            ?: uiState.unreadableMaps.firstOrNull { it.filePath == filePath }
            ?: blocked?.let { BlockedPendingMaps.toRepairEntry(it, baselineSnapshot) }
        if (entry == null) {
            uiState = uiState.copy(
                localMapRepairState = LocalMapRepairUiState(),
                statusMessage = "The recovery item is no longer available.",
            )
            return
        }
        val helperText = uiState.localMapRepairState.helperText.ifBlank {
            blocked?.let(BlockedPendingMaps::repairHelperText) ?: LocalMapRepairs.helperText(entry)
        }
        val document = LocalMapRepairs.validateRepairJson(rawJson)
            .getOrElse { error ->
                uiState = uiState.copy(
                    localMapRepairState = LocalMapRepairUiState(
                        targetPath = filePath,
                        draftText = rawJson,
                        entry = entry,
                        helperText = helperText,
                        errorMessage = error.message ?: "Map JSON could not be parsed.",
                    ),
                    statusMessage = error.message ?: "Map JSON could not be parsed.",
                )
                return
            }
        val connection = activeConnectionOrStatus() ?: return

        viewModelScope.launch {
            uiState = uiState.copy(
                localMapRepairState = LocalMapRepairUiState(
                    targetPath = filePath,
                    draftText = rawJson,
                    entry = entry,
                    helperText = helperText,
                    saving = true,
                ),
                statusMessage = "Saving local repair...",
            )
            val repairedSnapshot = LocalMapRepairs.buildRepairSnapshot(
                entry = entry,
                document = document,
                baselineSnapshot = baselineSnapshot,
            )
            val current = container.localStore.observeWorkspace(connection.settings.scope).first()
            val nextSnapshots = (current.snapshots.filterNot { it.filePath == filePath } + repairedSnapshot)
                .sortedBy { it.fileName.lowercase() }
            val nextUnreadableMaps = uiState.unreadableMaps.filterNot { it.filePath == filePath }
            val nextBlockedMaps = uiState.blockedPendingMaps.filterNot { it.filePath == filePath }
            val nextConflictMaps = uiState.pendingConflictMaps.filterNot { it.filePath == filePath }
            val next = current.copy(snapshots = nextSnapshots)
            container.localStore.saveWorkspace(connection.settings.scope, next)
            val pendingCountForMap = current.pendingOperations.count { it.operation.touchesFilePath(filePath) }
            uiState = uiState.copy(
                snapshots = next.snapshots,
                selectedMapFilePath = repairedSnapshot.filePath,
                pendingCount = next.pendingOperations.size,
                unreadableMaps = nextUnreadableMaps,
                unreadablePendingCounts = unreadablePendingCounts(nextUnreadableMaps, next.pendingOperations),
                blockedPendingMaps = nextBlockedMaps,
                blockedPendingCounts = blockedPendingCounts(nextBlockedMaps, next.pendingOperations),
                pendingConflictMaps = nextConflictMaps,
                pendingConflictCounts = pendingConflictCounts(nextConflictMaps, next.pendingOperations),
                conflictResolutionState = if (uiState.conflictResolutionState.targetPath == filePath) {
                    ConflictResolutionUiState()
                } else {
                    uiState.conflictResolutionState
                },
                localMapRepairState = LocalMapRepairUiState(),
                statusMessage = if (pendingCountForMap > 0) {
                    "Saved a local repair for \"${repairedSnapshot.mapName}\". $pendingCountForMap queued change${if (pendingCountForMap == 1) "" else "s"} for this map ${if (pendingCountForMap == 1) "is" else "are"} ready to retry."
                } else {
                    "Saved a local repair for \"${repairedSnapshot.mapName}\" on this device."
                },
            )
            if (
                next.pendingOperations.isNotEmpty() &&
                !hasPausedPendingOperation(nextUnreadableMaps, nextBlockedMaps, nextConflictMaps, next.pendingOperations)
            ) {
                FocusSyncWorker.enqueue(getApplication<Application>())
            }
        }
    }

    fun resetUnreadableMapFromGitHub(filePath: String) {
        val unreadable = uiState.unreadableMaps.firstOrNull { it.filePath == filePath }
        val blocked = uiState.blockedPendingMaps.firstOrNull { it.filePath == filePath }
        if (unreadable == null && blocked == null) {
            uiState = uiState.copy(statusMessage = "The recovery item is no longer available.")
            return
        }
        val coordinator = coordinatorOrStatus() ?: return
        val mapLabel = unreadable?.mapName?.ifBlank { unreadable.fileName.ifBlank { unreadable.filePath } }
            ?: blocked?.mapName?.ifBlank { blocked.fileName.ifBlank { blocked.filePath } }
            ?: filePath

        viewModelScope.launch {
            uiState = uiState.copy(loading = true, statusMessage = "Resetting \"$mapLabel\" from GitHub...")
            coordinator.resetMapToRemote(filePath)
                .onSuccess { result ->
                    when (result) {
                        is WorkspaceMapResetResult.Reset -> {
                            val nextUnreadableMaps = uiState.unreadableMaps.filterNot { it.filePath == filePath }
                            val nextBlockedMaps = uiState.blockedPendingMaps.filterNot { it.filePath == filePath }
                            val nextConflictMaps = uiState.pendingConflictMaps.filterNot { it.filePath == filePath }
                            uiState = uiState.copy(
                                loading = false,
                                snapshots = result.workspace.snapshots,
                                selectedMapFilePath = result.snapshot.filePath,
                                pendingCount = result.workspace.pendingOperations.size,
                                unreadableMaps = nextUnreadableMaps,
                                unreadablePendingCounts = unreadablePendingCounts(nextUnreadableMaps, result.workspace.pendingOperations),
                                blockedPendingMaps = nextBlockedMaps,
                                blockedPendingCounts = blockedPendingCounts(nextBlockedMaps, result.workspace.pendingOperations),
                                pendingConflictMaps = nextConflictMaps,
                                pendingConflictCounts = pendingConflictCounts(nextConflictMaps, result.workspace.pendingOperations),
                                conflictResolutionState = if (uiState.conflictResolutionState.targetPath == filePath) {
                                    ConflictResolutionUiState()
                                } else {
                                    uiState.conflictResolutionState
                                },
                                localMapRepairState = uiState.localMapRepairState.clearIfTarget(filePath),
                                statusMessage = resetStatusMessage(
                                    primary = UnreadableMaps.resetSuccessMessage(result.snapshot.mapName),
                                    discardedPendingCount = result.discardedPendingCount,
                                ),
                            )
                            enqueueBackgroundSyncIfReady(result.workspace.pendingOperations, nextUnreadableMaps, nextBlockedMaps, nextConflictMaps)
                        }
                        is WorkspaceMapResetResult.StillUnreadable -> {
                            val nextUnreadableMaps = (uiState.unreadableMaps.filterNot { it.filePath == filePath } + result.entry)
                                .sortedBy { it.fileName.lowercase() }
                            val nextBlockedMaps = uiState.blockedPendingMaps.filterNot { it.filePath == filePath }
                            val nextConflictMaps = uiState.pendingConflictMaps.filterNot { it.filePath == filePath }
                            uiState = uiState.copy(
                                loading = false,
                                unreadableMaps = nextUnreadableMaps,
                                unreadablePendingCounts = unreadablePendingCounts(nextUnreadableMaps, result.workspace.pendingOperations),
                                blockedPendingMaps = nextBlockedMaps,
                                blockedPendingCounts = blockedPendingCounts(nextBlockedMaps, result.workspace.pendingOperations),
                                pendingConflictMaps = nextConflictMaps,
                                pendingConflictCounts = pendingConflictCounts(nextConflictMaps, result.workspace.pendingOperations),
                                statusMessage = "${UnreadableMaps.resetStillUnreadableMessage(result.entry)} Repair locally, reset this device to GitHub, or retry after fixing the file elsewhere.",
                            )
                        }
                        is WorkspaceMapResetResult.Deleted -> {
                            val nextUnreadableMaps = uiState.unreadableMaps.filterNot { it.filePath == filePath }
                            val nextBlockedMaps = uiState.blockedPendingMaps.filterNot { it.filePath == filePath }
                            val nextConflictMaps = uiState.pendingConflictMaps.filterNot { it.filePath == filePath }
                            val selectedFilePath = uiState.selectedMapFilePath
                                ?.takeIf { selected -> result.workspace.snapshots.any { it.filePath == selected } }
                                ?: result.workspace.snapshots.firstOrNull()?.filePath
                            uiState = uiState.copy(
                                loading = false,
                                snapshots = result.workspace.snapshots,
                                selectedMapFilePath = selectedFilePath,
                                pendingCount = result.workspace.pendingOperations.size,
                                unreadableMaps = nextUnreadableMaps,
                                unreadablePendingCounts = unreadablePendingCounts(nextUnreadableMaps, result.workspace.pendingOperations),
                                blockedPendingMaps = nextBlockedMaps,
                                blockedPendingCounts = blockedPendingCounts(nextBlockedMaps, result.workspace.pendingOperations),
                                pendingConflictMaps = nextConflictMaps,
                                pendingConflictCounts = pendingConflictCounts(nextConflictMaps, result.workspace.pendingOperations),
                                conflictResolutionState = if (uiState.conflictResolutionState.targetPath == filePath) {
                                    ConflictResolutionUiState()
                                } else {
                                    uiState.conflictResolutionState
                                },
                                localMapRepairState = uiState.localMapRepairState.clearIfTarget(filePath),
                                statusMessage = resetStatusMessage(
                                    primary = UnreadableMaps.resetDeletedMessage(mapLabel),
                                    discardedPendingCount = result.discardedPendingCount,
                                ),
                            )
                            enqueueBackgroundSyncIfReady(result.workspace.pendingOperations, nextUnreadableMaps, nextBlockedMaps, nextConflictMaps)
                        }
                    }
                }
                .onFailure { error ->
                    uiState = uiState.copy(
                        loading = false,
                        statusMessage = "Could not reset \"$mapLabel\" from GitHub: ${error.message ?: "unknown error"}",
                    )
                }
        }
    }

    fun discardPendingOperationsForBlockedMap(filePath: String) {
        val blocked = uiState.blockedPendingMaps.firstOrNull { it.filePath == filePath }
        if (blocked == null) {
            uiState = uiState.copy(statusMessage = "The blocked queued changes are no longer available.")
            return
        }
        val coordinator = coordinatorOrStatus() ?: return

        viewModelScope.launch {
            uiState = uiState.copy(loading = true, statusMessage = "Discarding queued changes for \"${blocked.mapName}\"...")
            coordinator.discardPendingOperationsForMap(filePath)
                .onSuccess { result ->
                    val nextBlockedMaps = uiState.blockedPendingMaps.filterNot { it.filePath == filePath }
                    val nextConflictMaps = syncPendingConflictMaps(uiState.pendingConflictMaps, result.workspace.pendingOperations)
                    val selectedFilePath = uiState.selectedMapFilePath
                        ?.takeIf { selected -> result.workspace.snapshots.any { it.filePath == selected } }
                        ?: result.workspace.snapshots.firstOrNull()?.filePath
                    uiState = uiState.copy(
                        loading = false,
                        snapshots = result.workspace.snapshots,
                        selectedMapFilePath = selectedFilePath,
                        pendingCount = result.workspace.pendingOperations.size,
                        blockedPendingMaps = nextBlockedMaps,
                        blockedPendingCounts = blockedPendingCounts(nextBlockedMaps, result.workspace.pendingOperations),
                        pendingConflictMaps = nextConflictMaps,
                        pendingConflictCounts = pendingConflictCounts(nextConflictMaps, result.workspace.pendingOperations),
                        unreadablePendingCounts = unreadablePendingCounts(uiState.unreadableMaps, result.workspace.pendingOperations),
                        localMapRepairState = uiState.localMapRepairState.clearIfTarget(filePath),
                        conflictResolutionState = if (uiState.conflictResolutionState.targetPath == filePath) {
                            ConflictResolutionUiState()
                        } else {
                            uiState.conflictResolutionState
                        },
                        statusMessage = BlockedPendingMaps.discardStatusMessage(blocked, result.discardedPendingCount),
                    )
                    enqueueBackgroundSyncIfReady(
                        result.workspace.pendingOperations,
                        uiState.unreadableMaps,
                        nextBlockedMaps,
                        nextConflictMaps,
                    )
                }
                .onFailure { error ->
                    uiState = uiState.copy(
                        loading = false,
                        statusMessage = "Could not discard queued changes for \"${blocked.mapName}\": ${error.message ?: "unknown error"}",
                    )
                }
        }
    }

    fun openPendingConflictResolver(filePath: String) {
        val conflict = uiState.pendingConflictMaps.firstOrNull { it.filePath == filePath }
        if (conflict == null) {
            uiState = uiState.copy(statusMessage = "The conflict is no longer available.")
            return
        }
        val coordinator = coordinatorOrStatus() ?: return
        viewModelScope.launch {
            uiState = uiState.copy(
                conflictResolutionState = ConflictResolutionUiState(
                    targetPath = conflict.filePath,
                    mapName = conflict.mapName,
                    loading = true,
                ),
                statusMessage = "Loading conflict for \"${conflict.mapName}\"...",
            )
            coordinator.prepareConflictResolution(conflict.filePath)
                .onSuccess { draft ->
                    uiState = uiState.copy(
                        conflictResolutionState = ConflictResolutionUiState(
                            targetPath = draft.filePath,
                            mapName = draft.mapName,
                            loading = false,
                            remoteDocument = draft.remoteDocument,
                            remoteRevision = draft.remoteRevision,
                            localDocument = draft.localDocument,
                            items = draft.items.map { item -> item.toUiItem() },
                        ),
                        statusMessage = "Loaded conflict for \"${draft.mapName}\".",
                    )
                }
                .onFailure { error ->
                    uiState = uiState.copy(
                        conflictResolutionState = uiState.conflictResolutionState.copy(
                            loading = false,
                            errorMessage = error.message ?: "Could not load conflict details.",
                        ),
                        statusMessage = error.message ?: "Could not load conflict details.",
                    )
                }
        }
    }

    fun setConflictResolutionChoice(pendingOperationId: String, choice: PendingConflictResolution) {
        val state = uiState.conflictResolutionState
        uiState = uiState.copy(
            conflictResolutionState = state.copy(
                items = state.items.map { item ->
                    if (item.pendingOperationId == pendingOperationId) item.copy(choice = choice) else item
                },
                errorMessage = "",
            ),
        )
    }

    fun closeConflictResolution() {
        uiState = uiState.copy(conflictResolutionState = ConflictResolutionUiState())
    }

    fun acceptConflictResolution() {
        val state = uiState.conflictResolutionState
        val remoteDocument = state.remoteDocument
        if (!conflictResolutionCanAccept(state) || remoteDocument == null) {
            uiState = uiState.copy(
                conflictResolutionState = state.copy(errorMessage = "Choose local or remote for every queued change."),
            )
            return
        }
        val coordinator = coordinatorOrStatus() ?: return
        viewModelScope.launch {
            uiState = uiState.copy(
                conflictResolutionState = state.copy(loading = true, errorMessage = ""),
                statusMessage = "Saving conflict resolution...",
            )
            val choices = state.items.associate { item -> item.pendingOperationId to requireNotNull(item.choice) }
            coordinator.resolveConflict(
                filePath = state.targetPath,
                mapName = state.mapName,
                remoteDocument = remoteDocument,
                remoteRevision = state.remoteRevision,
                choices = choices,
            ).onSuccess { result ->
                val nextConflictMaps = syncPendingConflictMaps(
                    existing = uiState.pendingConflictMaps.filterNot { it.filePath == state.targetPath },
                    pendingOperations = result.workspace.pendingOperations,
                )
                val nextBlockedMaps = syncBlockedPendingMaps(uiState.blockedPendingMaps, result.workspace.pendingOperations)
                val selectedFilePath = uiState.selectedMapFilePath
                    ?.takeIf { selected -> result.workspace.snapshots.any { it.filePath == selected } }
                    ?: result.snapshot.filePath
                container.preferencesStore.recordSyncSuccess(PendingConflicts.resolvedStatusMessage(PendingConflicts.build(state.targetPath, state.mapName)))
                uiState = uiState.copy(
                    loading = false,
                    snapshots = result.workspace.snapshots,
                    selectedMapFilePath = selectedFilePath,
                    pendingCount = result.workspace.pendingOperations.size,
                    blockedPendingMaps = nextBlockedMaps,
                    blockedPendingCounts = blockedPendingCounts(nextBlockedMaps, result.workspace.pendingOperations),
                    pendingConflictMaps = nextConflictMaps,
                    pendingConflictCounts = pendingConflictCounts(nextConflictMaps, result.workspace.pendingOperations),
                    unreadablePendingCounts = unreadablePendingCounts(uiState.unreadableMaps, result.workspace.pendingOperations),
                    conflictResolutionState = ConflictResolutionUiState(),
                    statusMessage = PendingConflicts.resolvedStatusMessage(PendingConflicts.build(state.targetPath, state.mapName)),
                )
                enqueueBackgroundSyncIfReady(
                    result.workspace.pendingOperations,
                    uiState.unreadableMaps,
                    nextBlockedMaps,
                    nextConflictMaps,
                )
            }.onFailure { error ->
                uiState = uiState.copy(
                    loading = false,
                    conflictResolutionState = uiState.conflictResolutionState.copy(
                        loading = false,
                        errorMessage = error.message ?: "Failed to save conflict resolution. Try again.",
                    ),
                    statusMessage = error.message ?: "Failed to save conflict resolution.",
                )
            }
        }
    }

    fun deleteAttachment(filePath: String, nodeId: String, attachmentId: String, expectedRevision: String? = null) {
        val snapshot = snapshotOrStatus(filePath) ?: return
        val record = MapQueries.findNode(snapshot.document, nodeId)
        if (record == null) {
            uiState = uiState.copy(statusMessage = "The selected node is no longer loaded.")
            return
        }
        if (!record.node.canEditText) {
            uiState = uiState.copy(statusMessage = "Attachments are not supported for this node.")
            return
        }
        val attachment = record.node.metadata?.attachments.orEmpty()
            .firstOrNull { it.id == attachmentId }
        if (attachment == null) {
            uiState = uiState.copy(statusMessage = "The selected attachment is no longer available.")
            return
        }
        if (attachment.relativePath.isBlank()) {
            uiState = uiState.copy(statusMessage = "Attachment file path is missing.")
            return
        }
        val connection = activeConnectionOrStatus() ?: return
        val targetKey = attachmentDeleteTargetKey(filePath, nodeId, attachmentId)
        val displayName = attachment.displayName.ifBlank { attachment.relativePath }
        val commitMessage = CommitMessages.attachmentRemove(snapshot.mapName, displayName)

        viewModelScope.launch {
            uiState = uiState.copy(
                attachmentDeleteState = AttachmentDeleteUiState(targetKey = targetKey, deleting = true),
                statusMessage = "Deleting attachment...",
            )

            container.createMindMapService(connection.settings, connection.token)
                .deleteAttachment(
                    nodeId = nodeId,
                    relativePath = attachment.relativePath,
                    expectedRevision = expectedRevision?.takeIf { it.isNotBlank() },
                    commitMessage = commitMessage,
                )
                .onFailure { error ->
                    val message = error.message ?: "Could not delete attachment file. Try again."
                    uiState = uiState.copy(
                        attachmentDeleteState = AttachmentDeleteUiState(targetKey = targetKey, errorMessage = message),
                        statusMessage = message,
                    )
                    return@launch
                }

            val timestamp = ClockProvider.nowIsoSeconds()
            val mutation = MapMutation.RemoveAttachment(
                filePath = filePath,
                nodeId = nodeId,
                attachmentId = attachmentId,
                timestamp = timestamp,
                commitMessage = commitMessage,
            )
            enqueueMutationAndUpdate(mutation, successMessage = "Queued attachment removal.")
                .onSuccess {
                    val currentRequest = uiState.attachmentViewerState.request
                    val shouldCloseViewer = currentRequest?.filePath == filePath &&
                        currentRequest.nodeId == nodeId &&
                        currentRequest.attachment.id == attachmentId
                    uiState = uiState.copy(
                        attachmentDeleteState = AttachmentDeleteUiState(),
                        attachmentViewerState = if (shouldCloseViewer) AttachmentViewerUiState() else uiState.attachmentViewerState,
                    )
                }
                .onFailure { error ->
                    val message = error.message ?: "Could not queue attachment removal."
                    uiState = uiState.copy(
                        attachmentDeleteState = AttachmentDeleteUiState(targetKey = targetKey, errorMessage = message),
                        statusMessage = message,
                    )
                }
        }
    }

    fun closeAttachmentViewer() {
        uiState = uiState.copy(attachmentViewerState = AttachmentViewerUiState())
    }

    private fun bindWorkspace(scope: String) {
        if (workspaceScope == scope && workspaceJob != null) return
        workspaceScope = scope
        workspaceJob?.cancel()
        workspaceJob = viewModelScope.launch {
            container.localStore.observeWorkspace(scope).collect { workspace ->
                val selectedFilePath = uiState.selectedMapFilePath
                    ?.takeIf { filePath -> workspace.snapshots.any { it.filePath == filePath } }
                    ?: workspace.snapshots.firstOrNull()?.filePath
                val nextBlockedMaps = syncBlockedPendingMaps(uiState.blockedPendingMaps, workspace.pendingOperations)
                val nextConflictMaps = syncPendingConflictMaps(uiState.pendingConflictMaps, workspace.pendingOperations)
                uiState = uiState.copy(
                    snapshots = workspace.snapshots,
                    selectedMapFilePath = selectedFilePath,
                    pendingCount = workspace.pendingOperations.size,
                    unreadablePendingCounts = unreadablePendingCounts(uiState.unreadableMaps, workspace.pendingOperations),
                    blockedPendingMaps = nextBlockedMaps,
                    blockedPendingCounts = blockedPendingCounts(nextBlockedMaps, workspace.pendingOperations),
                    pendingConflictMaps = nextConflictMaps,
                    pendingConflictCounts = pendingConflictCounts(nextConflictMaps, workspace.pendingOperations),
                )
                if (
                    workspace.pendingOperations.isNotEmpty() &&
                    !hasPausedPendingOperation(uiState.unreadableMaps, nextBlockedMaps, nextConflictMaps, workspace.pendingOperations)
                ) {
                    FocusSyncWorker.enqueue(getApplication<Application>())
                }
            }
        }
    }

    private fun enqueueBackgroundSyncIfReady(
        pendingOperations: List<PendingMapOperation>,
        unreadableMaps: List<UnreadableMapEntry>,
        blockedPendingMaps: List<BlockedPendingMapEntry>,
        pendingConflictMaps: List<PendingConflictMapEntry>,
    ) {
        if (
            pendingOperations.isNotEmpty() &&
            !hasPausedPendingOperation(unreadableMaps, blockedPendingMaps, pendingConflictMaps, pendingOperations)
        ) {
            FocusSyncWorker.enqueue(getApplication<Application>())
        }
    }

    private fun enqueueMutation(mutation: MapMutation, successMessage: String) {
        viewModelScope.launch {
            enqueueMutationAndUpdate(mutation, successMessage)
        }
    }

    private suspend fun enqueueMutationAndUpdate(mutation: MapMutation, successMessage: String): Result<Unit> {
        val coordinator = coordinatorOrStatus()
            ?: return Result.failure(IllegalStateException(uiState.statusMessage))
        uiState = uiState.copy(statusMessage = "Saving locally...")
        return coordinator.enqueueMutation(mutation)
            .fold(
                onSuccess = { workspace ->
                    val canRunBackgroundSync = !hasPausedPendingOperation(
                        uiState.unreadableMaps,
                        uiState.blockedPendingMaps,
                        uiState.pendingConflictMaps,
                        workspace.pendingOperations,
                    )
                    uiState = uiState.copy(
                        snapshots = workspace.snapshots,
                        pendingCount = workspace.pendingOperations.size,
                        selectedMapFilePath = mutation.resultFilePath(),
                        unreadablePendingCounts = unreadablePendingCounts(uiState.unreadableMaps, workspace.pendingOperations),
                        blockedPendingCounts = blockedPendingCounts(uiState.blockedPendingMaps, workspace.pendingOperations),
                        pendingConflictCounts = pendingConflictCounts(uiState.pendingConflictMaps, workspace.pendingOperations),
                        statusMessage = if (canRunBackgroundSync) {
                            "$successMessage Sync will run in the background."
                        } else {
                            "$successMessage Queued behind map recovery."
                        },
                    )
                    if (canRunBackgroundSync) {
                        FocusSyncWorker.enqueue(getApplication<Application>())
                    }
                    Result.success(Unit)
                },
                onFailure = { error ->
                    uiState = uiState.copy(statusMessage = error.message ?: "Could not save the change.")
                    Result.failure(error)
                },
            )
    }

    private suspend fun uploadPreparedAttachmentBytes(
        snapshot: MapSnapshot,
        connection: ActiveConnection,
        nodeId: String,
        attachment: NodeAttachment,
        bytes: ByteArray,
        targetKey: String,
    ) {
        val commitMessage = CommitMessages.attachmentAdd(snapshot.mapName, attachment.displayName)
        container.createMindMapService(connection.settings, connection.token)
            .uploadAttachment(
                nodeId = nodeId,
                relativePath = attachment.relativePath,
                base64Content = Base64.getEncoder().encodeToString(bytes),
                commitMessage = commitMessage,
            )
            .onFailure { error ->
                val message = error.message ?: "Upload failed. Check your connection and try again."
                uiState = uiState.copy(
                    attachmentUploadState = AttachmentUploadUiState(targetKey = targetKey, errorMessage = message),
                    statusMessage = message,
                )
                return
            }

        val timestamp = ClockProvider.nowIsoSeconds()
        val mutation = MapMutation.AddAttachment(
            filePath = snapshot.filePath,
            nodeId = nodeId,
            attachment = attachment.copy(createdAtUtc = timestamp),
            timestamp = timestamp,
            commitMessage = commitMessage,
        )

        enqueueMutationAndUpdate(mutation, successMessage = "Queued attachment update.")
            .onSuccess {
                uiState = uiState.copy(attachmentUploadState = AttachmentUploadUiState())
            }
            .onFailure { error ->
                val message = error.message ?: "Could not queue attachment update."
                uiState = uiState.copy(
                    attachmentUploadState = AttachmentUploadUiState(targetKey = targetKey, errorMessage = message),
                    statusMessage = message,
                )
            }
    }

    private fun coordinatorOrStatus(): WorkspaceSyncCoordinator? {
        val connection = activeConnectionOrStatus() ?: return null
        return container.createWorkspaceSyncCoordinator(connection.settings, connection.token)
    }

    private fun activeConnectionOrStatus(): ActiveConnection? {
        val settings = uiState.repoSettings
        val token = container.tokenStore.getToken(settings.scope)
        return when {
            !settings.isComplete -> {
                uiState = uiState.copy(statusMessage = "Repository owner, repository name, and branch are required.")
                null
            }
            token.isNullOrBlank() -> {
                uiState = uiState.copy(statusMessage = "A GitHub personal access token is required.")
                null
            }
            else -> ActiveConnection(settings, token)
        }
    }

    private fun snapshotOrStatus(filePath: String): MapSnapshot? {
        val snapshot = uiState.snapshots.firstOrNull { it.filePath == filePath }
        if (snapshot == null) {
            uiState = uiState.copy(statusMessage = "The selected map is no longer loaded.")
        }
        return snapshot
    }

    private suspend fun readSelectedAttachment(uri: Uri): Result<SelectedAttachmentUpload> =
        withContext(Dispatchers.IO) {
            runCatching {
                readSelectedAttachment(
                    contentResolver = getApplication<Application>().contentResolver,
                    uri = uri,
                    attachmentId = UUID.randomUUID().toString(),
                    createdAtUtc = ClockProvider.nowIsoSeconds(),
                )
            }
        }

    private data class ActiveConnection(val settings: RepoSettings, val token: String)
}

private data class SelectedAttachmentUpload(
    val attachment: NodeAttachment,
    val bytes: ByteArray,
)

private data class SelectedAttachmentInfo(
    val displayName: String,
    val mediaType: String,
    val sizeBytes: Long?,
)

private fun readSelectedAttachment(
    contentResolver: ContentResolver,
    uri: Uri,
    attachmentId: String,
    createdAtUtc: String,
): SelectedAttachmentUpload {
    val info = selectedAttachmentInfo(contentResolver, uri)
    AttachmentUploads.validationError(info.sizeBytes, info.mediaType, info.displayName)?.let { error(it) }

    val bytes = readBytesWithinLimit(contentResolver, uri)
    AttachmentUploads.validationError(bytes.size.toLong(), info.mediaType, info.displayName)?.let { error(it) }

    val mediaType = AttachmentUploads.normalizedMediaType(info.mediaType, info.displayName)
    val attachment = AttachmentUploads.attachment(
        attachmentId = attachmentId,
        displayName = info.displayName,
        mediaType = mediaType,
        createdAtUtc = createdAtUtc,
    )
    return SelectedAttachmentUpload(
        attachment = attachment,
        bytes = bytes,
    )
}

private fun selectedAttachmentInfo(contentResolver: ContentResolver, uri: Uri): SelectedAttachmentInfo {
    var displayName: String? = uri.lastPathSegment
    var sizeBytes: Long? = null
    contentResolver.query(uri, arrayOf(OpenableColumns.DISPLAY_NAME, OpenableColumns.SIZE), null, null, null)
        ?.use { cursor ->
            if (cursor.moveToFirst()) {
                val nameIndex = cursor.getColumnIndex(OpenableColumns.DISPLAY_NAME)
                if (nameIndex >= 0 && !cursor.isNull(nameIndex)) {
                    displayName = cursor.getString(nameIndex)
                }
                val sizeIndex = cursor.getColumnIndex(OpenableColumns.SIZE)
                if (sizeIndex >= 0 && !cursor.isNull(sizeIndex)) {
                    sizeBytes = cursor.getLong(sizeIndex).takeIf { it >= 0 }
                }
            }
        }
    val cleanDisplayName = AttachmentUploads.displayName(displayName)
    return SelectedAttachmentInfo(
        displayName = cleanDisplayName,
        mediaType = AttachmentUploads.normalizedMediaType(contentResolver.getType(uri), cleanDisplayName),
        sizeBytes = sizeBytes,
    )
}

private fun readBytesWithinLimit(contentResolver: ContentResolver, uri: Uri): ByteArray {
    val output = ByteArrayOutputStream()
    val buffer = ByteArray(DEFAULT_BUFFER_SIZE)
    var totalBytes = 0L
    contentResolver.openInputStream(uri)?.use { input ->
        while (true) {
            val read = input.read(buffer)
            if (read < 0) break
            totalBytes += read.toLong()
            if (totalBytes > AttachmentUploads.MaxBytes) {
                error(AttachmentUploads.tooLargeMessage(totalBytes))
            }
            output.write(buffer, 0, read)
        }
    } ?: error("Could not open the selected file.")
    return output.toByteArray()
}

private fun FocusUiState.withWorkspaceLoadResult(succeeded: Boolean): FocusUiState =
    copy(
        workspaceLoadResultVersion = workspaceLoadResultVersion + 1,
        workspaceLoadSucceeded = succeeded,
    )
