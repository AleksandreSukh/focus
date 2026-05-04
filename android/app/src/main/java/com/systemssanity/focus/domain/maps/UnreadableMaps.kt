package com.systemssanity.focus.domain.maps

import com.systemssanity.focus.domain.model.NodeAttachment

enum class UnreadableMapReason {
    AutoResolveFailed,
    MergeConflict,
    InvalidJson,
    Unknown,
}

data class UnreadableMapEntry(
    val filePath: String,
    val fileName: String,
    val mapName: String,
    val revision: String,
    val reason: UnreadableMapReason,
    val message: String,
    val rawText: String,
)

object UnreadableMaps {
    const val RawMapMediaType = "application/json"
    const val RawMapAttachmentId = "__raw_map__"

    fun fileName(filePath: String): String =
        filePath.substringAfterLast('/').ifBlank { filePath.ifBlank { "map.json" } }

    fun mapName(filePath: String): String =
        fileName(filePath).removeSuffix(".json")

    fun message(fileName: String, reason: UnreadableMapReason): String =
        when (reason) {
            UnreadableMapReason.AutoResolveFailed ->
                "This map has merge conflicts that couldn't be auto-resolved. Repair locally or reset it from GitHub."
            UnreadableMapReason.MergeConflict ->
                "Map \"$fileName\" contains unresolved Git merge markers and cannot be loaded."
            UnreadableMapReason.InvalidJson ->
                "Map \"$fileName\" is not valid JSON and cannot be loaded."
            UnreadableMapReason.Unknown ->
                "Map \"$fileName\" could not be parsed and cannot be loaded."
        }

    fun reasonLabel(reason: UnreadableMapReason): String =
        when (reason) {
            UnreadableMapReason.AutoResolveFailed -> "Automatic merge conflict resolution could not finish safely"
            UnreadableMapReason.MergeConflict -> "Unresolved merge conflict markers were found"
            UnreadableMapReason.InvalidJson -> "Invalid JSON was found"
            UnreadableMapReason.Unknown -> "The map file could not be parsed"
        }

    fun retryLabel(entry: UnreadableMapEntry): String =
        "Retry ${entry.mapName.ifBlank { entry.fileName.ifBlank { entry.filePath } }}"

    fun resetToGitHubLabel(entry: UnreadableMapEntry): String =
        "Reset to GitHub ${entry.mapName.ifBlank { entry.fileName.ifBlank { entry.filePath } }}"

    fun resetSuccessMessage(mapName: String): String =
        "Reset \"$mapName\" to the GitHub version."

    fun resetDeletedMessage(mapName: String): String =
        "\"$mapName\" was deleted from GitHub."

    fun resetStillUnreadableMessage(entry: UnreadableMapEntry): String =
        "${reasonLabel(entry.reason)} in ${entry.fileName.ifBlank { fileName(entry.filePath) }}."

    fun discardedPendingText(count: Int): String =
        when (count) {
            0 -> "No queued local changes were discarded."
            1 -> "1 queued local change for this map was discarded on this device."
            else -> "$count queued local changes for this map were discarded on this device."
        }

    fun rawFileName(entry: UnreadableMapEntry): String {
        val candidate = entry.fileName
            .ifBlank { entry.mapName }
            .ifBlank { fileName(entry.filePath) }
            .ifBlank { "map.json" }
            .trim()
        val withExtension = if (candidate.endsWith(".json", ignoreCase = true)) candidate else "$candidate.json"
        return AttachmentExports.sanitizeFileName(withExtension).ifBlank { "map.json" }
    }

    fun rawBytes(entry: UnreadableMapEntry): ByteArray =
        entry.rawText.toByteArray(Charsets.UTF_8)

    fun viewRawLabel(entry: UnreadableMapEntry): String =
        "View raw file ${rawFileName(entry)}"

    fun downloadRawLabel(entry: UnreadableMapEntry): String =
        "Download raw file ${rawFileName(entry)}"

    fun rawAttachment(entry: UnreadableMapEntry): NodeAttachment =
        NodeAttachment(
            id = RawMapAttachmentId,
            relativePath = rawFileName(entry),
            mediaType = RawMapMediaType,
            displayName = rawFileName(entry),
        )
}
