package com.systemssanity.focus.domain.sync

import com.systemssanity.focus.data.github.GitHubBinarySnapshot
import com.systemssanity.focus.data.github.UnreadableMapException
import com.systemssanity.focus.data.local.CachedWorkspace
import com.systemssanity.focus.data.local.InMemoryFocusLocalStore
import com.systemssanity.focus.data.local.PendingMapOperation
import com.systemssanity.focus.domain.maps.MapMutation
import com.systemssanity.focus.domain.maps.PendingConflictResolution
import com.systemssanity.focus.domain.maps.UnreadableMapReason
import com.systemssanity.focus.domain.model.MapSnapshot
import com.systemssanity.focus.domain.model.MindMapDocument
import com.systemssanity.focus.domain.model.Node
import kotlinx.coroutines.flow.first
import kotlinx.coroutines.runBlocking
import kotlin.test.Test
import kotlin.test.assertEquals
import kotlin.test.assertFails
import kotlin.test.assertTrue

class WorkspaceSyncCoordinatorTest {
    @Test
    fun clearScopeRemovesSnapshotsAndPendingOnlyForThatScope() = runBlocking {
        val firstScope = "owner::repo::main::maps"
        val secondScope = "owner::repo::main::other"
        val firstSnapshot = snapshot("FocusMaps/First.json", "First")
        val secondSnapshot = snapshot("OtherMaps/Second.json", "Second")
        val firstPending = pendingEdit(firstScope, "first-pending", firstSnapshot, "First edit")
        val secondPending = pendingEdit(secondScope, "second-pending", secondSnapshot, "Second edit")
        val localStore = InMemoryFocusLocalStore()
        localStore.saveWorkspace(firstScope, CachedWorkspace(snapshots = listOf(firstSnapshot), pendingOperations = listOf(firstPending)))
        localStore.saveWorkspace(secondScope, CachedWorkspace(snapshots = listOf(secondSnapshot), pendingOperations = listOf(secondPending)))

        localStore.clearScope(firstScope)

        assertEquals(CachedWorkspace(), localStore.observeWorkspace(firstScope).first())
        assertEquals(
            CachedWorkspace(snapshots = listOf(secondSnapshot), pendingOperations = listOf(secondPending)),
            localStore.observeWorkspace(secondScope).first(),
        )
    }

    @Test
    fun loadWorkspaceCachesReadableMapsAndKeepsUnreadablePendingOperationsQueued() = runBlocking {
        val scope = "owner::repo::main::maps"
        val readable = snapshot("FocusMaps/Readable.json", "Readable")
        val repository = RecoveryRepository(
            snapshots = listOf(readable),
            unreadablePath = "FocusMaps/Broken.json",
        )
        val localStore = InMemoryFocusLocalStore()
        val pending = PendingMapOperation(
            id = "pending-broken-edit",
            scope = scope,
            operation = MapMutation.EditNodeText(
                filePath = "FocusMaps/Broken.json",
                nodeId = "22222222-2222-4222-8222-222222222222",
                text = "Queued edit",
                timestamp = "2026-05-04T10:00:00Z",
                commitMessage = "map:edit Broken queued",
            ),
            enqueuedAtMillis = 1,
        )
        localStore.saveWorkspace(scope, CachedWorkspace(pendingOperations = listOf(pending)))
        val coordinator = WorkspaceSyncCoordinator(
            scope = scope,
            localStore = localStore,
            service = MindMapService(repository),
        )

        val result = coordinator.loadWorkspace(forceRefresh = true).getOrThrow()
        val cached = localStore.observeWorkspace(scope).first()

        assertEquals(listOf("Readable.json"), result.workspace.snapshots.map { it.fileName })
        assertEquals(listOf("Broken.json"), result.unreadableMaps.map { it.fileName })
        assertEquals(listOf(pending), result.workspace.pendingOperations)
        assertEquals(listOf(pending), cached.pendingOperations)
        assertEquals(emptyList(), repository.savedMaps)
    }

