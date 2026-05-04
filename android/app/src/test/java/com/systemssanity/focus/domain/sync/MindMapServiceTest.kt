package com.systemssanity.focus.domain.sync

import com.systemssanity.focus.data.github.GitHubBinarySnapshot
import com.systemssanity.focus.data.github.UnreadableMapException
import com.systemssanity.focus.domain.maps.MapMutation
import com.systemssanity.focus.domain.maps.UnreadableMapReason
import com.systemssanity.focus.domain.model.MapSnapshot
import com.systemssanity.focus.domain.model.MindMapDocument
import com.systemssanity.focus.domain.model.Node
import com.systemssanity.focus.domain.model.NodeAttachment
import com.systemssanity.focus.domain.model.NodeMetadata
import kotlinx.coroutines.runBlocking
import kotlin.test.Test
import kotlin.test.assertEquals
import kotlin.test.assertFails
import kotlin.test.assertFalse
import kotlin.test.assertTrue

class MindMapServiceTest {
    @Test
    fun listMapsReturnsReadableSnapshotsAlongsideUnreadableMapsAndClearsStaleCache() = runBlocking {
        val readable = snapshot(
            filePath = "FocusMaps/Readable.json",
            document = MindMapDocument(rootNode = Node(uniqueIdentifier = ROOT_ID, name = "Readable")),
        )
        val staleBroken = snapshot(
            filePath = "FocusMaps/Broken.json",
            document = MindMapDocument(rootNode = Node(uniqueIdentifier = CHILD_ID, name = "Old broken cache")),
        )
        val repository = FakeMindMapRepository(listOf(readable, staleBroken)).apply {
            loadFailures["FocusMaps/Broken.json"] = UnreadableMapException(
                reason = UnreadableMapReason.MergeConflict,
                filePath = "FocusMaps/Broken.json",
                fileName = "Broken.json",
                mapName = "Broken",
                revision = "rev-broken",
                rawText = "<<<<<<< HEAD\n{}\n=======\n{}\n>>>>>>> main\n",
            )
        }
        val service = MindMapService(repository)
        service.hydrateSnapshots(listOf(staleBroken))

        val listed = service.listMaps(forceRefresh = true).getOrThrow()

        assertEquals(listOf("Readable.json"), listed.snapshots.map { it.fileName })
        assertEquals(listOf("Broken.json"), listed.unreadableMaps.map { it.fileName })
        assertEquals(UnreadableMapReason.MergeConflict, listed.unreadableMaps.single().reason)
        assertEquals("rev-broken", listed.unreadableMaps.single().revision)
        assertEquals("<<<<<<< HEAD\n{}\n=======\n{}\n>>>>>>> main\n", listed.unreadableMaps.single().rawText)
        assertEquals(emptyList(), service.cachedSnapshots().filter { it.filePath == "FocusMaps/Broken.json" })
    }

    @Test
    fun listMapsFailsForNonUnreadableLoadErrors() = runBlocking {
        val repository = FakeMindMapRepository(
            snapshot(
                filePath = "FocusMaps/Root.json",
                document = MindMapDocument(rootNode = Node(uniqueIdentifier = ROOT_ID, name = "Root")),
            ),
        ).apply {
            loadFailures["FocusMaps/Root.json"] = IllegalStateException("Network failed")
        }
        val service = MindMapService(repository)

        val error = assertFails {
            service.listMaps(forceRefresh = true).getOrThrow()
        }

        assertEquals("Network failed", error.message)
    }

    @Test
    fun replaceCachedSnapshotSeedsLocalRepairBaseline() = runBlocking {
        val repository = FakeMindMapRepository(emptyList())
        val service = MindMapService(repository)
        val repaired = snapshot(
            filePath = "FocusMaps/Broken.json",
            document = MindMapDocument(rootNode = Node(uniqueIdentifier = ROOT_ID, name = "Repaired")),
        )

        service.replaceCachedSnapshot(repaired)
        val loaded = service.loadMap("FocusMaps/Broken.json").getOrThrow()

        assertEquals(repaired, loaded)
        assertEquals(listOf(repaired), service.cachedSnapshots())
    }

    @Test
    fun removeCachedSnapshotClearsLocalRepairBaseline() = runBlocking {
        val repository = FakeMindMapRepository(emptyList())
        val service = MindMapService(repository)
        val repaired = snapshot(
            filePath = "FocusMaps/Broken.json",
            document = MindMapDocument(rootNode = Node(uniqueIdentifier = ROOT_ID, name = "Repaired")),
        )

        service.replaceCachedSnapshot(repaired)
        service.removeCachedSnapshot("FocusMaps/Broken.json")

        assertEquals(emptyList(), service.cachedSnapshots())
        assertFails {
            service.loadMap("FocusMaps/Broken.json").getOrThrow()
        }
        Unit
    }

