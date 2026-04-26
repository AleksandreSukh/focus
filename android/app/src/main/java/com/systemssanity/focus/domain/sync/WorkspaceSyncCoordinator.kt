package com.systemssanity.focus.domain.sync

import com.systemssanity.focus.data.local.CachedWorkspace
import com.systemssanity.focus.data.local.FocusLocalStore
import com.systemssanity.focus.data.local.PendingMapOperation
import com.systemssanity.focus.domain.maps.MapMutation
import com.systemssanity.focus.domain.maps.MapMutationEngine
import com.systemssanity.focus.domain.maps.MutationApplyResult
import com.systemssanity.focus.domain.model.MapSnapshot
import kotlinx.coroutines.flow.first
import java.util.UUID

class WorkspaceSyncCoordinator(
    private val scope: String,
    private val localStore: FocusLocalStore,
    private val service: MindMapService,
) {
    suspend fun loadWorkspace(forceRefresh: Boolean): Result<CachedWorkspace> =
        service.listMaps(forceRefresh).mapCatching { remoteSnapshots ->
            val current = currentWorkspace()
            val next = current.copy(snapshots = applyPendingOperations(remoteSnapshots, current.pendingOperations))
            service.hydrateSnapshots(remoteSnapshots)
            localStore.saveWorkspace(scope, next)
            if (next.pendingOperations.isNotEmpty()) {
                processPendingOperations().getOrThrow()
            } else {
                next
            }
        }

    suspend fun enqueueMutation(mutation: MapMutation): Result<CachedWorkspace> =
        runCatching {
            val current = currentWorkspace()
            val snapshot = current.snapshots.firstOrNull { it.filePath == mutation.filePath }
                ?: error("The selected map is no longer available.")
            val applied = MapMutationEngine.apply(snapshot.document, mutation)
            if (applied is MutationApplyResult.Rejected) error(applied.message)
            check(applied is MutationApplyResult.Applied)

            val optimisticSnapshot = snapshot.copy(
                document = applied.result.document,
                loadedAtMillis = System.currentTimeMillis(),
            )
            val pending = PendingMapOperation(
                id = UUID.randomUUID().toString(),
                scope = scope,
                operation = mutation,
                enqueuedAtMillis = System.currentTimeMillis(),
            )
            val next = current.copy(
                snapshots = current.snapshots.replaceSnapshot(optimisticSnapshot),
                pendingOperations = current.pendingOperations + pending,
            )
            localStore.saveWorkspace(scope, next)
            next
        }

    suspend fun processPendingOperations(): Result<CachedWorkspace> =
        runCatching {
            var workspace = currentWorkspace()
            while (workspace.pendingOperations.isNotEmpty()) {
                val pending = workspace.pendingOperations.first()
                service.loadMap(pending.operation.filePath, forceRefresh = true).getOrThrow()
                val saved = service.applyMutation(pending.operation.filePath, pending.operation).getOrThrow()
                workspace = workspace.copy(
                    snapshots = workspace.snapshots.replaceSnapshot(saved),
                    pendingOperations = workspace.pendingOperations.drop(1),
                )
                localStore.saveWorkspace(scope, workspace)
            }
            workspace
        }

    private suspend fun currentWorkspace(): CachedWorkspace =
        localStore.observeWorkspace(scope).first()

    private fun applyPendingOperations(
        snapshots: List<MapSnapshot>,
        pendingOperations: List<PendingMapOperation>,
    ): List<MapSnapshot> {
        var next = snapshots
        pendingOperations.forEach { pending ->
            val snapshot = next.firstOrNull { it.filePath == pending.operation.filePath } ?: return@forEach
            val applied = MapMutationEngine.apply(snapshot.document, pending.operation)
            if (applied is MutationApplyResult.Applied) {
                next = next.replaceSnapshot(snapshot.copy(document = applied.result.document))
            }
        }
        return next
    }

    private fun List<MapSnapshot>.replaceSnapshot(snapshot: MapSnapshot): List<MapSnapshot> {
        val replaced = map { if (it.filePath == snapshot.filePath) snapshot else it }
        return if (any { it.filePath == snapshot.filePath }) replaced else replaced + snapshot
    }
}