    @Test
    fun pendingOperationCanSyncFromLocalRepairWhenRemoteMapIsStillUnreadable() = runBlocking {
        val scope = "owner::repo::main::maps"
        val repaired = snapshot("FocusMaps/Broken.json", "Broken")
        val repository = RecoveryRepository(
            snapshots = emptyList(),
            unreadablePath = "FocusMaps/Broken.json",
        )
        val localStore = InMemoryFocusLocalStore()
        val pending = PendingMapOperation(
            id = "pending-repaired-edit",
            scope = scope,
            operation = MapMutation.EditNodeText(
                filePath = "FocusMaps/Broken.json",
                nodeId = repaired.document.rootNode.uniqueIdentifier,
                text = "Saved repair",
                timestamp = "2026-05-04T10:00:00Z",
                commitMessage = "map:edit Broken root",
            ),
            enqueuedAtMillis = 1,
        )
        localStore.saveWorkspace(scope, CachedWorkspace(snapshots = listOf(repaired), pendingOperations = listOf(pending)))
        val coordinator = WorkspaceSyncCoordinator(
            scope = scope,
            localStore = localStore,
            service = MindMapService(repository),
        )

        val synced = coordinator.processPendingOperations().getOrThrow().workspace
        val cached = localStore.observeWorkspace(scope).first()

        assertEquals(emptyList(), synced.pendingOperations)
        assertEquals(emptyList(), cached.pendingOperations)
        assertEquals(listOf("FocusMaps/Broken.json"), repository.savedMaps)
        assertEquals("Saved repair", repository.savedDocuments.single().rootNode.name)
        assertTrue(synced.snapshots.single().revision.startsWith("saved-"))
    }

    @Test
    fun missingTargetNodePendingOperationCreatesBlockedEntryAndKeepsQueue() = runBlocking {
        val scope = "owner::repo::main::maps"
        val remote = snapshot("FocusMaps/Broken.json", "Remote fixed")
        val optimistic = remote.copy(mapName = "Local optimistic")
        val pending = PendingMapOperation(
            id = "missing-node",
            scope = scope,
            operation = MapMutation.EditNodeText(
                filePath = remote.filePath,
                nodeId = "22222222-2222-4222-8222-222222222222",
                text = "Queued edit",
                timestamp = "2026-05-04T10:00:00Z",
                commitMessage = "map:edit Broken missing",
            ),
            enqueuedAtMillis = 1,
        )
        val repository = RecoveryRepository(snapshots = listOf(remote))
        val localStore = InMemoryFocusLocalStore()
        localStore.saveWorkspace(scope, CachedWorkspace(snapshots = listOf(optimistic), pendingOperations = listOf(pending)))
        val coordinator = WorkspaceSyncCoordinator(scope, localStore, MindMapService(repository))

        val result = coordinator.processPendingOperations().getOrThrow()
        val cached = localStore.observeWorkspace(scope).first()

        assertEquals("Broken.json", result.blockedPendingMap?.fileName)
        assertEquals("Remote fixed", result.workspace.snapshots.single().mapName)
        assertEquals(listOf(pending), result.workspace.pendingOperations)
        assertEquals(listOf(pending), cached.pendingOperations)
        assertEquals(emptyList(), repository.savedMaps)
    }

    @Test
    fun nonNotFoundMutationValidationRemainsSyncFailure() = runBlocking {
        val scope = "owner::repo::main::maps"
        val remote = snapshot("FocusMaps/Map.json", "Map")
        val pending = PendingMapOperation(
            id = "invalid-root-task-state",
            scope = scope,
            operation = MapMutation.SetTaskState(
                filePath = remote.filePath,
                nodeId = remote.document.rootNode.uniqueIdentifier,
                taskState = com.systemssanity.focus.domain.model.TaskState.Todo,
                timestamp = "2026-05-04T10:00:00Z",
                commitMessage = "map:task Map root",
            ),
            enqueuedAtMillis = 1,
        )
        val localStore = InMemoryFocusLocalStore()
        localStore.saveWorkspace(scope, CachedWorkspace(snapshots = listOf(remote), pendingOperations = listOf(pending)))
        val coordinator = WorkspaceSyncCoordinator(scope, localStore, MindMapService(RecoveryRepository(listOf(remote))))

        val error = assertFails {
            coordinator.processPendingOperations().getOrThrow()
        }

        assertEquals("Can't change task state for root node.", error.message)
    }

