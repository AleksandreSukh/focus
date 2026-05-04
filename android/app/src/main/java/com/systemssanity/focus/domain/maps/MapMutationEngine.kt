package com.systemssanity.focus.domain.maps

import com.systemssanity.focus.domain.model.DeviceIdentity
import com.systemssanity.focus.domain.model.MindMapDocument
import com.systemssanity.focus.domain.model.MindMapJson
import com.systemssanity.focus.domain.model.Node
import com.systemssanity.focus.domain.model.NodeMetadata
import com.systemssanity.focus.domain.model.NodeMetadataSources
import com.systemssanity.focus.domain.model.TaskState
import java.util.UUID

object MapMutationEngine {
    fun apply(document: MindMapDocument, mutation: MapMutation): MutationApplyResult {
        val normalized = MindMapJson.normalize(document)
        return when (mutation) {
            is MapMutation.EditNodeText -> editNodeText(normalized, mutation)
            is MapMutation.RenameMap -> renameMap(normalized, mutation)
            is MapMutation.SetTaskState -> setTaskState(normalized, mutation)
            is MapMutation.SetHideDoneTasks -> setHideDoneTasks(normalized, mutation)
            is MapMutation.SetStarred -> setStarred(normalized, mutation)
            is MapMutation.AddChildNote -> addChild(normalized, mutation, TaskState.None)
            is MapMutation.AddChildTask -> addChild(normalized, mutation, TaskState.Todo)
            is MapMutation.DeleteNode -> deleteNode(normalized, mutation)
            is MapMutation.AddAttachment -> addAttachment(normalized, mutation)
            is MapMutation.RemoveAttachment -> removeAttachment(normalized, mutation)
        }
    }

    private fun editNodeText(document: MindMapDocument, mutation: MapMutation.EditNodeText): MutationApplyResult {
        val text = sanitizeInput(mutation.text)
        if (text.isBlank()) return rejected("VALIDATION_ERROR", "Node text cannot be empty.")
        return updateNode(document, mutation.nodeId, timestamp = mutation.timestamp) { node ->
            if (!node.canEditText) return@updateNode rejected("VALIDATION_ERROR", "Idea-tag nodes are read-only.")
            node.copy(name = text).touched(mutation.timestamp)
        }
    }

    private fun renameMap(document: MindMapDocument, mutation: MapMutation.RenameMap): MutationApplyResult {
        val text = sanitizeInput(mutation.text)
        if (text.isBlank()) return rejected("VALIDATION_ERROR", "Map title cannot be empty.")

        val root = document.rootNode
        if (root.uniqueIdentifier != mutation.nodeId) {
            return rejected("VALIDATION_ERROR", "Only the root node can rename the map.")
        }
        if (!root.canEditText) {
            return rejected("VALIDATION_ERROR", "Root node is read-only.")
        }

        return applied(
            document.copy(rootNode = root.copy(name = text).touched(mutation.timestamp), updatedAt = mutation.timestamp),
            affectedNodeId = mutation.nodeId,
            selectedNodeId = mutation.nodeId,
        )
    }

    private fun setTaskState(document: MindMapDocument, mutation: MapMutation.SetTaskState): MutationApplyResult {
        if (document.rootNode.uniqueIdentifier == mutation.nodeId) {
            return rejected("VALIDATION_ERROR", "Can't change task state for root node.")
        }
        return updateNode(document, mutation.nodeId, timestamp = mutation.timestamp) { node ->
            if (node.isIdeaTag) return@updateNode rejected("VALIDATION_ERROR", "Task mode is not supported for idea tags.")
            node.copy(taskState = mutation.taskState).touched(mutation.timestamp)
        }
    }

    private fun setHideDoneTasks(document: MindMapDocument, mutation: MapMutation.SetHideDoneTasks): MutationApplyResult =
        updateNode(document, mutation.nodeId, timestamp = mutation.timestamp) { node ->
            if (node.isIdeaTag) return@updateNode rejected("VALIDATION_ERROR", "Hide done tasks is not supported for idea tags.")
            node.copy(
                hideDoneTasks = mutation.hideDoneTasks,
                hideDoneTasksExplicit = true,
                children = node.children.map { clearHideDoneOverrides(it, mutation.timestamp) },
            ).touched(mutation.timestamp)
        }

