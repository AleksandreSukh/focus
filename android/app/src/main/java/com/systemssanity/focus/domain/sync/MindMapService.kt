package com.systemssanity.focus.domain.sync

import com.systemssanity.focus.domain.maps.MapMutation
import com.systemssanity.focus.domain.maps.MapMutationEngine
import com.systemssanity.focus.domain.maps.MutationApplyResult
import com.systemssanity.focus.domain.model.MindMapJson
import com.systemssanity.focus.domain.model.MapSnapshot
import com.systemssanity.focus.domain.model.MindMapDocument
import java.util.UUID

class MindMapService(private val repository: MindMapRepository) {
    private val snapshotsByPath = LinkedHashMap<String, MapSnapshot>()

    suspend fun listMaps(forceRefresh: Boolean = false): Result<List<MapSnapshot>> =
        repository.listFiles().mapCatching { files ->
            files.map { (_, filePath) -> loadMap(filePath, forceRefresh).getOrThrow() }
        }

    suspend fun loadMap(filePath: String, forceRefresh: Boolean = false): Result<MapSnapshot> {
        if (!forceRefresh) {
            snapshotsByPath[filePath]?.let { return Result.success(it) }
        }
        return repository.loadMap(filePath).onSuccess { snapshotsByPath[filePath] = it }
    }

    suspend fun createMap(filePath: String, mapName: String, commitMessage: String): Result<MapSnapshot> {
        val document = MindMapJson.normalize(MindMapDocument(
            rootNode = com.systemssanity.focus.domain.model.Node(
                uniqueIdentifier = UUID.randomUUID().toString(),
                name = mapName,
            ),
        ))
        return repository.createMap(filePath, document, commitMessage).map { revision ->
            MapSnapshot(
                filePath = filePath,
                fileName = filePath.substringAfterLast('/'),
                mapName = mapName,
                document = document,
                revision = revision,
                loadedAtMillis = System.currentTimeMillis(),
            ).also { snapshotsByPath[filePath] = it }
        }
    }

    suspend fun deleteMap(filePath: String, commitMessage: String): Result<Unit> =
        runCatching {
            val latest = loadMap(filePath, forceRefresh = true).getOrThrow()
            repository.deleteMap(filePath, latest.revision, commitMessage).getOrThrow()
            snapshotsByPath.remove(filePath)
            Unit
        }

    suspend fun renameMap(
        oldFilePath: String,
        newFilePath: String,
        document: MindMapDocument,
        oldRevision: String,
        commitMessage: String,
    ): Result<MapSnapshot> =
        repository.renameMap(oldFilePath, newFilePath, MindMapJson.normalize(document), oldRevision, commitMessage)
            .map { revision ->
                val fileName = newFilePath.substringAfterLast('/')
                MapSnapshot(
                    filePath = newFilePath,
                    fileName = fileName,
                    mapName = fileName.removeSuffix(".json"),
                    document = MindMapJson.normalize(document),
                    revision = revision,
                    loadedAtMillis = System.currentTimeMillis(),
                ).also {
                    snapshotsByPath.remove(oldFilePath)
                    snapshotsByPath[newFilePath] = it
                }
            }

    suspend fun loadAttachment(nodeId: String, relativePath: String, mediaType: String) =
        repository.loadAttachment(nodeId, relativePath, mediaType)

    suspend fun uploadAttachment(nodeId: String, relativePath: String, base64Content: String, commitMessage: String) =
        repository.uploadAttachment(nodeId, relativePath, base64Content, commitMessage)

    suspend fun deleteAttachment(nodeId: String, relativePath: String, expectedRevision: String?, commitMessage: String) =
        repository.deleteAttachment(nodeId, relativePath, expectedRevision, commitMessage)

    suspend fun applyMutation(filePath: String, mutation: MapMutation): Result<MapSnapshot> =
        runCatching {
            val latest = loadMap(filePath).getOrThrow()
            val applied = MapMutationEngine.apply(latest.document, mutation)
            if (applied is MutationApplyResult.Rejected) {
                error(applied.message)
            }
            check(applied is MutationApplyResult.Applied)

            val firstSave = repository.saveMap(
                filePath = filePath,
                document = applied.result.document,
                revision = latest.revision,
                commitMessage = mutation.commitMessage,
            )
            var savedDocument = applied.result.document
            val revision = firstSave.getOrElse { firstError ->
                if (!repository.run { firstError.isStaleState() }) throw firstError
                val refreshed = loadMap(filePath, forceRefresh = true).getOrThrow()
                val retried = MapMutationEngine.apply(refreshed.document, mutation)
                if (retried is MutationApplyResult.Rejected) error(retried.message)
                check(retried is MutationApplyResult.Applied)
                savedDocument = retried.result.document
                repository.saveMap(filePath, retried.result.document, refreshed.revision, mutation.commitMessage).getOrThrow()
            }

            latest.copy(
                document = savedDocument,
                revision = revision,
                loadedAtMillis = System.currentTimeMillis(),
            ).also { snapshotsByPath[filePath] = it }
        }

    fun hydrateSnapshots(snapshots: List<MapSnapshot>) {
        snapshotsByPath.clear()
        snapshots.forEach { snapshotsByPath[it.filePath] = it }
    }

    fun cachedSnapshots(): List<MapSnapshot> = snapshotsByPath.values.toList()
}