    @Test
    fun unresolvedStaleStateCreatesPendingConflictAndKeepsQueue() = runBlocking {
        val scope = "owner::repo::main::maps"
        val remote = snapshot("FocusMaps/Map.json", "Map")
            .copy(document = MindMapDocument(rootNode = Node(uniqueIdentifier = "11111111-1111-4111-8111-111111111111", name = "Remote")))
        val pending = pendingEdit(scope, "edit-root", remote, "Local")
        val localStore = InMemoryFocusLocalStore()
        localStore.saveWorkspace(scope, CachedWorkspace(snapshots = listOf(remote), pendingOperations = listOf(pending)))
        val coordinator = WorkspaceSyncCoordinator(
            scope = scope,
            localStore = localStore,
            service = MindMapService(RecoveryRepository(listOf(remote), staleSaveAttempts = 2)),
        )

        val result = coordinator.processPendingOperations().getOrThrow()
        val cached = localStore.observeWorkspace(scope).first()

        assertEquals("Map.json", result.pendingConflictMap?.fileName)
        assertEquals(listOf(pending), result.workspace.pendingOperations)
        assertEquals(listOf(pending), cached.pendingOperations)
    }

    @Test
    fun prepareConflictResolutionLoadsRemoteAndKeepsOperationOrderForMap() = runBlocking {
        val scope = "owner::repo::main::maps"
        val remote = snapshot("FocusMaps/Map.json", "Map")
            .copy(document = MindMapDocument(rootNode = Node(uniqueIdentifier = "11111111-1111-4111-8111-111111111111", name = "Remote")))
        val local = remote.copy(document = MindMapDocument(rootNode = remote.document.rootNode.copy(name = "Local")))
        val pendingOne = pendingEdit(scope, "one", remote, "Local one")
        val pendingTwo = pendingEdit(scope, "two", remote, "Local two")
        val pendingOther = pendingEdit(scope, "other", snapshot("FocusMaps/Other.json", "Other"), "Other")
        val localStore = InMemoryFocusLocalStore()
        localStore.saveWorkspace(
            scope,
            CachedWorkspace(snapshots = listOf(local), pendingOperations = listOf(pendingOne, pendingTwo, pendingOther)),
        )
        val coordinator = WorkspaceSyncCoordinator(scope, localStore, MindMapService(RecoveryRepository(listOf(remote))))

        val draft = coordinator.prepareConflictResolution("FocusMaps/Map.json").getOrThrow()

        assertEquals("Remote", draft.remoteDocument.rootNode.name)
        assertEquals("Local", draft.localDocument.rootNode.name)
        assertEquals(listOf("one", "two"), draft.items.map { it.pendingOperationId })
        assertEquals(listOf("text change in Map", "text change in Map"), draft.items.map { it.description })
    }

    @Test
    fun resolveConflictWithLocalChoicesAppliesSelectedMutationsAndClearsOnlyThatMap() = runBlocking {
        val scope = "owner::repo::main::maps"
        val remote = snapshot("FocusMaps/Map.json", "Map")
            .copy(document = MindMapDocument(rootNode = Node(uniqueIdentifier = "11111111-1111-4111-8111-111111111111", name = "Remote")))
        val other = snapshot("FocusMaps/Other.json", "Other")
        val pendingLocal = pendingEdit(scope, "local", remote, "Local")
        val pendingOther = pendingEdit(scope, "other", other, "Other edit")
        val localStore = InMemoryFocusLocalStore()
        localStore.saveWorkspace(
            scope,
            CachedWorkspace(snapshots = listOf(remote, other), pendingOperations = listOf(pendingLocal, pendingOther)),
        )
        val repository = RecoveryRepository(listOf(remote, other))
        val coordinator = WorkspaceSyncCoordinator(scope, localStore, MindMapService(repository))

        val result = coordinator.resolveConflict(
            filePath = remote.filePath,
            mapName = remote.mapName,
            remoteDocument = remote.document,
            remoteRevision = remote.revision,
            choices = mapOf("local" to PendingConflictResolution.Local),
        ).getOrThrow()
        val cached = localStore.observeWorkspace(scope).first()

        assertEquals("Local", repository.savedDocuments.single().rootNode.name)
        assertEquals(listOf("map:resolve Map"), repository.savedCommitMessages)
        assertEquals(listOf(pendingOther), result.workspace.pendingOperations)
        assertEquals(listOf(pendingOther), cached.pendingOperations)
    }

