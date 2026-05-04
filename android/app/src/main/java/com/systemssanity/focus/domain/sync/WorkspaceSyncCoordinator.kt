package com.systemssanity.focus.domain.sync

import com.systemssanity.focus.data.github.UnreadableMapException
import com.systemssanity.focus.data.local.CachedWorkspace
import com.systemssanity.focus.data.local.FocusLocalStore
import com.systemssanity.focus.data.local.PendingMapOperation
import com.systemssanity.focus.domain.maps.BlockedPendingMapEntry
import com.systemssanity.focus.domain.maps.BlockedPendingMaps
import com.systemssanity.focus.domain.maps.MapMutation
import com.systemssanity.focus.domain.maps.MapMutationEngine
import com.systemssanity.focus.domain.maps.MapMutationRejectedException
import com.systemssanity.focus.domain.maps.MutationApplyResult
import com.systemssanity.focus.domain.maps.PendingConflictMapEntry
import com.systemssanity.focus.domain.maps.PendingConflictResolution
import com.systemssanity.focus.domain.maps.PendingConflicts
import com.systemssanity.focus.domain.maps.UnreadableMapEntry
import com.systemssanity.focus.domain.maps.resultFilePath
import com.systemssanity.focus.domain.maps.touchesFilePath
import com.systemssanity.focus.domain.model.MapSnapshot
import com.systemssanity.focus.domain.model.MindMapDocument
import kotlinx.coroutines.flow.first
import java.util.UUID

