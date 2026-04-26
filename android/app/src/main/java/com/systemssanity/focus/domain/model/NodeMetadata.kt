package com.systemssanity.focus.domain.model

import kotlinx.serialization.SerialName
import kotlinx.serialization.Serializable

object NodeMetadataSources {
    const val Manual = "manual"
    const val ClipboardText = "clipboard-text"
    const val ClipboardImage = "clipboard-image"
    const val LegacyImport = "legacy-import"
}

@Serializable
data class NodeAttachment(
    @SerialName("id") val id: String = "",
    @SerialName("relativePath") val relativePath: String = "",
    @SerialName("mediaType") val mediaType: String = "",
    @SerialName("displayName") val displayName: String = "",
    @SerialName("createdAtUtc") val createdAtUtc: String = ClockProvider.nowIsoSeconds(),
)

@Serializable
data class NodeMetadata(
    @SerialName("createdAtUtc") val createdAtUtc: String = ClockProvider.nowIsoSeconds(),
    @SerialName("updatedAtUtc") val updatedAtUtc: String = createdAtUtc,
    @SerialName("source") val source: String? = NodeMetadataSources.Manual,
    @SerialName("device") val device: String? = DeviceIdentity.defaultDeviceName,
    @SerialName("attachments") val attachments: List<NodeAttachment> = emptyList(),
) {
    fun touched(timestamp: String = ClockProvider.nowIsoSeconds()): NodeMetadata =
        copy(updatedAtUtc = timestamp)
}