    @Test
    fun resolveConflictWithRemoteChoiceDiscardsLocalMutationAfterSavingRemoteDocument() = runBlocking {
        val scope = "owner::repo::main::maps"
        val remote = snapshot("FocusMaps/Map.json", "Map")
            .copy(document = MindMapDocument(rootNode = Node(uniqueIdentifier = "11111111-1111-4111-8111-111111111111", name = "Remote")))
        val pendingLocal = pendingEdit(scope, "local", remote, "Local")
        val localStore = InMemoryFocusLocalStore()
        localStore.saveWorkspace(scope, CachedWorkspace(snapshots = listOf(remote), pendingOperations = listOf(pendingLocal)))
        val repository = RecoveryRepository(listOf(remote))
        val coordinator = WorkspaceSyncCoordinator(scope, localStore, MindMapService(repository))

        val result = coordinator.resolveConflict(
            filePath = remote.filePath,
            mapName = remote.mapName,
            remoteDocument = remote.document,
            remoteRevision = remote.revision,
            choices = mapOf("local" to PendingConflictResolution.Remote),
        ).getOrThrow()

        assertEquals("Remote", repository.savedDocuments.single().rootNode.name)
        assertEquals(emptyList(), result.workspace.pendingOperations)
    }

    @Test
    fun failedConflictResolutionSaveLeavesPendingOperationsQueued() = runBlocking {
        val scope = "owner::repo::main::maps"
        val remote = snapshot("FocusMaps/Map.json", "Remote")
        val pendingLocal = pendingEdit(scope, "local", remote, "Local")
        val localStore = InMemoryFocusLocalStore()
        localStore.saveWorkspace(scope, CachedWorkspace(snapshots = listOf(remote), pendingOperations = listOf(pendingLocal)))
        val coordinator = WorkspaceSyncCoordinator(
            scope = scope,
            localStore = localStore,
            service = MindMapService(RecoveryRepository(listOf(remote), staleSaveAttempts = 1)),
        )

        assertFails {
            coordinator.resolveConflict(
                filePath = remote.filePath,
                mapName = remote.mapName,
                remoteDocument = remote.document,
                remoteRevision = remote.revision,
                choices = mapOf("local" to PendingConflictResolution.Local),
            ).getOrThrow()
        }
        val cached = localStore.observeWorkspace(scope).first()

        assertEquals(listOf(pendingLocal), cached.pendingOperations)
        assertEquals("Remote", cached.snapshots.single().document.rootNode.name)
    }

    @Test
    fun discardPendingOperationsForMapRemovesOnlyMatchingQueueAndRestoresRemoteBaseline() = runBlocking {
        val scope = "owner::repo::main::maps"
        val remoteBroken = snapshot("FocusMaps/Broken.json", "Remote fixed")
        val localBroken = remoteBroken.copy(mapName = "Local optimistic")
        val other = snapshot("FocusMaps/Other.json", "Other")
        val pendingBroken = pendingEdit(scope, "broken", remoteBroken, "Queued broken")
        val pendingOther = pendingEdit(scope, "other", other, "Queued other")
        val localStore = InMemoryFocusLocalStore()
        localStore.saveWorkspace(
            scope,
            CachedWorkspace(
                snapshots = listOf(localBroken, other),
                pendingOperations = listOf(pendingBroken, pendingOther),
            ),
        )
        val coordinator = WorkspaceSyncCoordinator(scope, localStore, MindMapService(RecoveryRepository(listOf(remoteBroken, other))))

        val result = coordinator.discardPendingOperationsForMap("FocusMaps/Broken.json").getOrThrow()
        val cached = localStore.observeWorkspace(scope).first()

        assertEquals(1, result.discardedPendingCount)
        assertEquals(listOf(pendingOther), result.workspace.pendingOperations)
        assertEquals(listOf(pendingOther), cached.pendingOperations)
        assertEquals("Remote fixed", cached.snapshots.first { it.filePath == "FocusMaps/Broken.json" }.mapName)
    }

