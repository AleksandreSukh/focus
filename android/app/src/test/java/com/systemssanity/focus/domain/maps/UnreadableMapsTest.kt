package com.systemssanity.focus.domain.maps

import kotlin.test.Test
import kotlin.test.assertContentEquals
import kotlin.test.assertEquals

class UnreadableMapsTest {
    @Test
    fun describesUnreadableReasons() {
        assertEquals(
            "Automatic merge conflict resolution could not finish safely",
            UnreadableMaps.reasonLabel(UnreadableMapReason.AutoResolveFailed),
        )
        assertEquals(
            "Unresolved merge conflict markers were found",
            UnreadableMaps.reasonLabel(UnreadableMapReason.MergeConflict),
        )
        assertEquals(
            "Invalid JSON was found",
            UnreadableMaps.reasonLabel(UnreadableMapReason.InvalidJson),
        )
        assertEquals(
            "The map file could not be parsed",
            UnreadableMaps.reasonLabel(UnreadableMapReason.Unknown),
        )
    }

    @Test
    fun buildsPwaStyleUnreadableMessages() {
        assertEquals(
            "This map has merge conflicts that couldn't be auto-resolved. Repair locally or reset it from GitHub.",
            UnreadableMaps.message("Broken.json", UnreadableMapReason.AutoResolveFailed),
        )
        assertEquals(
            "Map \"Broken.json\" contains unresolved Git merge markers and cannot be loaded.",
            UnreadableMaps.message("Broken.json", UnreadableMapReason.MergeConflict),
        )
        assertEquals(
            "Map \"Broken.json\" is not valid JSON and cannot be loaded.",
            UnreadableMaps.message("Broken.json", UnreadableMapReason.InvalidJson),
        )
        assertEquals(
            "Map \"Broken.json\" could not be parsed and cannot be loaded.",
            UnreadableMaps.message("Broken.json", UnreadableMapReason.Unknown),
        )
    }

    @Test
    fun derivesFallbackNamesAndRetryLabels() {
        val entry = UnreadableMapEntry(
            filePath = "FocusMaps/Broken.json",
            fileName = "",
            mapName = "",
            revision = "rev",
            reason = UnreadableMapReason.InvalidJson,
            message = "Broken",
            rawText = "{}",
        )

        assertEquals("Broken.json", UnreadableMaps.fileName("FocusMaps/Broken.json"))
        assertEquals("Broken", UnreadableMaps.mapName("FocusMaps/Broken.json"))
        assertEquals("Retry FocusMaps/Broken.json", UnreadableMaps.retryLabel(entry))
    }

    @Test
    fun buildsRawUnreadableMapExportDetails() {
        val named = entry(fileName = "Broken.json", mapName = "Broken", filePath = "FocusMaps/Broken.json", rawText = "{\"rootNode\":")
        val mapNameOnly = entry(fileName = "", mapName = "Needs Repair", filePath = "", rawText = "")
        val pathOnly = entry(fileName = "", mapName = "", filePath = "FocusMaps/bad:name.json", rawText = "abc")

        assertEquals("Broken.json", UnreadableMaps.rawFileName(named))
        assertEquals("Needs Repair.json", UnreadableMaps.rawFileName(mapNameOnly))
        assertEquals("bad_name.json", UnreadableMaps.rawFileName(pathOnly))
        assertContentEquals("{\"rootNode\":".toByteArray(Charsets.UTF_8), UnreadableMaps.rawBytes(named))
        assertEquals("View raw file Broken.json", UnreadableMaps.viewRawLabel(named))
        assertEquals("Download raw file Broken.json", UnreadableMaps.downloadRawLabel(named))

        val attachment = UnreadableMaps.rawAttachment(named)
        assertEquals(UnreadableMaps.RawMapAttachmentId, attachment.id)
        assertEquals("Broken.json", attachment.displayName)
        assertEquals("Broken.json", attachment.relativePath)
        assertEquals(UnreadableMaps.RawMapMediaType, attachment.mediaType)
    }

    private fun entry(
        filePath: String,
        fileName: String,
        mapName: String,
        rawText: String,
    ): UnreadableMapEntry =
        UnreadableMapEntry(
            filePath = filePath,
            fileName = fileName,
            mapName = mapName,
            revision = "rev",
            reason = UnreadableMapReason.InvalidJson,
            message = "Broken",
            rawText = rawText,
        )
}
