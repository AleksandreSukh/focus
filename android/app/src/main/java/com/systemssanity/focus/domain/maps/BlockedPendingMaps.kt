package com.systemssanity.focus.domain.maps

import com.systemssanity.focus.domain.model.MapSnapshot

data class BlockedPendingMapEntry(
    val filePath: String,
    val fileName: String,
    val mapName: String,
    val message: String,
)

object BlockedPendingMaps {
    fun build(filePath: String, mapName: String = ""): BlockedPendingMapEntry {
        val fileName = UnreadableMaps.fileName(filePath)
        val label = mapName.ifBlank { fileName.removeSuffix(".json") }.ifBlank { filePath }
        return BlockedPendingMapEntry(
            filePath = filePath,
            fileName = fileName,
            mapName = label,
            message = message(label),
        )
    }

    fun build(filePath: String, snapshot: MapSnapshot?): BlockedPendingMapEntry =
        build(filePath = filePath, mapName = snapshot?.mapName.orEmpty())

    fun message(mapName: String): String =
        "Queued changes for \"$mapName\" can't be applied because the repaired map no longer contains the targeted node."

    fun repairActionLabel(entry: BlockedPendingMapEntry): String =
        "Repair locally ${entry.fileName.ifBlank { entry.filePath }}"

    fun resetToGitHubLabel(entry: BlockedPendingMapEntry): String =
        "Reset to GitHub ${entry.mapName.ifBlank { entry.fileName.ifBlank { entry.filePath } }}"

    fun retryLabel(entry: BlockedPendingMapEntry): String =
        "Retry ${entry.mapName.ifBlank { entry.fileName.ifBlank { entry.filePath } }}"

    fun discardLabel(entry: BlockedPendingMapEntry): String =
        "Discard queued changes ${entry.mapName.ifBlank { entry.fileName.ifBlank { entry.filePath } }}"

    fun repairHelperText(entry: BlockedPendingMapEntry): String =
        "${entry.message} Save a repaired local copy to retry the queued changes on this device."

    fun discardStatusMessage(entry: BlockedPendingMapEntry, discardedCount: Int): String =
        when (discardedCount) {
            0 -> "No queued local changes were discarded for \"${entry.mapName}\"."
            1 -> "Discarded 1 queued local change for \"${entry.mapName}\"."
            else -> "Discarded $discardedCount queued local changes for \"${entry.mapName}\"."
        }

    fun toRepairEntry(entry: BlockedPendingMapEntry, baselineSnapshot: MapSnapshot? = null): UnreadableMapEntry =
        UnreadableMapEntry(
            filePath = entry.filePath,
            fileName = entry.fileName,
            mapName = entry.mapName,
            revision = baselineSnapshot?.revision.orEmpty(),
            reason = UnreadableMapReason.Unknown,
            message = entry.message,
            rawText = "",
        )
}
