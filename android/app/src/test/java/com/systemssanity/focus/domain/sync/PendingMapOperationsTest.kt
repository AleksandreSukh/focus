package com.systemssanity.focus.domain.sync

import com.systemssanity.focus.data.local.PendingMapOperation
import com.systemssanity.focus.domain.maps.MapMutation
import com.systemssanity.focus.domain.model.MindMapDocument
import com.systemssanity.focus.domain.model.MapSnapshot
import com.systemssanity.focus.domain.model.Node
import com.systemssanity.focus.domain.model.TaskState
import kotlin.test.Test
import kotlin.test.assertEquals
import kotlin.test.assertNull

class PendingMapOperationsTest {
    @Test
    fun optimisticRenameChangesPathAndMapNameAndRemovesOldPath() {
        val snapshot = snapshot("FocusMaps/Old.json", "Old")
        val operation = renameOperation(snapshot, "FocusMaps/New.json", "New")
        val renamed = PendingMapOperations.snapshotForAppliedMutation(
            snapshot = snapshot,
            operation = operation,
            document = snapshot.document.copy(rootNode = snapshot.document.rootNode.copy(name = "New")),
        )

        val replaced = PendingMapOperations.replaceSnapshot(listOf(snapshot), operation, renamed)

        assertEquals(listOf("FocusMaps/New.json"), replaced.map { it.filePath })
        assertEquals("New.json", replaced.single().fileName)
        assertEquals("New", replaced.single().mapName)
        assertEquals("New", replaced.single().document.rootNode.name)
        assertNull(replaced.firstOrNull { it.filePath == "FocusMaps/Old.json" })
    }

    @Test
    fun pendingRenameAllowsLaterMutationToTargetNewPath() {
        val snapshot = snapshot("FocusMaps/Old.json", "Old")
        val rootId = snapshot.document.rootNode.uniqueIdentifier
        val pending = listOf(
            PendingMapOperation(
                id = "rename",
                scope = "scope",
                operation = renameOperation(snapshot, "FocusMaps/New.json", "New"),
                enqueuedAtMillis = 1,
            ),
            PendingMapOperation(
                id = "add-note",
                scope = "scope",
                operation = MapMutation.AddChildNote(
                    filePath = "FocusMaps/New.json",
                    parentNodeId = rootId,
                    newNodeId = "22222222-2222-4222-8222-222222222222",
                    text = "Later note",
                    timestamp = "2026-04-25T10:06:00Z",
                    commitMessage = "map:add note New Later note",
                ),
                enqueuedAtMillis = 2,
            ),
        )

        val applied = PendingMapOperations.applyToSnapshots(listOf(snapshot), pending)

        assertEquals(listOf("FocusMaps/New.json"), applied.map { it.filePath })
        assertEquals("New", applied.single().document.rootNode.name)
        assertEquals("Later note", applied.single().document.rootNode.children.single().name)
        assertEquals(TaskState.None, applied.single().document.rootNode.children.single().taskState)
    }

    private fun snapshot(filePath: String, mapName: String): MapSnapshot =
        MapSnapshot(
            filePath = filePath,
            fileName = filePath.substringAfterLast('/'),
            mapName = mapName,
            document = MindMapDocument(
                rootNode = Node(
                    uniqueIdentifier = "11111111-1111-4111-8111-111111111111",
                    name = mapName,
                ),
            ),
            revision = "rev",
            loadedAtMillis = 0,
        )

    private fun renameOperation(snapshot: MapSnapshot, newFilePath: String, text: String): MapMutation.RenameMap =
        MapMutation.RenameMap(
            filePath = snapshot.filePath,
            newFilePath = newFilePath,
            nodeId = snapshot.document.rootNode.uniqueIdentifier,
            text = text,
            timestamp = "2026-04-25T10:05:00Z",
            commitMessage = "map:rename ${snapshot.mapName} -> $text",
        )
}
