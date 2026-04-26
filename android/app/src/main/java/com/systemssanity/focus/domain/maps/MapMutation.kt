package com.systemssanity.focus.domain.maps

import com.systemssanity.focus.domain.model.NodeAttachment
import com.systemssanity.focus.domain.model.TaskState
import kotlinx.serialization.Serializable

@Serializable
sealed interface MapMutation {
    val filePath: String
    val timestamp: String
    val commitMessage: String

    @Serializable
    data class EditNodeText(
        override val filePath: String,
        val nodeId: String,
        val text: String,
        override val timestamp: String,
        override val commitMessage: String,
    ) : MapMutation

    @Serializable
    data class SetTaskState(
        override val filePath: String,
        val nodeId: String,
        val taskState: TaskState,
        override val timestamp: String,
        override val commitMessage: String,
    ) : MapMutation

    @Serializable
    data class SetHideDoneTasks(
        override val filePath: String,
        val nodeId: String,
        val hideDoneTasks: Boolean,
        override val timestamp: String,
        override val commitMessage: String,
    ) : MapMutation

    @Serializable
    data class AddChildNote(
        override val filePath: String,
        val parentNodeId: String,
        val newNodeId: String,
        val text: String,
        override val timestamp: String,
        override val commitMessage: String,
    ) : MapMutation

    @Serializable
    data class AddChildTask(
        override val filePath: String,
        val parentNodeId: String,
        val newNodeId: String,
        val text: String,
        override val timestamp: String,
        override val commitMessage: String,
    ) : MapMutation

    @Serializable
    data class DeleteNode(
        override val filePath: String,
        val nodeId: String,
        override val timestamp: String,
        override val commitMessage: String,
    ) : MapMutation

    @Serializable
    data class AddAttachment(
        override val filePath: String,
        val nodeId: String,
        val attachment: NodeAttachment,
        override val timestamp: String,
        override val commitMessage: String,
    ) : MapMutation

    @Serializable
    data class RemoveAttachment(
        override val filePath: String,
        val nodeId: String,
        val attachmentId: String,
        override val timestamp: String,
        override val commitMessage: String,
    ) : MapMutation
}

data class DeletedAttachmentRef(
    val nodeId: String,
    val attachmentId: String,
    val relativePath: String,
    val displayName: String,
)

data class MutationResult(
    val document: com.systemssanity.focus.domain.model.MindMapDocument,
    val affectedNodeId: String,
    val selectedNodeId: String,
    val deletedAttachments: List<DeletedAttachmentRef> = emptyList(),
)

sealed interface MutationApplyResult {
    data class Applied(val result: MutationResult) : MutationApplyResult
    data class Rejected(val code: String, val message: String) : MutationApplyResult
}
