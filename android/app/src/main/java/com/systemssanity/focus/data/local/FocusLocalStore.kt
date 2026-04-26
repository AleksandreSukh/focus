package com.systemssanity.focus.data.local

import com.systemssanity.focus.domain.model.MapSnapshot
import kotlinx.coroutines.flow.Flow
import kotlinx.coroutines.flow.MutableStateFlow
import kotlinx.coroutines.flow.map
import kotlinx.coroutines.flow.update

interface FocusLocalStore {
    fun observeWorkspace(scope: String): Flow<CachedWorkspace>
    suspend fun saveWorkspace(scope: String, workspace: CachedWorkspace)
    suspend fun replaceSnapshots(scope: String, snapshots: List<MapSnapshot>)
    suspend fun appendPendingOperation(operation: PendingMapOperation)
    suspend fun removePendingOperation(scope: String, operationId: String)
    suspend fun clearScope(scope: String)
}

class InMemoryFocusLocalStore : FocusLocalStore {
    private val workspaces = MutableStateFlow<Map<String, CachedWorkspace>>(emptyMap())

    override fun observeWorkspace(scope: String): Flow<CachedWorkspace> =
        workspaces.map { it[scope] ?: CachedWorkspace() }

    override suspend fun saveWorkspace(scope: String, workspace: CachedWorkspace) {
        workspaces.update { current -> current + (scope to workspace) }
    }

    override suspend fun replaceSnapshots(scope: String, snapshots: List<MapSnapshot>) {
        workspaces.update { current ->
            current + (scope to (current[scope] ?: CachedWorkspace()).copy(snapshots = snapshots))
        }
    }

    override suspend fun appendPendingOperation(operation: PendingMapOperation) {
        workspaces.update { current ->
            val workspace = current[operation.scope] ?: CachedWorkspace()
            current + (operation.scope to workspace.copy(pendingOperations = workspace.pendingOperations + operation))
        }
    }

    override suspend fun removePendingOperation(scope: String, operationId: String) {
        workspaces.update { current ->
            val workspace = current[scope] ?: CachedWorkspace()
            current + (scope to workspace.copy(pendingOperations = workspace.pendingOperations.filterNot { it.id == operationId }))
        }
    }

    override suspend fun clearScope(scope: String) {
        workspaces.update { it - scope }
    }
}