    @Test
    fun discardPendingOperationsForMapHandlesRenameOldAndNewPaths() = runBlocking {
        val scope = "owner::repo::main::maps"
        val old = snapshot("FocusMaps/Old.json", "Old")
        val renamed = old.copy(filePath = "FocusMaps/New.json", fileName = "New.json", mapName = "New")
        val pendingRename = PendingMapOperation(
            id = "rename",
            scope = scope,
            operation = MapMutation.RenameMap(
                filePath = old.filePath,
                newFilePath = renamed.filePath,
                nodeId = old.document.rootNode.uniqueIdentifier,
                text = "New",
                timestamp = "2026-05-04T10:00:00Z",
                commitMessage = "map:rename Old New",
            ),
            enqueuedAtMillis = 1,
        )
        val localStore = InMemoryFocusLocalStore()
        localStore.saveWorkspace(scope, CachedWorkspace(snapshots = listOf(renamed), pendingOperations = listOf(pendingRename)))
        val coordinator = WorkspaceSyncCoordinator(scope, localStore, MindMapService(RecoveryRepository(listOf(old))))

        val result = coordinator.discardPendingOperationsForMap("FocusMaps/New.json").getOrThrow()

        assertEquals(1, result.discardedPendingCount)
        assertEquals(emptyList(), result.workspace.pendingOperations)
        assertEquals(listOf("FocusMaps/Old.json"), result.workspace.snapshots.map { it.filePath })
    }

    @Test
    fun resetMapToRemoteReplacesLocalSnapshotAndDiscardsOnlyMatchingPendingOperations() = runBlocking {
        val scope = "owner::repo::main::maps"
        val localBroken = snapshot("FocusMaps/Broken.json", "Local repair")
        val remoteBroken = snapshot("FocusMaps/Broken.json", "Remote fixed")
        val other = snapshot("FocusMaps/Other.json", "Other")
        val pendingBroken = pendingEdit(scope, "broken", localBroken, "Queued broken")
        val pendingOther = pendingEdit(scope, "other", other, "Queued other")
        val repository = RecoveryRepository(snapshots = listOf(remoteBroken, other))
        val localStore = InMemoryFocusLocalStore()
        localStore.saveWorkspace(
            scope,
            CachedWorkspace(
                snapshots = listOf(localBroken, other),
                pendingOperations = listOf(pendingBroken, pendingOther),
            ),
        )
        val coordinator = WorkspaceSyncCoordinator(scope, localStore, MindMapService(repository))

        val result = coordinator.resetMapToRemote("FocusMaps/Broken.json").getOrThrow()
            as WorkspaceMapResetResult.Reset
        val cached = localStore.observeWorkspace(scope).first()

        assertEquals("Remote fixed", result.snapshot.mapName)
        assertEquals(1, result.discardedPendingCount)
        assertEquals(listOf("Remote fixed", "Other"), cached.snapshots.map { it.mapName })
        assertEquals(listOf(pendingOther), cached.pendingOperations)
    }

