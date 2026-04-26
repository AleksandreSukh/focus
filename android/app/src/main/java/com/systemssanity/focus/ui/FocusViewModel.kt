package com.systemssanity.focus.ui

import android.app.Application
import androidx.compose.runtime.getValue
import androidx.compose.runtime.mutableStateOf
import androidx.compose.runtime.setValue
import androidx.lifecycle.AndroidViewModel
import androidx.lifecycle.viewModelScope
import com.systemssanity.focus.FocusApplication
import com.systemssanity.focus.data.local.RepoSettings
import com.systemssanity.focus.domain.maps.CommitMessages
import com.systemssanity.focus.domain.maps.MapMutation
import com.systemssanity.focus.domain.maps.MapQueries
import com.systemssanity.focus.domain.maps.TaskFilter
import com.systemssanity.focus.domain.model.ClockProvider
import com.systemssanity.focus.domain.model.MapSnapshot
import com.systemssanity.focus.domain.model.TaskState
import com.systemssanity.focus.domain.sync.FocusSyncWorker
import com.systemssanity.focus.domain.sync.WorkspaceSyncCoordinator
import kotlinx.coroutines.Job
import kotlinx.coroutines.flow.first
import kotlinx.coroutines.launch
import java.util.UUID

data class FocusUiState(
    val repoSettings: RepoSettings = RepoSettings(),
    val tokenPresent: Boolean = false,
    val snapshots: List<MapSnapshot> = emptyList(),
    val selectedMapFilePath: String? = null,
    val pendingCount: Int = 0,
    val taskFilter: TaskFilter = TaskFilter.Open,
    val statusMessage: String = "Connection required.",
    val loading: Boolean = false,
)

class FocusViewModel(application: Application) : AndroidViewModel(application) {
    private val container = (application as FocusApplication).appContainer
    private var workspaceJob: Job? = null
    private var workspaceScope: String? = null

    var uiState by mutableStateOf(FocusUiState())
        private set

    init {
        viewModelScope.launch {
            container.preferencesStore.repoSettings.collect { settings ->
                uiState = uiState.copy(
                    repoSettings = settings,
                    tokenPresent = container.tokenStore.getToken(settings.scope) != null,
                    statusMessage = if (settings.isComplete) "Ready to load workspace." else "Connection required.",
                )
                bindWorkspace(settings.scope)
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
            uiState = uiState.copy(statusMessage = "Repository owner, repository name, and branch are required.")
            return
        }
        if (token == null) {
            uiState = uiState.copy(statusMessage = "A GitHub personal access token is required.")
            return
        }

        viewModelScope.launch {
            uiState = uiState.copy(loading = true, statusMessage = "Loading maps from GitHub...")
            val coordinator = container.createWorkspaceSyncCoordinator(settings, token)
            coordinator.loadWorkspace(forceRefresh)
                .onSuccess { workspace ->
                    val selectedFilePath = uiState.selectedMapFilePath
                        ?.takeIf { filePath -> workspace.snapshots.any { it.filePath == filePath } }
                        ?: workspace.snapshots.firstOrNull()?.filePath
                    uiState = uiState.copy(
                        snapshots = workspace.snapshots,
                        selectedMapFilePath = selectedFilePath,
                        pendingCount = workspace.pendingOperations.size,
                        tokenPresent = true,
                        loading = false,
                        statusMessage = "Loaded ${workspace.snapshots.size} map${if (workspace.snapshots.size == 1) "" else "s"}.",
                    )
                }
                .onFailure { error ->
                    uiState = uiState.copy(
                        loading = false,
                        statusMessage = error.message ?: "Could not load maps.",
                    )
                }
        }
    }

    fun setTaskFilter(filter: TaskFilter) {
        uiState = uiState.copy(taskFilter = filter)
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
            val filePath = buildMapFilePath(connection.settings.repoPath, trimmedName)
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
            if (current.pendingOperations.any { it.operation.filePath == filePath }) {
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
                .onSuccess { workspace ->
                    container.preferencesStore.recordSyncSuccess(
                        "Synced queued changes. Pending operations: ${workspace.pendingOperations.size}.",
                    )
                    uiState = uiState.copy(
                        loading = false,
                        snapshots = workspace.snapshots,
                        pendingCount = workspace.pendingOperations.size,
                        statusMessage = "Synced queued changes.",
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

    private fun bindWorkspace(scope: String) {
        if (workspaceScope == scope && workspaceJob != null) return
        workspaceScope = scope
        workspaceJob?.cancel()
        workspaceJob = viewModelScope.launch {
            container.localStore.observeWorkspace(scope).collect { workspace ->
                val selectedFilePath = uiState.selectedMapFilePath
                    ?.takeIf { filePath -> workspace.snapshots.any { it.filePath == filePath } }
                    ?: workspace.snapshots.firstOrNull()?.filePath
                uiState = uiState.copy(
                    snapshots = workspace.snapshots,
                    selectedMapFilePath = selectedFilePath,
                    pendingCount = workspace.pendingOperations.size,
                )
                if (workspace.pendingOperations.isNotEmpty()) {
                    FocusSyncWorker.enqueue(getApplication<Application>())
                }
            }
        }
    }

    private fun enqueueMutation(mutation: MapMutation, successMessage: String) {
        val coordinator = coordinatorOrStatus() ?: return
        viewModelScope.launch {
            uiState = uiState.copy(statusMessage = "Saving locally...")
            coordinator.enqueueMutation(mutation)
                .onSuccess { workspace ->
                    uiState = uiState.copy(
                        snapshots = workspace.snapshots,
                        pendingCount = workspace.pendingOperations.size,
                        selectedMapFilePath = mutation.filePath,
                        statusMessage = "$successMessage Sync will run in the background.",
                    )
                    FocusSyncWorker.enqueue(getApplication<Application>())
                }
                .onFailure { error ->
                    uiState = uiState.copy(statusMessage = error.message ?: "Could not save the change.")
                }
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

    private data class ActiveConnection(val settings: RepoSettings, val token: String)

    private fun buildMapFilePath(repoPath: String, mapName: String): String {
        val directory = repoPath.trim('/')
        val fileName = sanitizeMapFileName(mapName)
        return if (directory.isBlank()) fileName else "$directory/$fileName"
    }

    private fun sanitizeMapFileName(mapName: String): String {
        val sanitized = mapName
            .trim()
            .replace(Regex("[\\\\/:*?\"<>|]+"), "-")
            .replace(Regex("\\s+"), " ")
            .trim('.', ' ')
            .ifBlank { "Untitled" }
        val withoutExtension = if (sanitized.endsWith(".json", ignoreCase = true)) {
            sanitized.dropLast(5)
        } else {
            sanitized
        }.ifBlank { "Untitled" }
        return "$withoutExtension.json"
    }
}
