package com.systemssanity.focus.domain.maps

object CommitMessages {
    fun nodeAdd(mapName: String, shortText: String, kind: String): String =
        "map:add ${if (kind == "task") "task" else "note"} ${mapName.normalizedMapName()} ${shortText.normalized(48)}".trimEnd()

    fun nodeEdit(mapName: String, nodeId: String): String =
        "map:edit ${mapName.normalizedMapName()} ${nodeId.normalized()}"

    fun nodeTaskState(mapName: String, nodeId: String, taskStateWireValue: Int): String {
        val label = when (taskStateWireValue) {
            1 -> "todo"
            2 -> "doing"
            3 -> "done"
            else -> "clear"
        }
        return "map:task ${mapName.normalizedMapName()} ${nodeId.normalized()} -> $label"
    }

    fun nodeHideDone(mapName: String, nodeId: String, hideDoneTasks: Boolean): String =
        "map:hide-done ${mapName.normalizedMapName()} ${nodeId.normalized()} -> ${if (hideDoneTasks) "hide" else "show"}"

    fun nodeDelete(mapName: String, nodeId: String): String =
        "map:delete ${mapName.normalizedMapName()} ${nodeId.normalized()}"

    fun mapCreate(mapName: String): String =
        "map:create ${mapName.normalizedMapName()}"

    fun mapDelete(mapName: String): String =
        "map:drop ${mapName.normalizedMapName()}"

    fun mapRename(oldMapName: String, newMapName: String): String =
        "map:rename ${oldMapName.normalizedMapName()} -> ${newMapName.normalizedMapName()}"

    fun conflictResolve(mapName: String): String =
        "map:resolve ${mapName.normalizedMapName()}"

    fun attachmentAdd(mapName: String, fileName: String): String =
        "map:attach ${mapName.normalizedMapName()} ${fileName.normalized(48)}".trimEnd()

    fun attachmentRemove(mapName: String, fileName: String): String =
        "map:detach ${mapName.normalizedMapName()} ${fileName.normalized(48)}".trimEnd()

    private fun String.normalized(maxLength: Int = Int.MAX_VALUE): String =
        trim().replace(Regex("\\s+"), " ").let { if (it.length > maxLength) it.take(maxLength).trimEnd() else it }

    private fun String.normalizedMapName(): String =
        normalized(48).ifBlank { "map" }
}
