package com.systemssanity.focus.data.local

import android.content.Context
import com.systemssanity.focus.domain.model.MapSnapshot
import com.systemssanity.focus.domain.model.MindMapDocument
import com.systemssanity.focus.domain.model.MindMapJson
import kotlinx.coroutines.Dispatchers
import kotlinx.coroutines.flow.Flow
import kotlinx.coroutines.flow.MutableStateFlow
import kotlinx.coroutines.sync.Mutex
import kotlinx.coroutines.sync.withLock
import kotlinx.coroutines.withContext
import kotlinx.serialization.Serializable
import kotlinx.serialization.encodeToString
import kotlinx.serialization.json.Json
import java.io.File
import java.util.Base64

class FileFocusLocalStore(context: Context) : FocusLocalStore {
    private val rootDirectory = File(context.filesDir, "focus-workspaces")
    private val json = Json {
        encodeDefaults = true
        ignoreUnknownKeys = true
        prettyPrint = true
    }
    private val mutex = Mutex()
    private val flows = mutableMapOf<String, MutableStateFlow<CachedWorkspace>>()

    override fun observeWorkspace(scope: String): Flow<CachedWorkspace> =
        flowFor(scope)

    override suspend fun saveWorkspace(scope: String, workspace: CachedWorkspace) {
        updateWorkspace(scope) { workspace }
    }

    override suspend fun replaceSnapshots(scope: String, snapshots: List<MapSnapshot>) {
        updateWorkspace(scope) { current ->
            current.copy(snapshots = snapshots)
        }
    }

    override suspend fun appendPendingOperation(operation: PendingMapOperation) {
        updateWorkspace(operation.scope) { current ->
            current.copy(pendingOperations = current.pendingOperations + operation)
        }
    }

    override suspend fun removePendingOperation(scope: String, operationId: String) {
        updateWorkspace(scope) { current ->
            current.copy(pendingOperations = current.pendingOperations.filterNot { it.id == operationId })
        }
    }

    override suspend fun clearScope(scope: String) {
        mutex.withLock {
            withContext(Dispatchers.IO) {
                workspaceFile(scope).delete()
            }
            flowFor(scope).value = CachedWorkspace()
        }
    }

    private suspend fun updateWorkspace(scope: String, transform: (CachedWorkspace) -> CachedWorkspace) {
        mutex.withLock {
            val current = flowFor(scope).value
            val next = transform(current)
            writeWorkspace(scope, next)
            flowFor(scope).value = next
        }
    }

    private fun flowFor(scope: String): MutableStateFlow<CachedWorkspace> =
        synchronized(flows) {
            flows.getOrPut(scope) {
                MutableStateFlow(readWorkspaceBlocking(scope))
            }
        }

    private fun readWorkspaceBlocking(scope: String): CachedWorkspace =
        runCatching {
            val file = workspaceFile(scope)
            if (!file.isFile) {
                CachedWorkspace()
            } else {
                val dto = json.decodeFromString<CachedWorkspaceDto>(file.readText())
                dto.toDomain()
            }
        }.getOrDefault(CachedWorkspace())

    private suspend fun writeWorkspace(scope: String, workspace: CachedWorkspace) {
        withContext(Dispatchers.IO) {
            rootDirectory.mkdirs()
            workspaceFile(scope).writeText(json.encodeToString(CachedWorkspaceDto.fromDomain(workspace)))
        }
    }

    private fun workspaceFile(scope: String): File {
        val fileName = Base64.getUrlEncoder()
            .withoutPadding()
            .encodeToString(scope.toByteArray(Charsets.UTF_8))
        return File(rootDirectory, "$fileName.json")
    }

    @Serializable
    private data class CachedWorkspaceDto(
        val snapshots: List<MapSnapshotDto> = emptyList(),
        val pendingOperations: List<PendingMapOperation> = emptyList(),
    ) {
        fun toDomain(): CachedWorkspace =
            CachedWorkspace(
                snapshots = snapshots.mapNotNull { it.toDomainOrNull() },
                pendingOperations = pendingOperations,
            )

        companion object {
            fun fromDomain(workspace: CachedWorkspace): CachedWorkspaceDto =
                CachedWorkspaceDto(
                    snapshots = workspace.snapshots.map(MapSnapshotDto::fromDomain),
                    pendingOperations = workspace.pendingOperations,
                )
        }
    }

    @Serializable
    private data class MapSnapshotDto(
        val filePath: String,
        val fileName: String,
        val mapName: String,
        val document: MindMapDocument,
        val revision: String,
        val loadedAtMillis: Long,
    ) {
        fun toDomainOrNull(): MapSnapshot? =
            runCatching {
                MapSnapshot(
                    filePath = filePath,
                    fileName = fileName,
                    mapName = mapName,
                    document = MindMapJson.normalize(document),
                    revision = revision,
                    loadedAtMillis = loadedAtMillis,
                )
            }.getOrNull()

        companion object {
            fun fromDomain(snapshot: MapSnapshot): MapSnapshotDto =
                MapSnapshotDto(
                    filePath = snapshot.filePath,
                    fileName = snapshot.fileName,
                    mapName = snapshot.mapName,
                    document = MindMapJson.normalize(snapshot.document),
                    revision = snapshot.revision,
                    loadedAtMillis = snapshot.loadedAtMillis,
                )
        }
    }
}