    @Test
    fun resetMapToRemoteKeepsPendingOperationsWhenRemoteIsStillUnreadable() = runBlocking {
        val scope = "owner::repo::main::maps"
        val localBroken = snapshot("FocusMaps/Broken.json", "Local repair")
        val pendingBroken = pendingEdit(scope, "broken", localBroken, "Queued broken")
        val repository = RecoveryRepository(
            snapshots = emptyList(),
            unreadablePath = "FocusMaps/Broken.json",
        )
        val localStore = InMemoryFocusLocalStore()
        localStore.saveWorkspace(scope, CachedWorkspace(snapshots = listOf(localBroken), pendingOperations = listOf(pendingBroken)))
        val coordinator = WorkspaceSyncCoordinator(scope, localStore, MindMapService(repository))

        val result = coordinator.resetMapToRemote("FocusMaps/Broken.json").getOrThrow()
            as WorkspaceMapResetResult.StillUnreadable
        val cached = localStore.observeWorkspace(scope).first()

        assertEquals("Broken.json", result.entry.fileName)
        assertEquals(listOf(localBroken), cached.snapshots)
        assertEquals(listOf(pendingBroken), cached.pendingOperations)
    }

    @Test
    fun resetMapToRemoteRemovesLocalStateWhenRemoteFileWasDeleted() = runBlocking {
        val scope = "owner::repo::main::maps"
        val localBroken = snapshot("FocusMaps/Broken.json", "Local repair")
        val other = snapshot("FocusMaps/Other.json", "Other")
        val pendingBroken = pendingEdit(scope, "broken", localBroken, "Queued broken")
        val pendingOther = pendingEdit(scope, "other", other, "Queued other")
        val repository = RecoveryRepository(
            snapshots = listOf(other),
            notFoundPaths = setOf("FocusMaps/Broken.json"),
        )
        val localStore = InMemoryFocusLocalStore()
        localStore.saveWorkspace(
            scope,
            CachedWorkspace(
                snapshots = listOf(localBroken, other),
                pendingOperations = listOf(pendingBroken, pendingOther),
            ),
        )
        val coordinator = WorkspaceSyncCoordinator(scope, localStore, MindMapService(repository))

        val result = coordinator.resetMapToRemote("FocusMaps/Broken.json").getOrThrow()
            as WorkspaceMapResetResult.Deleted
        val cached = localStore.observeWorkspace(scope).first()

        assertEquals("FocusMaps/Broken.json", result.filePath)
        assertEquals(1, result.discardedPendingCount)
        assertEquals(listOf(other), cached.snapshots)
        assertEquals(listOf(pendingOther), cached.pendingOperations)
    }

    @Test
    fun resetMapToRemoteFailsForOtherLoadErrors() = runBlocking {
        val scope = "owner::repo::main::maps"
        val repository = RecoveryRepository(
            snapshots = emptyList(),
            loadFailures = mapOf("FocusMaps/Broken.json" to IllegalStateException("Network failed")),
        )
        val coordinator = WorkspaceSyncCoordinator(scope, InMemoryFocusLocalStore(), MindMapService(repository))

        val error = assertFails {
            coordinator.resetMapToRemote("FocusMaps/Broken.json").getOrThrow()
        }

        assertEquals("Network failed", error.message)
    }
}