    @Test
    fun serviceClassifiesRepositoryNotFoundErrors() {
        val repository = FakeMindMapRepository(emptyList())
        val service = MindMapService(repository)

        assertTrue(service.isNotFound(FakeNotFoundException("FocusMaps/Missing.json")))
        assertFalse(service.isNotFound(IllegalStateException("Other failure")))
    }

    @Test
    fun deleteMapRemovesOwnedAttachmentsBeforeDeletingMapFile() = runBlocking {
        val repository = FakeMindMapRepository(
            snapshot(
                filePath = "FocusMaps/Root.json",
                document = MindMapDocument(
                    rootNode = Node(
                        uniqueIdentifier = ROOT_ID,
                        name = "Root",
                        metadata = metadataAttachment("root-image", "root.png", "Root image"),
                        children = listOf(
                            Node(
                                uniqueIdentifier = CHILD_ID,
                                name = "Child",
                                metadata = metadataAttachment("child-audio", "child.m4a", "Child audio"),
                            ),
                        ),
                    ),
                ),
            ),
        )
        val service = MindMapService(repository)

        service.deleteMap("FocusMaps/Root.json", "map:delete Root").getOrThrow()

        assertEquals(
            listOf(
                "deleteAttachment:$ROOT_ID/root.png",
                "deleteAttachment:$CHILD_ID/child.m4a",
                "deleteMap:FocusMaps/Root.json",
            ),
            repository.operations,
        )
        assertEquals(listOf(null, null), repository.deletedAttachments.map { it.expectedRevision })
        assertEquals(listOf("map:delete Root", "map:delete Root"), repository.deletedAttachments.map { it.commitMessage })
        assertFalse(repository.snapshots.containsKey("FocusMaps/Root.json"))
    }

    @Test
    fun deleteMapAbortsWhenAttachmentCleanupFails() = runBlocking {
        val repository = FakeMindMapRepository(
            snapshot(
                filePath = "FocusMaps/Root.json",
                document = MindMapDocument(
                    rootNode = Node(
                        uniqueIdentifier = ROOT_ID,
                        name = "Root",
                        metadata = metadataAttachment("root-image", "root.png", "Root image"),
                    ),
                ),
            ),
        ).apply {
            failingAttachmentPath = "root.png"
        }
        val service = MindMapService(repository)

        assertFails {
            service.deleteMap("FocusMaps/Root.json", "map:delete Root").getOrThrow()
        }

        assertEquals(listOf("deleteAttachment:$ROOT_ID/root.png"), repository.operations)
        assertTrue(repository.snapshots.containsKey("FocusMaps/Root.json"))
    }

    @Test
    fun deleteNodeMutationRemovesSubtreeAttachmentsBeforeSavingMap() = runBlocking {
        val deleted = Node(
            uniqueIdentifier = DELETE_ID,
            name = "Delete me",
            metadata = metadataAttachment("deleted-image", "deleted.png", "Deleted image"),
            children = listOf(
                Node(
                    uniqueIdentifier = GRANDCHILD_ID,
                    name = "Grandchild",
                    metadata = metadataAttachment("grandchild-text", "grandchild.txt", "Grandchild text"),
                ),
            ),
        )
        val keep = Node(uniqueIdentifier = KEEP_ID, name = "Keep me", number = 2)
        val repository = FakeMindMapRepository(
            snapshot(
                filePath = "FocusMaps/Root.json",
                document = MindMapDocument(
                    rootNode = Node(
                        uniqueIdentifier = ROOT_ID,
                        name = "Root",
                        children = listOf(deleted, keep),
                    ),
                ),
            ),
        )
        val service = MindMapService(repository)

        val saved = service.applyMutation(
            filePath = "FocusMaps/Root.json",
            mutation = MapMutation.DeleteNode(
                filePath = "FocusMaps/Root.json",
                nodeId = DELETE_ID,
                timestamp = "2026-05-04T10:00:00Z",
                commitMessage = "map:delete Root $DELETE_ID",
            ),
        ).getOrThrow()

        assertEquals(
            listOf(
                "deleteAttachment:$DELETE_ID/deleted.png",
                "deleteAttachment:$GRANDCHILD_ID/grandchild.txt",
                "saveMap:FocusMaps/Root.json",
            ),
            repository.operations,
        )
        assertEquals(listOf(KEEP_ID), saved.document.rootNode.children.map { it.uniqueIdentifier })
        assertEquals(1, saved.document.rootNode.children.single().number)
        assertEquals(listOf("map:delete Root $DELETE_ID", "map:delete Root $DELETE_ID"), repository.deletedAttachments.map { it.commitMessage })
    }

