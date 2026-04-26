package com.systemssanity.focus.domain.model

import kotlinx.serialization.SerialName
import kotlinx.serialization.Serializable

@Serializable
data class MindMapDocument(
    @SerialName("rootNode") val rootNode: Node = Node(name = "Root"),
    @SerialName("updatedAt") val updatedAt: String = rootNode.metadata?.updatedAtUtc ?: ClockProvider.nowIsoSeconds(),
)

data class MapSnapshot(
    val filePath: String,
    val fileName: String,
    val mapName: String,
    val document: MindMapDocument,
    val revision: String,
    val loadedAtMillis: Long,
)
