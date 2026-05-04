package com.systemssanity.focus.domain.maps

data class PendingConflictMapEntry(
    val filePath: String,
    val fileName: String,
    val mapName: String,
    val message: String,
)

enum class PendingConflictResolution {
    Local,
    Remote,
}

object PendingConflicts {
    fun build(filePath: String, mapName: String = ""): PendingConflictMapEntry {
        val fileName = MapFilePaths.fileName(filePath)
        val resolvedMapName = mapName.ifBlank { MapFilePaths.mapName(filePath) }
        return PendingConflictMapEntry(
            filePath = filePath,
            fileName = fileName,
            mapName = resolvedMapName,
            message = "Queued local changes conflict with the latest GitHub version and need manual resolution.",
        )
    }

    fun pendingText(pendingCount: Int): String =
        when (pendingCount) {
            0 -> "No queued changes for this map."
            1 -> "1 queued change needs a local or remote choice."
            else -> "$pendingCount queued changes need local or remote choices."
        }

    fun resolveLabel(entry: PendingConflictMapEntry): String =
        "Resolve conflict for ${entry.mapName.ifBlank { entry.fileName.ifBlank { entry.filePath } }}"

    fun retryLabel(entry: PendingConflictMapEntry): String =
        "Retry syncing ${entry.mapName.ifBlank { entry.fileName.ifBlank { entry.filePath } }}"

    fun statusMessage(entry: PendingConflictMapEntry): String =
        "Resolve conflict for \"${entry.mapName.ifBlank { entry.fileName.ifBlank { entry.filePath } }}\" to resume queued changes."

    fun resolvedStatusMessage(entry: PendingConflictMapEntry): String =
        "Conflict in \"${entry.mapName.ifBlank { entry.fileName.ifBlank { entry.filePath } }}\" resolved."

    fun describeOperation(operation: MapMutation, mapName: String): String {
        val label = mapName.ifBlank { MapFilePaths.mapName(operation.resultFilePath()) }
        return when (operation) {
            is MapMutation.RenameMap -> "map rename in $label"
            is MapMutation.EditNodeText -> "text change in $label"
            is MapMutation.SetTaskState -> "task state update in $label"
            is MapMutation.SetStarred -> "star update in $label"
            is MapMutation.SetHideDoneTasks -> "branch task visibility update in $label"
            is MapMutation.AddChildTask -> "new child task in $label"
            is MapMutation.AddChildNote -> "new child note in $label"
            is MapMutation.DeleteNode -> "node removal in $label"
            is MapMutation.AddAttachment -> "attachment added in $label"
            is MapMutation.RemoveAttachment -> "attachment removed in $label"
        }
    }

    fun choiceLabel(choice: PendingConflictResolution): String =
        when (choice) {
            PendingConflictResolution.Local -> "Keep my change"
            PendingConflictResolution.Remote -> "Use remote version"
        }
}