    private fun setStarred(document: MindMapDocument, mutation: MapMutation.SetStarred): MutationApplyResult {
        if (document.rootNode.uniqueIdentifier == mutation.nodeId) {
            return rejected("VALIDATION_ERROR", "Can't change starred state for root node.")
        }

        var found = false
        var rejection: MutationApplyResult.Rejected? = null

        fun visit(parent: Node): Node {
            val childIndex = parent.children.indexOfFirst { it.uniqueIdentifier == mutation.nodeId }
            if (childIndex >= 0) {
                found = true
                val target = parent.children[childIndex]
                if (target.isIdeaTag) {
                    rejection = rejected("VALIDATION_ERROR", "Starred state is not supported for idea tags.")
                    return parent
                }

                val updatedTarget = target.copy(starred = mutation.starred).touched(mutation.timestamp)
                val remainingChildren = parent.children.filterIndexed { index, _ -> index != childIndex }
                val insertIndex = if (mutation.starred) {
                    firstSelectableChildIndex(remainingChildren)
                } else {
                    unstarredInsertionIndex(remainingChildren)
                }
                val nextChildren = remainingChildren.toMutableList().apply {
                    add(insertIndex, updatedTarget)
                }
                return parent.copy(children = nextChildren).renumberedChildren().touched(mutation.timestamp)
            }

            var changed = false
            val nextChildren = parent.children.map { child ->
                val nextChild = visit(child)
                if (nextChild != child) changed = true
                nextChild
            }
            return if (changed) parent.copy(children = nextChildren) else parent
        }

        val nextRoot = visit(document.rootNode)
        rejection?.let { return it }
        if (!found) return rejected("NOT_FOUND", "Node \"${mutation.nodeId}\" was not found.")
        return applied(
            document.copy(rootNode = nextRoot, updatedAt = mutation.timestamp),
            affectedNodeId = mutation.nodeId,
            selectedNodeId = mutation.nodeId,
        )
    }

    private fun addChild(document: MindMapDocument, mutation: MapMutation, taskState: TaskState): MutationApplyResult {
        val parentId = when (mutation) {
            is MapMutation.AddChildNote -> mutation.parentNodeId
            is MapMutation.AddChildTask -> mutation.parentNodeId
            else -> error("Unsupported add mutation")
        }
        val text = when (mutation) {
            is MapMutation.AddChildNote -> mutation.text
            is MapMutation.AddChildTask -> mutation.text
            else -> ""
        }.let(::sanitizeInput)
        val newNodeId = when (mutation) {
            is MapMutation.AddChildNote -> mutation.newNodeId
            is MapMutation.AddChildTask -> mutation.newNodeId
            else -> UUID.randomUUID().toString()
        }.ifBlank { UUID.randomUUID().toString() }

        if (text.isBlank()) return rejected("VALIDATION_ERROR", "Child node text cannot be empty.")
        return updateNode(document, parentId, selectedNodeOverride = newNodeId, timestamp = mutation.timestamp) { parent ->
            if (parent.isIdeaTag) return@updateNode rejected("VALIDATION_ERROR", "Idea-tag nodes are read-only.")
            val child = Node(
                uniqueIdentifier = newNodeId,
                name = text,
                number = parent.children.size + 1,
                taskState = taskState,
                metadata = NodeMetadata(
                    createdAtUtc = mutation.timestamp,
                    updatedAtUtc = mutation.timestamp,
                    source = NodeMetadataSources.Manual,
                    device = DeviceIdentity.defaultDeviceName,
                ),
            )
            parent.copy(children = parent.children + child).renumberedChildren().touched(mutation.timestamp)
        }
    }

    private fun deleteNode(document: MindMapDocument, mutation: MapMutation.DeleteNode): MutationApplyResult {
        if (document.rootNode.uniqueIdentifier == mutation.nodeId) {
            return rejected("VALIDATION_ERROR", "Cannot delete the root node.")
        }
        val deleted = findNode(document.rootNode, mutation.nodeId)
            ?: return rejected("NOT_FOUND", "Node \"${mutation.nodeId}\" was not found.")
        if (deleted.isIdeaTag) return rejected("VALIDATION_ERROR", "Idea-tag nodes cannot be deleted.")

        var parentId = ""
        val nextRoot = removeChild(document.rootNode, mutation.nodeId, mutation.timestamp) { parent ->
            parentId = parent.uniqueIdentifier
        } ?: return rejected("NOT_FOUND", "Node \"${mutation.nodeId}\" was not found.")

        return applied(
            document.copy(rootNode = nextRoot, updatedAt = mutation.timestamp),
            affectedNodeId = parentId,
            selectedNodeId = parentId,
            deletedAttachments = collectDeletedAttachmentRefs(deleted),
        )
    }

    private fun addAttachment(document: MindMapDocument, mutation: MapMutation.AddAttachment): MutationApplyResult =
        updateNode(document, mutation.nodeId, timestamp = mutation.timestamp) { node ->
            if (mutation.attachment.relativePath.isBlank()) {
                return@updateNode rejected("VALIDATION_ERROR", "Attachment must have a relativePath.")
            }
            val metadata = node.effectiveMetadata(mutation.timestamp)
            node.copy(
                metadata = metadata.copy(
                    attachments = metadata.attachments
                        .filterNot { it.id == mutation.attachment.id }
                        .plus(mutation.attachment),
                ).touched(mutation.timestamp),
            )
        }

