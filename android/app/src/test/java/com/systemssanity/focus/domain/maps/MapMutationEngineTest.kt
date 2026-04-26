package com.systemssanity.focus.domain.maps

import com.systemssanity.focus.domain.model.ClockProvider
import com.systemssanity.focus.domain.model.MindMapDocument
import com.systemssanity.focus.domain.model.Node
import com.systemssanity.focus.domain.model.NodeAttachment
import com.systemssanity.focus.domain.model.NodeMetadata
import com.systemssanity.focus.domain.model.TaskState
import kotlin.test.Test
import kotlin.test.assertEquals
import kotlin.test.assertIs

class MapMutationEngineTest {
    @Test
    fun rejectsTaskStateChangesOnRoot() {
        val document = MindMapDocument(rootNode = Node(name = "Root"))
        val result = MapMutationEngine.apply(
            document,
            MapMutation.SetTaskState(
                filePath = "FocusMaps/Root.json",
                nodeId = document.rootNode.uniqueIdentifier,
                taskState = TaskState.Done,
                timestamp = ClockProvider.nowIsoSeconds(),
                commitMessage = "map:task Root root -> done",
            ),
        )

        assertIs<MutationApplyResult.Rejected>(result)
    }

    @Test
    fun addsChildTaskUnderSelectedNode() {
        val document = MindMapDocument(rootNode = Node(name = "Root"))
        val result = MapMutationEngine.apply(
            document,
            MapMutation.AddChildTask(
                filePath = "FocusMaps/Root.json",
                parentNodeId = document.rootNode.uniqueIdentifier,
                newNodeId = "33333333-3333-4333-8333-333333333333",
                text = "Ship app",
                timestamp = ClockProvider.nowIsoSeconds(),
                commitMessage = "map:add task Root Ship app",
            ),
        )

        val applied = assertIs<MutationApplyResult.Applied>(result)
        val child = applied.result.document.rootNode.children.single()
        assertEquals("Ship app", child.name)
        assertEquals(TaskState.Todo, child.taskState)
    }

    @Test
    fun editsNodeTextAndTouchesMetadata() {
        val child = Node(
            uniqueIdentifier = "22222222-2222-4222-8222-222222222222",
            name = "Old text",
            metadata = NodeMetadata(createdAtUtc = "2026-04-25T10:00:00Z", updatedAtUtc = "2026-04-25T10:00:00Z"),
        )
        val document = MindMapDocument(rootNode = Node(name = "Root", children = listOf(child)))

        val result = MapMutationEngine.apply(
            document,
            MapMutation.EditNodeText(
                filePath = "FocusMaps/Root.json",
                nodeId = child.uniqueIdentifier,
                text = "New text",
                timestamp = "2026-04-25T10:05:00Z",
                commitMessage = "map:edit Root child",
            ),
        )

        val applied = assertIs<MutationApplyResult.Applied>(result)
        val edited = applied.result.document.rootNode.children.single()
        assertEquals("New text", edited.name)
        assertEquals("2026-04-25T10:05:00Z", edited.metadata?.updatedAtUtc)
    }

    @Test
    fun deletesNodeRenumbersSiblingsAndReturnsDeletedAttachments() {
        val deleted = Node(
            uniqueIdentifier = "22222222-2222-4222-8222-222222222222",
            name = "Delete me",
            number = 1,
            metadata = NodeMetadata(
                attachments = listOf(
                    NodeAttachment(
                        id = "attachment-1",
                        relativePath = "image.png",
                        mediaType = "image/png",
                        displayName = "Image",
                    ),
                ),
            ),
        )
        val remaining = Node(
            uniqueIdentifier = "33333333-3333-4333-8333-333333333333",
            name = "Keep me",
            number = 2,
        )
        val document = MindMapDocument(rootNode = Node(name = "Root", children = listOf(deleted, remaining)))

        val result = MapMutationEngine.apply(
            document,
            MapMutation.DeleteNode(
                filePath = "FocusMaps/Root.json",
                nodeId = deleted.uniqueIdentifier,
                timestamp = "2026-04-25T10:05:00Z",
                commitMessage = "map:delete Root child",
            ),
        )

        val applied = assertIs<MutationApplyResult.Applied>(result)
        val child = applied.result.document.rootNode.children.single()
        assertEquals("Keep me", child.name)
        assertEquals(1, child.number)
        assertEquals("image.png", applied.result.deletedAttachments.single().relativePath)
    }

    @Test
    fun setHideDoneClearsDescendantOverrides() {
        val descendant = Node(
            uniqueIdentifier = "33333333-3333-4333-8333-333333333333",
            name = "Descendant",
            hideDoneTasks = true,
            hideDoneTasksExplicit = true,
        )
        val parent = Node(
            uniqueIdentifier = "22222222-2222-4222-8222-222222222222",
            name = "Parent",
            children = listOf(descendant),
        )
        val document = MindMapDocument(rootNode = Node(name = "Root", children = listOf(parent)))

        val result = MapMutationEngine.apply(
            document,
            MapMutation.SetHideDoneTasks(
                filePath = "FocusMaps/Root.json",
                nodeId = parent.uniqueIdentifier,
                hideDoneTasks = true,
                timestamp = "2026-04-25T10:05:00Z",
                commitMessage = "map:hide-done Root parent -> hide",
            ),
        )

        val applied = assertIs<MutationApplyResult.Applied>(result)
        val updatedParent = applied.result.document.rootNode.children.single()
        val updatedDescendant = updatedParent.children.single()
        assertEquals(true, updatedParent.hideDoneTasks)
        assertEquals(true, updatedParent.hideDoneTasksExplicit)
        assertEquals(false, updatedDescendant.hideDoneTasks)
        assertEquals(null, updatedDescendant.hideDoneTasksExplicit)
    }
}
