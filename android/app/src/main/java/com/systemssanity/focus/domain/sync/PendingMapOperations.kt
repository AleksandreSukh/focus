package com.systemssanity.focus.domain.sync

import com.systemssanity.focus.data.local.PendingMapOperation
import com.systemssanity.focus.domain.maps.MapFilePaths
import com.systemssanity.focus.domain.maps.MapMutation
import com.systemssanity.focus.domain.maps.MapMutationEngine
import com.systemssanity.focus.domain.maps.MutationApplyResult
import com.systemssanity.focus.domain.model.MapSnapshot
import com.systemssanity.focus.domain.model.MindMapDocument

internal object PendingMapOperations {
    fun applyToSnapshots(
        snapshots: List<MapSnapshot>,
        pendingOperations: List<PendingMapOperation>,
    ): List<MapSnapshot> {
        var next = snapshots
        pendingOperations.forEach { pending ->
            val snapshot = findSnapshotForOperation(next, pending.operation) ?: return@forEach
            val applied = MapMutationEngine.apply(snapshot.document, pending.operation)
            if (applied is MutationApplyResult.Applied) {
                next = replaceSnapshot(next, pending.operation, snapshotForAppliedMutation(snapshot, pending.operation, applied.result.document))
            }
        }
        return next
    }

    fun findSnapshotForOperation(snapshots: List<MapSnapshot>, operation: MapMutation): MapSnapshot? =
        snapshots.firstOrNull { it.filePath == operation.filePath }
            ?: if (operation is MapMutation.RenameMap) {
                snapshots.firstOrNull { it.filePath == operation.newFilePath }
            } else {
                null
            }

    fun snapshotForAppliedMutation(
        snapshot: MapSnapshot,
        operation: MapMutation,
        document: MindMapDocument,
    ): MapSnapshot =
        if (operation is MapMutation.RenameMap) {
            val fileName = MapFilePaths.fileName(operation.newFilePath)
            snapshot.copy(
                filePath = operation.newFilePath,
                fileName = fileName,
                mapName = MapFilePaths.mapName(operation.newFilePath),
                document = document,
                loadedAtMillis = System.currentTimeMillis(),
            )
        } else {
            snapshot.copy(
                document = document,
                loadedAtMillis = System.currentTimeMillis(),
            )
        }

    fun replaceSnapshot(
        snapshots: List<MapSnapshot>,
        operation: MapMutation,
        snapshot: MapSnapshot,
    ): List<MapSnapshot> {
        val pathsToReplace = buildSet {
            add(snapshot.filePath)
            add(operation.filePath)
            if (operation is MapMutation.RenameMap) add(operation.newFilePath)
        }
        return snapshots.filterNot { it.filePath in pathsToReplace } + snapshot
    }
}