    private fun removeAttachment(document: MindMapDocument, mutation: MapMutation.RemoveAttachment): MutationApplyResult =
        updateNode(document, mutation.nodeId, timestamp = mutation.timestamp) { node ->
            val metadata = node.effectiveMetadata(mutation.timestamp)
            val nextAttachments = metadata.attachments.filterNot { it.id == mutation.attachmentId }
            if (nextAttachments.size == metadata.attachments.size) {
                return@updateNode rejected("VALIDATION_ERROR", "Attachment \"${mutation.attachmentId}\" not found on node.")
            }
            node.copy(metadata = metadata.copy(attachments = nextAttachments).touched(mutation.timestamp))
        }

    private fun updateNode(
        document: MindMapDocument,
        nodeId: String,
        selectedNodeOverride: String = nodeId,
        timestamp: String,
        transform: (Node) -> Any,
    ): MutationApplyResult {
        var found = false
        var rejection: MutationApplyResult.Rejected? = null
        fun visit(node: Node): Node {
            if (node.uniqueIdentifier == nodeId) {
                found = true
                return when (val transformed = transform(node)) {
                    is Node -> transformed
                    is MutationApplyResult.Rejected -> {
                        rejection = transformed
                        node
                    }
                    else -> node
                }
            }
            return node.copy(children = node.children.map(::visit))
        }

        val nextRoot = visit(document.rootNode)
        rejection?.let { return it }
        if (!found) return rejected("NOT_FOUND", "Node \"$nodeId\" was not found.")
        return applied(
            document.copy(rootNode = nextRoot, updatedAt = timestamp),
            affectedNodeId = nodeId,
            selectedNodeId = selectedNodeOverride,
        )
    }

    private fun removeChild(
        node: Node,
        nodeId: String,
        timestamp: String,
        onParent: (Node) -> Unit,
    ): Node? {
        val childIndex = node.children.indexOfFirst { it.uniqueIdentifier == nodeId }
        if (childIndex >= 0) {
            onParent(node)
            return node.copy(
                children = node.children.filterIndexed { index, _ -> index != childIndex },
            ).renumberedChildren().touched(timestamp)
        }

        var changed = false
        val nextChildren = node.children.map { child ->
            val removed = removeChild(child, nodeId, timestamp, onParent)
            if (removed != null && removed != child) changed = true
            removed ?: child
        }
        return if (changed) node.copy(children = nextChildren).touched(timestamp) else node
    }

    private fun clearHideDoneOverrides(node: Node, timestamp: String): Node =
        node.copy(
            hideDoneTasks = false,
            hideDoneTasksExplicit = null,
            children = node.children.map { clearHideDoneOverrides(it, timestamp) },
        ).touched(timestamp)

    private fun Node.touched(timestamp: String): Node =
        copy(metadata = effectiveMetadata(timestamp).touched(timestamp))

    private fun Node.renumberedChildren(): Node =
        copy(children = children.mapIndexed { index, child -> child.copy(number = index + 1) })

    private fun firstSelectableChildIndex(children: List<Node>): Int =
        children.indexOfFirst { !it.isIdeaTag }.takeIf { it >= 0 } ?: children.size

    private fun unstarredInsertionIndex(children: List<Node>): Int {
        for (index in children.indices.reversed()) {
            val child = children[index]
            if (!child.isIdeaTag && child.starred) {
                return index + 1
            }
        }
        return firstSelectableChildIndex(children)
    }

    private fun applied(
        document: MindMapDocument,
        affectedNodeId: String,
        selectedNodeId: String,
        deletedAttachments: List<DeletedAttachmentRef> = emptyList(),
    ): MutationApplyResult.Applied =
        MutationApplyResult.Applied(
            MutationResult(
                document = MindMapJson.normalize(document),
                affectedNodeId = affectedNodeId,
                selectedNodeId = selectedNodeId,
                deletedAttachments = deletedAttachments,
            ),
        )

    private fun rejected(code: String, message: String): MutationApplyResult.Rejected =
        MutationApplyResult.Rejected(code, message)

    private fun sanitizeInput(input: String): String =
        input.trim().filter { !it.isISOControl() || it == '\r' || it == '\n' || it == '\t' }

    private fun findNode(node: Node, nodeId: String): Node? {
        if (node.uniqueIdentifier == nodeId) return node
        return node.children.firstNotNullOfOrNull { findNode(it, nodeId) }
    }

}
