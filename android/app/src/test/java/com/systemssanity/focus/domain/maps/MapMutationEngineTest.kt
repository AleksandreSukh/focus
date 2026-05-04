package com.systemssanity.focus.domain.maps

import com.systemssanity.focus.domain.model.ClockProvider
import com.systemssanity.focus.domain.model.MindMapDocument
import com.systemssanity.focus.domain.model.Node
import com.systemssanity.focus.domain.model.NodeAttachment
import com.systemssanity.focus.domain.model.NodeMetadata
import com.systemssanity.focus.domain.model.NodeType
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
    fun addsChildNoteUnderSelectedNode() {
        val document = MindMapDocument(rootNode = Node(name = "Root"))
        val result = MapMutationEngine.apply(
            document,
            MapMutation.AddChildNote(
                filePath = "FocusMaps/Root.json",
                parentNodeId = document.rootNode.uniqueIdentifier,
                newNodeId = "22222222-2222-4222-8222-222222222222",
                text = "Capture idea",
                timestamp = ClockProvider.nowIsoSeconds(),
                commitMessage = "map:add note Root Capture idea",
            ),
        )

        val applied = assertIs<MutationApplyResult.Applied>(result)
        val child = applied.result.document.rootNode.children.single()
        assertEquals("Capture idea", child.name)
        assertEquals(TaskState.None, child.taskState)
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
    fun renamesMapByEditingRootNodeTextAndTouchingMetadata() {
        val root = Node(
            uniqueIdentifier = "11111111-1111-4111-8111-111111111111",
            name = "Old map",
            metadata = NodeMetadata(createdAtUtc = "2026-04-25T10:00:00Z", updatedAtUtc = "2026-04-25T10:00:00Z"),
        )
        val document = MindMapDocument(rootNode = root)

        val result = MapMutationEngine.apply(
            document,
            MapMutation.RenameMap(
                filePath = "FocusMaps/Old map.json",
                newFilePath = "FocusMaps/New map.json",
                nodeId = root.uniqueIdentifier,
                text = "New map",
                timestamp = "2026-04-25T10:05:00Z",
                commitMessage = "map:rename Old map -> New map",
            ),
        )

        val applied = assertIs<MutationApplyResult.Applied>(result)
        assertEquals("New map", applied.result.document.rootNode.name)
        assertEquals("2026-04-25T10:05:00Z", applied.result.document.updatedAt)
        assertEquals("2026-04-25T10:05:00Z", applied.result.document.rootNode.metadata?.updatedAtUtc)
        assertEquals(root.uniqueIdentifier, applied.result.selectedNodeId)
    }

    @Test
    fun renameMapRejectsBlankAndNonRootTargets() {
        val child = Node(uniqueIdentifier = "22222222-2222-4222-8222-222222222222", name = "Child")
        val document = MindMapDocument(
            rootNode = Node(uniqueIdentifier = "11111111-1111-4111-8111-111111111111", name = "Root", children = listOf(child)),
        )

        val blankResult = MapMutationEngine.apply(
            document,
            MapMutation.RenameMap(
                filePath = "FocusMaps/Root.json",
                newFilePath = "FocusMaps/Untitled.json",
                nodeId = document.rootNode.uniqueIdentifier,
                text = " ",
                timestamp = "2026-04-25T10:05:00Z",
                commitMessage = "map:rename Root -> Untitled",
            ),
        )
        val childResult = MapMutationEngine.apply(
            document,
            MapMutation.RenameMap(
                filePath = "FocusMaps/Root.json",
                newFilePath = "FocusMaps/Child.json",
                nodeId = child.uniqueIdentifier,
                text = "Child",
                timestamp = "2026-04-25T10:05:00Z",
                commitMessage = "map:rename Root -> Child",
            ),
        )

        assertIs<MutationApplyResult.Rejected>(blankResult)
        assertIs<MutationApplyResult.Rejected>(childResult)
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
                        id = "11111111-1111-4111-8111-111111111111",
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
    fun addAttachmentAddsOrReplacesMetadataAndTouchesNode() {
        val attachmentId = "11111111-1111-4111-8111-111111111111"
        val child = Node(
            uniqueIdentifier = "22222222-2222-4222-8222-222222222222",
            name = "Child",
            metadata = NodeMetadata(
                createdAtUtc = "2026-04-25T10:00:00Z",
                updatedAtUtc = "2026-04-25T10:00:00Z",
                attachments = listOf(
                    NodeAttachment(
                        id = attachmentId,
                        relativePath = "old.png",
                        mediaType = "image/png",
                        displayName = "Old image",
                    ),
                ),
            ),
        )
        val document = MindMapDocument(rootNode = Node(name = "Root", children = listOf(child)))

        val result = MapMutationEngine.apply(
            document,
            MapMutation.AddAttachment(
                filePath = "FocusMaps/Root.json",
                nodeId = child.uniqueIdentifier,
                attachment = NodeAttachment(
                    id = attachmentId,
                    relativePath = "new.png",
                    mediaType = "image/png",
                    displayName = "New image",
                    createdAtUtc = "2026-04-25T10:05:00Z",
                ),
                timestamp = "2026-04-25T10:05:00Z",
                commitMessage = "map:attach Root New image",
            ),
        )

        val applied = assertIs<MutationApplyResult.Applied>(result)
        val updated = applied.result.document.rootNode.children.single()
        val attachment = updated.metadata?.attachments?.single()
        assertEquals("new.png", attachment?.relativePath)
        assertEquals("New image", attachment?.displayName)
        assertEquals("2026-04-25T10:05:00Z", updated.metadata?.updatedAtUtc)
        assertEquals("2026-04-25T10:05:00Z", applied.result.document.updatedAt)
    }

    @Test
    fun addAttachmentRejectsBlankRelativePath() {
        val child = Node(uniqueIdentifier = "22222222-2222-4222-8222-222222222222", name = "Child")
        val document = MindMapDocument(rootNode = Node(name = "Root", children = listOf(child)))

        val result = MapMutationEngine.apply(
            document,
            MapMutation.AddAttachment(
                filePath = "FocusMaps/Root.json",
                nodeId = child.uniqueIdentifier,
                attachment = NodeAttachment(
                    id = "11111111-1111-4111-8111-111111111111",
                    relativePath = " ",
                    mediaType = "image/png",
                    displayName = "Image",
                ),
                timestamp = "2026-04-25T10:05:00Z",
                commitMessage = "map:attach Root Image",
            ),
        )

        assertIs<MutationApplyResult.Rejected>(result)
    }

    @Test
    fun removeAttachmentRemovesMetadataAndTouchesNode() {
        val child = Node(
            uniqueIdentifier = "22222222-2222-4222-8222-222222222222",
            name = "Child",
            metadata = NodeMetadata(
                createdAtUtc = "2026-04-25T10:00:00Z",
                updatedAtUtc = "2026-04-25T10:00:00Z",
                attachments = listOf(
                    NodeAttachment(
                        id = "11111111-1111-4111-8111-111111111111",
                        relativePath = "old.png",
                        mediaType = "image/png",
                        displayName = "Old image",
                    ),
                    NodeAttachment(
                        id = "33333333-3333-4333-8333-333333333333",
                        relativePath = "keep.png",
                        mediaType = "image/png",
                        displayName = "Keep image",
                    ),
                ),
            ),
        )
        val document = MindMapDocument(rootNode = Node(name = "Root", children = listOf(child)))

        val result = MapMutationEngine.apply(
            document,
            MapMutation.RemoveAttachment(
                filePath = "FocusMaps/Root.json",
                nodeId = child.uniqueIdentifier,
                attachmentId = "11111111-1111-4111-8111-111111111111",
                timestamp = "2026-04-25T10:05:00Z",
                commitMessage = "map:detach Root Old image",
            ),
        )

        val applied = assertIs<MutationApplyResult.Applied>(result)
        val updated = applied.result.document.rootNode.children.single()
        val attachment = updated.metadata?.attachments?.single()
        assertEquals("33333333-3333-4333-8333-333333333333", attachment?.id)
        assertEquals("2026-04-25T10:05:00Z", updated.metadata?.updatedAtUtc)
        assertEquals("2026-04-25T10:05:00Z", applied.result.document.updatedAt)
    }

    @Test
    fun removeAttachmentRejectsMissingAttachmentOrNode() {
        val child = Node(
            uniqueIdentifier = "22222222-2222-4222-8222-222222222222",
            name = "Child",
            metadata = NodeMetadata(
                attachments = listOf(
                    NodeAttachment(
                        id = "11111111-1111-4111-8111-111111111111",
                        relativePath = "old.png",
                        mediaType = "image/png",
                        displayName = "Old image",
                    ),
                ),
            ),
        )
        val document = MindMapDocument(rootNode = Node(name = "Root", children = listOf(child)))

        val missingAttachment = MapMutationEngine.apply(
            document,
            MapMutation.RemoveAttachment(
                filePath = "FocusMaps/Root.json",
                nodeId = child.uniqueIdentifier,
                attachmentId = "missing",
                timestamp = "2026-04-25T10:05:00Z",
                commitMessage = "map:detach Root Old image",
            ),
        )
        val missingNode = MapMutationEngine.apply(
            document,
            MapMutation.RemoveAttachment(
                filePath = "FocusMaps/Root.json",
                nodeId = "missing-node",
                attachmentId = "11111111-1111-4111-8111-111111111111",
                timestamp = "2026-04-25T10:05:00Z",
                commitMessage = "map:detach Root Old image",
            ),
        )

        assertIs<MutationApplyResult.Rejected>(missingAttachment)
        assertIs<MutationApplyResult.Rejected>(missingNode)
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

    @Test
    fun starringChildSetsStarredMovesBeforeSiblingsAndRenumbers() {
        val first = Node(uniqueIdentifier = "11111111-1111-4111-8111-111111111111", name = "First", number = 1)
        val second = Node(uniqueIdentifier = "22222222-2222-4222-8222-222222222222", name = "Second", number = 2)
        val third = Node(uniqueIdentifier = "33333333-3333-4333-8333-333333333333", name = "Third", number = 3)
        val document = MindMapDocument(
            rootNode = Node(
                uniqueIdentifier = "aaaaaaaa-aaaa-4aaa-8aaa-aaaaaaaaaaaa",
                name = "Root",
                children = listOf(first, second, third),
            ),
        )

        val result = MapMutationEngine.apply(
            document,
            MapMutation.SetStarred(
                filePath = "FocusMaps/Root.json",
                nodeId = second.uniqueIdentifier,
                starred = true,
                timestamp = "2026-04-25T10:05:00Z",
                commitMessage = "map:star Root ${second.uniqueIdentifier} -> starred",
            ),
        )

        val applied = assertIs<MutationApplyResult.Applied>(result)
        val children = applied.result.document.rootNode.children
        assertEquals(listOf(second.uniqueIdentifier, first.uniqueIdentifier, third.uniqueIdentifier), children.map { it.uniqueIdentifier })
        assertEquals(listOf(1, 2, 3), children.map { it.number })
        assertEquals(true, children.first().starred)
        assertEquals(second.uniqueIdentifier, applied.result.selectedNodeId)
    }

    @Test
    fun starringSecondChildMovesBeforeExistingStarredSibling() {
        val first = Node(uniqueIdentifier = "11111111-1111-4111-8111-111111111111", name = "First", number = 1, starred = true)
        val second = Node(uniqueIdentifier = "22222222-2222-4222-8222-222222222222", name = "Second", number = 2)
        val third = Node(uniqueIdentifier = "33333333-3333-4333-8333-333333333333", name = "Third", number = 3)
        val document = MindMapDocument(rootNode = Node(name = "Root", children = listOf(first, second, third)))

        val result = MapMutationEngine.apply(
            document,
            MapMutation.SetStarred(
                filePath = "FocusMaps/Root.json",
                nodeId = second.uniqueIdentifier,
                starred = true,
                timestamp = "2026-04-25T10:05:00Z",
                commitMessage = "map:star Root ${second.uniqueIdentifier} -> starred",
            ),
        )

        val applied = assertIs<MutationApplyResult.Applied>(result)
        assertEquals(
            listOf(second.uniqueIdentifier, first.uniqueIdentifier, third.uniqueIdentifier),
            applied.result.document.rootNode.children.map { it.uniqueIdentifier },
        )
    }

    @Test
    fun unstarringChildMovesBelowRemainingStarredSiblings() {
        val first = Node(uniqueIdentifier = "11111111-1111-4111-8111-111111111111", name = "First", number = 1, starred = true)
        val second = Node(uniqueIdentifier = "22222222-2222-4222-8222-222222222222", name = "Second", number = 2, starred = true)
        val third = Node(uniqueIdentifier = "33333333-3333-4333-8333-333333333333", name = "Third", number = 3)
        val document = MindMapDocument(rootNode = Node(name = "Root", children = listOf(first, second, third)))

        val result = MapMutationEngine.apply(
            document,
            MapMutation.SetStarred(
                filePath = "FocusMaps/Root.json",
                nodeId = second.uniqueIdentifier,
                starred = false,
                timestamp = "2026-04-25T10:05:00Z",
                commitMessage = "map:star Root ${second.uniqueIdentifier} -> unstarred",
            ),
        )

        val applied = assertIs<MutationApplyResult.Applied>(result)
        val children = applied.result.document.rootNode.children
        assertEquals(listOf(first.uniqueIdentifier, second.uniqueIdentifier, third.uniqueIdentifier), children.map { it.uniqueIdentifier })
        assertEquals(listOf(true, false, false), children.map { it.starred })
        assertEquals(listOf(1, 2, 3), children.map { it.number })
    }

    @Test
    fun starringRejectsRootAndIdeaTargets() {
        val idea = Node(
            uniqueIdentifier = "22222222-2222-4222-8222-222222222222",
            name = "Idea",
            nodeType = NodeType.IdeaBagItem,
        )
        val document = MindMapDocument(rootNode = Node(name = "Root", children = listOf(idea)))

        val rootResult = MapMutationEngine.apply(
            document,
            MapMutation.SetStarred(
                filePath = "FocusMaps/Root.json",
                nodeId = document.rootNode.uniqueIdentifier,
                starred = true,
                timestamp = "2026-04-25T10:05:00Z",
                commitMessage = "map:star Root root -> starred",
            ),
        )
        val ideaResult = MapMutationEngine.apply(
            document,
            MapMutation.SetStarred(
                filePath = "FocusMaps/Root.json",
                nodeId = idea.uniqueIdentifier,
                starred = true,
                timestamp = "2026-04-25T10:05:00Z",
                commitMessage = "map:star Root ${idea.uniqueIdentifier} -> starred",
            ),
        )

        assertIs<MutationApplyResult.Rejected>(rootResult)
        assertIs<MutationApplyResult.Rejected>(ideaResult)
    }
}
