package com.systemssanity.focus.domain.maps

import com.systemssanity.focus.domain.model.MapSnapshot
import com.systemssanity.focus.domain.model.MindMapDocument
import com.systemssanity.focus.domain.model.MindMapJson
import kotlinx.serialization.json.Json
import kotlinx.serialization.json.JsonObject

object LocalMapRepairs {
    private val json = Json {
        ignoreUnknownKeys = true
    }

    fun buildDraft(entry: UnreadableMapEntry, baselineSnapshot: MapSnapshot? = null): String =
        entry.rawText.takeIf { it.isNotBlank() }
            ?: baselineSnapshot?.document?.let(MindMapJson::serialize)
            ?: ""

    fun repairFileName(entry: UnreadableMapEntry): String =
        entry.fileName
            .ifBlank { UnreadableMaps.fileName(entry.filePath) }
            .ifBlank { "map.json" }

    fun repairActionLabel(entry: UnreadableMapEntry): String =
        "Repair locally ${repairFileName(entry)}"

    fun downloadRepairDraftLabel(entry: UnreadableMapEntry): String =
        "Download repair draft ${repairFileName(entry)}"

    fun noDraftMessage(entry: UnreadableMapEntry): String =
        "No local map data is available to repair for \"${unreadableTitle(entry)}\"."

    fun helperText(entry: UnreadableMapEntry): String =
        "${UnreadableMaps.reasonLabel(entry.reason)} in ${repairFileName(entry)}. Save a repaired local copy to unblock this device."

    fun validateRepairJson(rawText: String): Result<MindMapDocument> =
        runCatching {
            if (rawText.trim().isBlank()) {
                error("Map JSON cannot be empty.")
            }

            val element = json.parseToJsonElement(rawText)
            if (element !is JsonObject) {
                error("Map JSON must be an object.")
            }
            if (!element.containsKey("rootNode") && !element.containsKey("RootNode")) {
                error("Map JSON must include a root node.")
            }

            MindMapJson.parse(rawText)
        }.recoverCatching { error ->
            if (error is IllegalStateException && !error.message.isNullOrBlank()) {
                throw error
            }
            throw IllegalStateException(error.message ?: "Map JSON could not be parsed.", error)
        }

    fun buildRepairSnapshot(
        entry: UnreadableMapEntry,
        document: MindMapDocument,
        baselineSnapshot: MapSnapshot? = null,
        loadedAtMillis: Long = System.currentTimeMillis(),
    ): MapSnapshot {
        val fileName = repairFileName(entry)
        return MapSnapshot(
            filePath = entry.filePath,
            fileName = fileName,
            mapName = entry.mapName
                .ifBlank { baselineSnapshot?.mapName.orEmpty() }
                .ifBlank { fileName.removeSuffix(".json") },
            document = MindMapJson.normalize(document),
            revision = entry.revision.ifBlank { baselineSnapshot?.revision.orEmpty() },
            loadedAtMillis = loadedAtMillis,
        )
    }

    private fun unreadableTitle(entry: UnreadableMapEntry): String =
        entry.mapName.ifBlank { entry.fileName.ifBlank { entry.filePath } }
}
