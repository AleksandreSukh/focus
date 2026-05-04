package com.systemssanity.focus.domain.maps

import com.systemssanity.focus.domain.model.MapSnapshot
import com.systemssanity.focus.domain.model.MindMapDocument
import com.systemssanity.focus.domain.model.Node
import kotlin.test.Test
import kotlin.test.assertEquals
import kotlin.test.assertTrue

class LocalMapRepairsTest {
    @Test
    fun buildDraftUsesRawTextThenBaselineSnapshot() {
        val entry = entry(rawText = "{\"rootNode\":")
        val baseline = snapshot("FocusMaps/Broken.json", "Baseline")

        assertEquals("{\"rootNode\":", LocalMapRepairs.buildDraft(entry, baseline))
        assertTrue(LocalMapRepairs.buildDraft(entry.copy(rawText = ""), baseline).contains("\"rootNode\""))
        assertEquals("", LocalMapRepairs.buildDraft(entry.copy(rawText = ""), baselineSnapshot = null))
    }

    @Test
    fun repairLabelsUsePwaStyleFileNames() {
        val entry = entry(fileName = "Broken.json", mapName = "Broken", rawText = "{}")

        assertEquals("Broken.json", LocalMapRepairs.repairFileName(entry))
        assertEquals("Repair locally Broken.json", LocalMapRepairs.repairActionLabel(entry))
        assertEquals("Download repair draft Broken.json", LocalMapRepairs.downloadRepairDraftLabel(entry))
        assertEquals("No local map data is available to repair for \"Broken\".", LocalMapRepairs.noDraftMessage(entry))
        assertEquals(
            "Invalid JSON was found in Broken.json. Save a repaired local copy to unblock this device.",
            LocalMapRepairs.helperText(entry),
        )
    }

    @Test
    fun validateRepairJsonRejectsBlankNonObjectAndMissingRoot() {
        assertEquals(
            "Map JSON cannot be empty.",
            LocalMapRepairs.validateRepairJson(" ").exceptionOrNull()?.message,
        )
        assertEquals(
            "Map JSON must be an object.",
            LocalMapRepairs.validateRepairJson("[]").exceptionOrNull()?.message,
        )
        assertEquals(
            "Map JSON must include a root node.",
            LocalMapRepairs.validateRepairJson("{\"updatedAt\":\"2026-05-04T10:00:00Z\"}").exceptionOrNull()?.message,
        )
    }

    @Test
    fun validatesAndBuildsNormalizedRepairSnapshot() {
        val document = LocalMapRepairs.validateRepairJson(
            """
            {
              "RootNode": {
                "UniqueIdentifier": "11111111-1111-4111-8111-111111111111",
                "Name": "Fixed Root"
              }
            }
            """.trimIndent(),
        ).getOrThrow()
        val baseline = snapshot("FocusMaps/Broken.json", "Old Baseline").copy(revision = "baseline-rev")
        val repaired = LocalMapRepairs.buildRepairSnapshot(
            entry = entry(fileName = "Broken.json", mapName = "Broken", revision = "broken-rev", rawText = ""),
            document = document,
            baselineSnapshot = baseline,
            loadedAtMillis = 42,
        )

        assertEquals("FocusMaps/Broken.json", repaired.filePath)
        assertEquals("Broken.json", repaired.fileName)
        assertEquals("Broken", repaired.mapName)
        assertEquals("broken-rev", repaired.revision)
        assertEquals(42, repaired.loadedAtMillis)
        assertEquals("Fixed Root", repaired.document.rootNode.name)
    }

    private fun entry(
        filePath: String = "FocusMaps/Broken.json",
        fileName: String = "Broken.json",
        mapName: String = "",
        revision: String = "rev",
        rawText: String,
    ): UnreadableMapEntry =
        UnreadableMapEntry(
            filePath = filePath,
            fileName = fileName,
            mapName = mapName,
            revision = revision,
            reason = UnreadableMapReason.InvalidJson,
            message = "Broken",
            rawText = rawText,
        )

    private fun snapshot(filePath: String, name: String): MapSnapshot =
        MapSnapshot(
            filePath = filePath,
            fileName = filePath.substringAfterLast('/'),
            mapName = name,
            document = MindMapDocument(
                rootNode = Node(
                    uniqueIdentifier = "22222222-2222-4222-8222-222222222222",
                    name = name,
                ),
            ),
            revision = "rev-$name",
            loadedAtMillis = 0,
        )
}