class WorkspaceSyncCoordinator(
    private val scope: String,
    private val localStore: FocusLocalStore,
    private val service: MindMapService,
) {
    suspend fun loadWorkspace(forceRefresh: Boolean): Result<WorkspaceLoadResult> =
        service.listMaps(forceRefresh).mapCatching { listed ->
            val remoteSnapshots = listed.snapshots
            val current = currentWorkspace()
            val next = current.copy(snapshots = PendingMapOperations.applyToSnapshots(remoteSnapshots, current.pendingOperations))
            service.hydrateSnapshots(remoteSnapshots)
            localStore.saveWorkspace(scope, next)
            val synced = if (next.pendingOperations.isNotEmpty()) {
                processPendingOperations().getOrThrow()
            } else {
                WorkspacePendingSyncResult(workspace = next)
            }
            val unreadableMaps = (listed.unreadableMaps + listOfNotNull(synced.pausedUnreadableMap))
                .distinctBy { it.filePath }
                .sortedBy { it.fileName.lowercase() }
            WorkspaceLoadResult(
                workspace = synced.workspace,
                unreadableMaps = unreadableMaps,
                blockedPendingMaps = listOfNotNull(synced.blockedPendingMap),
                pendingConflictMaps = listOfNotNull(synced.pendingConflictMap),
            )
        }

    suspend fun enqueueMutation(mutation: MapMutation): Result<CachedWorkspace> =
        runCatching {
            val current = currentWorkspace()
            val snapshot = PendingMapOperations.findSnapshotForOperation(current.snapshots, mutation)
                ?: error("The selected map is no longer available.")
            val applied = MapMutationEngine.apply(snapshot.document, mutation)
            if (applied is MutationApplyResult.Rejected) error(applied.message)
            check(applied is MutationApplyResult.Applied)

            val optimisticSnapshot = PendingMapOperations.snapshotForAppliedMutation(snapshot, mutation, applied.result.document)
            val pending = PendingMapOperation(
                id = UUID.randomUUID().toString(),
                scope = scope,
                operation = mutation,
                enqueuedAtMillis = System.currentTimeMillis(),
            )
            val next = current.copy(
                snapshots = PendingMapOperations.replaceSnapshot(current.snapshots, mutation, optimisticSnapshot),
                pendingOperations = current.pendingOperations + pending,
            )
            localStore.saveWorkspace(scope, next)
            next
        }

    suspend fun processPendingOperations(): Result<WorkspacePendingSyncResult> =
        runCatching {
            var workspace = currentWorkspace()
            while (workspace.pendingOperations.isNotEmpty()) {
                val pending = workspace.pendingOperations.first()
                val saved = try {
                    syncPendingOperation(pending.operation, workspace.snapshots)
                } catch (error: UnreadableMapException) {
                    return@runCatching WorkspacePendingSyncResult(
                        workspace = workspace,
                        pausedUnreadableMap = error.toUnreadableMapEntry(),
                    )
                } catch (error: MapMutationRejectedException) {
                    if (error.code == "NOT_FOUND") {
                        val blocked = buildBlockedPendingResult(workspace, pending.operation)
                        localStore.saveWorkspace(scope, blocked.workspace)
                        return@runCatching blocked
                    }
                    throw error
                } catch (error: Throwable) {
                    if (service.isNotFound(error)) {
                        val blocked = buildBlockedPendingResult(workspace, pending.operation)
                        localStore.saveWorkspace(scope, blocked.workspace)
                        return@runCatching blocked
                    }
                    if (service.isStaleState(error)) {
                        return@runCatching WorkspacePendingSyncResult(
                            workspace = workspace,
                            pendingConflictMap = buildPendingConflictEntry(workspace, pending.operation),
                        )
                    }
                    throw error
                }
                workspace = workspace.copy(
                    snapshots = PendingMapOperations.replaceSnapshot(workspace.snapshots, pending.operation, saved),
                    pendingOperations = workspace.pendingOperations.drop(1),
                )
                localStore.saveWorkspace(scope, workspace)
            }
            WorkspacePendingSyncResult(workspace = workspace)
        }

    suspend fun discardPendingOperationsForMap(filePath: String): Result<WorkspaceDiscardPendingResult> =
        runCatching {
            val current = currentWorkspace()
            val removed = current.pendingOperations.filter { it.operation.touchesFilePath(filePath) }
            if (removed.isEmpty()) {
                return@runCatching WorkspaceDiscardPendingResult(
                    workspace = current,
                    discardedPendingCount = 0,
                )
            }

            val baselineSnapshots = loadDiscardBaselines(removed.map { it.operation })
            val pathsToReplace = removed
                .flatMap { pending -> pending.operation.touchedFilePaths() }
                .toSet()
            val nextSnapshots = (current.snapshots.filterNot { it.filePath in pathsToReplace } + baselineSnapshots)
                .distinctBy { it.filePath }
                .sortedBy { it.fileName.lowercase() }
            val next = current.copy(
                snapshots = nextSnapshots,
                pendingOperations = current.pendingOperations.filterNot { it.operation.touchesFilePath(filePath) },
            )
            localStore.saveWorkspace(scope, next)
            WorkspaceDiscardPendingResult(
                workspace = next,
                discardedPendingCount = removed.size,
            )
        }

    suspend fun prepareConflictResolution(filePath: String): Result<WorkspaceConflictResolutionDraft> =
        runCatching {
            val current = currentWorkspace()
            val pendingForMap = pendingOperationsForConflict(current.pendingOperations, filePath)
            if (pendingForMap.isEmpty()) error("There are no queued changes for this map.")

            val remote = service.loadMap(filePath, forceRefresh = true).getOrThrow()
            val local = current.snapshots.firstOrNull { it.filePath == filePath }
                ?: pendingForMap.asSequence()
                    .mapNotNull { pending -> PendingMapOperations.findSnapshotForOperation(current.snapshots, pending.operation) }
                    .firstOrNull()
                ?: remote
            val mapName = remote.mapName.ifBlank { local.mapName }
            WorkspaceConflictResolutionDraft(
                filePath = filePath,
                mapName = mapName,
                localDocument = local.document,
                remoteDocument = remote.document,
                remoteRevision = remote.revision,
                items = pendingForMap.map { pending ->
                    WorkspaceConflictResolutionItem(
                        pendingOperationId = pending.id,
                        operation = pending.operation,
                        description = PendingConflicts.describeOperation(pending.operation, mapName),
                    )
                },
            )
        }

    suspend fun resolveConflict(
        filePath: String,
        mapName: String,
        remoteDocument: MindMapDocument,
        remoteRevision: String,
        choices: Map<String, PendingConflictResolution>,
    ): Result<WorkspaceConflictResolutionResult> =
        runCatching {
            val current = currentWorkspace()
            val pendingForMap = pendingOperationsForConflict(current.pendingOperations, filePath)
            if (pendingForMap.isEmpty()) error("There are no queued changes for this map.")
            val missingChoice = pendingForMap.firstOrNull { pending -> choices[pending.id] == null }
            if (missingChoice != null) error("Choose local or remote for every queued change.")

            var mergedDocument = remoteDocument
            pendingForMap.forEach { pending ->
                if (choices[pending.id] == PendingConflictResolution.Local) {
                    val applied = MapMutationEngine.apply(mergedDocument, pending.operation)
                    if (applied is MutationApplyResult.Rejected) {
                        throw MapMutationRejectedException(applied.code, applied.message)
                    }
                    check(applied is MutationApplyResult.Applied)
                    mergedDocument = applied.result.document
                }
            }

            val saved = service.saveResolved(filePath, mapName, mergedDocument, remoteRevision).getOrThrow()
            val touchedPaths = (pendingForMap.flatMap { it.operation.touchedFilePaths() } + filePath).toSet()
            touchedPaths.forEach { touchedPath ->
                if (touchedPath != saved.filePath) service.removeCachedSnapshot(touchedPath)
            }
            service.replaceCachedSnapshot(saved)
            val next = current.copy(
                snapshots = (current.snapshots.filterNot { it.filePath in touchedPaths } + saved)
                    .distinctBy { it.filePath }
                    .sortedBy { it.fileName.lowercase() },
                pendingOperations = current.pendingOperations.filterNot { pending ->
                    touchedPaths.any { path -> pending.operation.touchesFilePath(path) }
                },
            )
            localStore.saveWorkspace(scope, next)
            WorkspaceConflictResolutionResult(
                workspace = next,
                snapshot = saved,
                resolvedPendingCount = pendingForMap.size,
            )
        }

    suspend fun resetMapToRemote(filePath: String): Result<WorkspaceMapResetResult> =
        runCatching {
            val current = currentWorkspace()
            val discardedPendingCount = current.pendingOperations.count { it.operation.touchesFilePath(filePath) }
            val loaded = service.loadMap(filePath, forceRefresh = true)
                .getOrElse { error ->
                    if (error is UnreadableMapException) {
                        return@runCatching WorkspaceMapResetResult.StillUnreadable(
                            entry = error.toUnreadableMapEntry(),
                            workspace = current,
                        )
                    }
                    if (service.isNotFound(error)) {
                        service.removeCachedSnapshot(filePath)
                        val next = current.copy(
                            snapshots = current.snapshots.filterNot { it.filePath == filePath },
                            pendingOperations = current.pendingOperations.filterNot { it.operation.touchesFilePath(filePath) },
                        )
                        localStore.saveWorkspace(scope, next)
                        return@runCatching WorkspaceMapResetResult.Deleted(
                            filePath = filePath,
                            discardedPendingCount = discardedPendingCount,
                            workspace = next,
                        )
                    }
                    throw error
                }

            val next = current.copy(
                snapshots = (current.snapshots.filterNot { it.filePath == filePath } + loaded)
                    .sortedBy { it.fileName.lowercase() },
                pendingOperations = current.pendingOperations.filterNot { it.operation.touchesFilePath(filePath) },
            )
            service.replaceCachedSnapshot(loaded)
            localStore.saveWorkspace(scope, next)
            WorkspaceMapResetResult.Reset(
                snapshot = loaded,
                discardedPendingCount = discardedPendingCount,
                workspace = next,
            )
        }

    private suspend fun currentWorkspace(): CachedWorkspace =
        localStore.observeWorkspace(scope).first()

    private suspend fun syncPendingOperation(operation: MapMutation, localSnapshots: List<MapSnapshot>): MapSnapshot {
        if (operation is MapMutation.RenameMap) {
            val latest = service.loadMap(operation.filePath, forceRefresh = true).getOrThrow()
            val applied = MapMutationEngine.apply(latest.document, operation)
            if (applied is MutationApplyResult.Rejected) {
                throw MapMutationRejectedException(applied.code, applied.message)
            }
            check(applied is MutationApplyResult.Applied)
            return service.renameMap(
                oldFilePath = operation.filePath,
                newFilePath = operation.newFilePath,
                document = applied.result.document,
                oldRevision = latest.revision,
                commitMessage = operation.commitMessage,
            ).getOrThrow()
        }

        val firstAttempt = service.applyMutation(operation.filePath, operation)
        if (firstAttempt.isSuccess) {
            return firstAttempt.getOrThrow()
        }

        val firstError = firstAttempt.exceptionOrNull()
        if (firstError is UnreadableMapException) {
            val localSnapshot = PendingMapOperations.findSnapshotForOperation(localSnapshots, operation)
            if (localSnapshot != null) {
                service.replaceCachedSnapshot(localSnapshot)
                return service.applyMutation(operation.filePath, operation).getOrThrow()
            }
        }
        throw firstError ?: IllegalStateException("Could not sync pending operation.")
    }

    private fun buildBlockedPendingResult(
        workspace: CachedWorkspace,
        operation: MapMutation,
    ): WorkspacePendingSyncResult {
        val baseline = service.cachedSnapshots()
            .firstOrNull { snapshot -> operation.touchedFilePaths().contains(snapshot.filePath) }
        val nextWorkspace = if (baseline == null) {
            workspace
        } else {
            workspace.copy(
                snapshots = PendingMapOperations.replaceSnapshot(workspace.snapshots, operation, baseline),
            )
        }
        val displayPath = operation.resultFilePath()
        return WorkspacePendingSyncResult(
            workspace = nextWorkspace,
            blockedPendingMap = BlockedPendingMaps.build(displayPath, baseline),
        )
    }

    private fun buildPendingConflictEntry(
        workspace: CachedWorkspace,
        operation: MapMutation,
    ): PendingConflictMapEntry {
        val localSnapshot = PendingMapOperations.findSnapshotForOperation(workspace.snapshots, operation)
        val mapName = if (operation is MapMutation.RenameMap) "" else localSnapshot?.mapName.orEmpty()
        return PendingConflicts.build(operation.filePath, mapName)
    }

    private suspend fun loadDiscardBaselines(operations: List<MapMutation>): List<MapSnapshot> {
        val renameBaselinePaths = operations
            .filterIsInstance<MapMutation.RenameMap>()
            .map { it.filePath }
        val baselinePaths = (renameBaselinePaths.ifEmpty { operations.map { it.filePath } }).distinct()
        return baselinePaths.map { path ->
            service.cachedSnapshots().firstOrNull { it.filePath == path }
                ?: service.loadMap(path, forceRefresh = true).getOrThrow()
        }
    }
}

