package com.systemssanity.focus.domain.model

import kotlinx.serialization.SerialName
import kotlinx.serialization.Serializable
import java.util.UUID

@Serializable
data class Link(
    @SerialName("id") val id: String,
    @SerialName("relationType") val relationType: Int = 0,
    @SerialName("metadata") val metadata: String? = null,
)

@Serializable
data class Node(
    @SerialName("nodeType") val nodeType: NodeType = NodeType.TextItem,
    @SerialName("uniqueIdentifier") val uniqueIdentifier: String = UUID.randomUUID().toString(),
    @SerialName("name") val name: String = "",
    @SerialName("children") val children: List<Node> = emptyList(),
    @SerialName("links") val links: Map<String, Link> = emptyMap(),
    @SerialName("number") val number: Int = 1,
    @SerialName("collapsed") val collapsed: Boolean = false,
    @SerialName("hideDoneTasks") val hideDoneTasks: Boolean = false,
    @SerialName("hideDoneTasksExplicit") val hideDoneTasksExplicit: Boolean? = null,
    @SerialName("taskState") val taskState: TaskState = TaskState.None,
    @SerialName("metadata") val metadata: NodeMetadata? = null,
) {
    val isIdeaTag: Boolean get() = nodeType == NodeType.IdeaBagItem
    val canChangeTaskState: Boolean get() = !isIdeaTag
    val canEditText: Boolean get() = nodeType.isEditableInMobile

    fun effectiveMetadata(timestamp: String = ClockProvider.nowIsoSeconds()): NodeMetadata =
        metadata ?: NodeMetadata(
            createdAtUtc = timestamp,
            updatedAtUtc = timestamp,
            source = NodeMetadataSources.LegacyImport,
            device = null,
        )
}