private class RecoveryRepository(
    snapshots: List<MapSnapshot>,
    private val unreadablePath: String? = null,
    private val notFoundPaths: Set<String> = emptySet(),
    private val loadFailures: Map<String, Throwable> = emptyMap(),
    staleSaveAttempts: Int = 0,
) : MindMapRepositoryGateway {
    private val snapshotsByPath = LinkedHashMap(snapshots.associateBy { it.filePath })
    val savedMaps = mutableListOf<String>()
    val savedDocuments = mutableListOf<MindMapDocument>()
    val savedCommitMessages = mutableListOf<String>()
    private var saveCount = 0
    private var remainingStaleSaveAttempts = staleSaveAttempts

    override suspend fun listFiles(): Result<List<Pair<String, String>>> =
        Result.success(
            (snapshotsByPath.values.map { it.fileName to it.filePath } + listOfNotNull(unreadablePath?.let { "Broken.json" to it }))
                .sortedBy { it.first.lowercase() },
        )

    override suspend fun loadMap(filePath: String): Result<MapSnapshot> =
        loadFailures[filePath]?.let(Result.Companion::failure)
            ?: if (filePath in notFoundPaths) {
                Result.failure(WorkspaceFakeNotFoundException(filePath))
            } else if (filePath == unreadablePath) {
            Result.failure(
                UnreadableMapException(
                    reason = UnreadableMapReason.InvalidJson,
                    filePath = unreadablePath,
                    fileName = "Broken.json",
                    mapName = "Broken",
                    revision = "rev-broken",
                    rawText = "{\"rootNode\":",
                ),
            )
        } else {
            snapshotsByPath[filePath]?.let(Result.Companion::success)
                ?: Result.failure(IllegalStateException("Missing map $filePath"))
        }

    override suspend fun saveMap(
        filePath: String,
        document: MindMapDocument,
        revision: String?,
        commitMessage: String,
    ): Result<String> {
        if (remainingStaleSaveAttempts > 0) {
            remainingStaleSaveAttempts--
            return Result.failure(WorkspaceFakeStaleException(filePath))
        }
        savedMaps += filePath
        savedDocuments += document
        savedCommitMessages += commitMessage
        val nextRevision = "saved-${++saveCount}"
        val current = snapshotsByPath[filePath]
        snapshotsByPath[filePath] = (current ?: MapSnapshot(
            filePath = filePath,
            fileName = filePath.substringAfterLast('/'),
            mapName = filePath.substringAfterLast('/').removeSuffix(".json"),
            document = document,
            revision = nextRevision,
            loadedAtMillis = 0,
        )).copy(document = document, revision = nextRevision)
        return Result.success(nextRevision)
    }

    override suspend fun createMap(filePath: String, document: MindMapDocument, commitMessage: String): Result<String> =
        Result.failure(UnsupportedOperationException("createMap is not used by these tests"))

    override suspend fun deleteMap(filePath: String, revision: String, commitMessage: String): Result<Unit> =
        Result.failure(UnsupportedOperationException("deleteMap is not used by these tests"))

    override suspend fun renameMap(
        oldFilePath: String,
        newFilePath: String,
        document: MindMapDocument,
        oldRevision: String,
        commitMessage: String,
    ): Result<String> =
        Result.failure(UnsupportedOperationException("renameMap is not used by these tests"))

    override suspend fun loadAttachment(nodeId: String, relativePath: String, mediaType: String): Result<GitHubBinarySnapshot> =
        Result.failure(UnsupportedOperationException("loadAttachment is not used by these tests"))

    override suspend fun uploadAttachment(
        nodeId: String,
        relativePath: String,
        base64Content: String,
        commitMessage: String,
    ): Result<String> =
        Result.failure(UnsupportedOperationException("uploadAttachment is not used by these tests"))

    override suspend fun deleteAttachment(
        nodeId: String,
        relativePath: String,
        expectedRevision: String?,
        commitMessage: String,
    ): Result<Unit> =
        Result.failure(UnsupportedOperationException("deleteAttachment is not used by these tests"))

    override fun Throwable.isStaleState(): Boolean = this is WorkspaceFakeStaleException

    override fun Throwable.isNotFound(): Boolean = this is WorkspaceFakeNotFoundException
}

private class WorkspaceFakeNotFoundException(filePath: String) : Exception("Missing map $filePath")

private class WorkspaceFakeStaleException(filePath: String) : Exception("Stale map $filePath")

private fun pendingEdit(scope: String, id: String, snapshot: MapSnapshot, text: String): PendingMapOperation =
    PendingMapOperation(
        id = id,
        scope = scope,
        operation = MapMutation.EditNodeText(
            filePath = snapshot.filePath,
            nodeId = snapshot.document.rootNode.uniqueIdentifier,
            text = text,
            timestamp = "2026-05-04T10:00:00Z",
            commitMessage = "map:edit ${snapshot.mapName} root",
        ),
        enqueuedAtMillis = 1,
    )

private fun snapshot(filePath: String, name: String): MapSnapshot =
    MapSnapshot(
        filePath = filePath,
        fileName = filePath.substringAfterLast('/'),
        mapName = name,
        document = MindMapDocument(
            rootNode = Node(
                uniqueIdentifier = "11111111-1111-4111-8111-111111111111",
                name = name,
            ),
        ),
        revision = "rev-$name",
        loadedAtMillis = 0,
    )