private fun pendingOperationsForConflict(
    pendingOperations: List<PendingMapOperation>,
    filePath: String,
): List<PendingMapOperation> {
    val first = pendingOperations.firstOrNull { pending -> pending.operation.touchesFilePath(filePath) }
        ?: return emptyList()
    val conflictPaths = first.operation.touchedFilePaths()
    return pendingOperations.filter { pending ->
        conflictPaths.any { path -> pending.operation.touchesFilePath(path) }
    }
}

data class WorkspaceLoadResult(
    val workspace: CachedWorkspace,
    val unreadableMaps: List<UnreadableMapEntry> = emptyList(),
    val blockedPendingMaps: List<BlockedPendingMapEntry> = emptyList(),
    val pendingConflictMaps: List<PendingConflictMapEntry> = emptyList(),
)

data class WorkspacePendingSyncResult(
    val workspace: CachedWorkspace,
    val blockedPendingMap: BlockedPendingMapEntry? = null,
    val pausedUnreadableMap: UnreadableMapEntry? = null,
    val pendingConflictMap: PendingConflictMapEntry? = null,
)

data class WorkspaceDiscardPendingResult(
    val workspace: CachedWorkspace,
    val discardedPendingCount: Int,
)

data class WorkspaceConflictResolutionDraft(
    val filePath: String,
    val mapName: String,
    val localDocument: MindMapDocument,
    val remoteDocument: MindMapDocument,
    val remoteRevision: String,
    val items: List<WorkspaceConflictResolutionItem>,
)

data class WorkspaceConflictResolutionItem(
    val pendingOperationId: String,
    val operation: MapMutation,
    val description: String,
)

data class WorkspaceConflictResolutionResult(
    val workspace: CachedWorkspace,
    val snapshot: MapSnapshot,
    val resolvedPendingCount: Int,
)

sealed class WorkspaceMapResetResult {
    data class Reset(
        val snapshot: MapSnapshot,
        val discardedPendingCount: Int,
        val workspace: CachedWorkspace,
    ) : WorkspaceMapResetResult()

    data class StillUnreadable(
        val entry: UnreadableMapEntry,
        val workspace: CachedWorkspace,
    ) : WorkspaceMapResetResult()

    data class Deleted(
        val filePath: String,
        val discardedPendingCount: Int,
        val workspace: CachedWorkspace,
    ) : WorkspaceMapResetResult()
}

private fun MapMutation.touchedFilePaths(): List<String> =
    buildList {
        add(filePath)
        if (this@touchedFilePaths is MapMutation.RenameMap) {
            add(newFilePath)
        }
    }.distinct()