    @Test
    fun deleteNodeMutationAbortsSaveWhenAttachmentCleanupFails() = runBlocking {
        val repository = FakeMindMapRepository(
            snapshot(
                filePath = "FocusMaps/Root.json",
                document = MindMapDocument(
                    rootNode = Node(
                        uniqueIdentifier = ROOT_ID,
                        name = "Root",
                        children = listOf(
                            Node(
                                uniqueIdentifier = DELETE_ID,
                                name = "Delete me",
                                metadata = metadataAttachment("deleted-image", "deleted.png", "Deleted image"),
                            ),
                        ),
                    ),
                ),
            ),
        ).apply {
            failingAttachmentPath = "deleted.png"
        }
        val service = MindMapService(repository)

        assertFails {
            service.applyMutation(
                filePath = "FocusMaps/Root.json",
                mutation = MapMutation.DeleteNode(
                    filePath = "FocusMaps/Root.json",
                    nodeId = DELETE_ID,
                    timestamp = "2026-05-04T10:00:00Z",
                    commitMessage = "map:delete Root $DELETE_ID",
                ),
            ).getOrThrow()
        }

        assertEquals(listOf("deleteAttachment:$DELETE_ID/deleted.png"), repository.operations)
        assertEquals(DELETE_ID, repository.snapshots.getValue("FocusMaps/Root.json").document.rootNode.children.single().uniqueIdentifier)
    }

    private fun snapshot(filePath: String, document: MindMapDocument): MapSnapshot =
        MapSnapshot(
            filePath = filePath,
            fileName = filePath.substringAfterLast('/'),
            mapName = filePath.substringAfterLast('/').removeSuffix(".json"),
            document = document,
            revision = "rev-1",
            loadedAtMillis = 0,
        )

    private fun metadataAttachment(id: String, relativePath: String, displayName: String): NodeMetadata =
        NodeMetadata(
            attachments = listOf(
                NodeAttachment(
                    id = id,
                    relativePath = relativePath,
                    mediaType = "application/octet-stream",
                    displayName = displayName,
                ),
            ),
        )
}

private const val ROOT_ID = "11111111-1111-4111-8111-111111111111"
private const val CHILD_ID = "22222222-2222-4222-8222-222222222222"
private const val DELETE_ID = "33333333-3333-4333-8333-333333333333"
private const val GRANDCHILD_ID = "44444444-4444-4444-8444-444444444444"
private const val KEEP_ID = "55555555-5555-4555-8555-555555555555"

private class FakeMindMapRepository(initialSnapshots: List<MapSnapshot>) : MindMapRepositoryGateway {
    constructor(initialSnapshot: MapSnapshot) : this(listOf(initialSnapshot))

    val snapshots = LinkedHashMap(initialSnapshots.associateBy { it.filePath })
    val operations = mutableListOf<String>()
    val deletedAttachments = mutableListOf<DeletedAttachmentCall>()
    val loadFailures = mutableMapOf<String, Throwable>()
    var failingAttachmentPath: String? = null
    private var saveCount = 0

    override suspend fun listFiles(): Result<List<Pair<String, String>>> =
        Result.success(snapshots.values.map { it.fileName to it.filePath })

    override suspend fun loadMap(filePath: String): Result<MapSnapshot> =
        loadFailures[filePath]?.let(Result.Companion::failure)
            ?: snapshots[filePath]?.let(Result.Companion::success)
            ?: Result.failure(IllegalStateException("Missing map $filePath"))

    override suspend fun saveMap(
        filePath: String,
        document: MindMapDocument,
        revision: String?,
        commitMessage: String,
    ): Result<String> {
        operations += "saveMap:$filePath"
        val nextRevision = "saved-${++saveCount}"
        val current = snapshots.getValue(filePath)
        snapshots[filePath] = current.copy(document = document, revision = nextRevision)
        return Result.success(nextRevision)
    }

    override suspend fun createMap(filePath: String, document: MindMapDocument, commitMessage: String): Result<String> =
        Result.failure(UnsupportedOperationException("createMap is not used by these tests"))

    override suspend fun deleteMap(filePath: String, revision: String, commitMessage: String): Result<Unit> {
        operations += "deleteMap:$filePath"
        snapshots.remove(filePath)
        return Result.success(Unit)
    }

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
    ): Result<Unit> {
        operations += "deleteAttachment:$nodeId/$relativePath"
        if (relativePath == failingAttachmentPath) {
            return Result.failure(IllegalStateException("Unable to delete $relativePath"))
        }
        deletedAttachments += DeletedAttachmentCall(nodeId, relativePath, expectedRevision, commitMessage)
        return Result.success(Unit)
    }

    override fun Throwable.isStaleState(): Boolean = false

    override fun Throwable.isNotFound(): Boolean = this is FakeNotFoundException
}

private class FakeNotFoundException(filePath: String) : Exception("Missing map $filePath")

private data class DeletedAttachmentCall(
    val nodeId: String,
    val relativePath: String,
    val expectedRevision: String?,
    val commitMessage: String,
)
