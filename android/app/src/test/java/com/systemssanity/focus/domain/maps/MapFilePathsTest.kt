package com.systemssanity.focus.domain.maps

import kotlin.test.Test
import kotlin.test.assertEquals

class MapFilePathsTest {
    @Test
    fun buildsMapFilePathInsideConfiguredDirectory() {
        assertEquals("FocusMaps/My Map.json", MapFilePaths.build("/FocusMaps/", "My Map"))
        assertEquals("My Map.json", MapFilePaths.build("/", "My Map"))
    }

    @Test
    fun renamePreservesExistingDirectory() {
        assertEquals("FocusMaps/New Map.json", MapFilePaths.rename("FocusMaps/Old Map.json", "New Map"))
        assertEquals("New Map.json", MapFilePaths.rename("Old Map.json", "New Map"))
    }

    @Test
    fun sanitizesInvalidFileNameCharactersAndJsonExtension() {
        assertEquals("Bad-Name.json", MapFilePaths.sanitizeFileName("Bad/Name"))
        assertEquals("Plan.json", MapFilePaths.sanitizeFileName("Plan.json"))
        assertEquals("Untitled.json", MapFilePaths.sanitizeFileName("..."))
    }

    @Test
    fun unchangedRenameCanBeDetectedByComparingPaths() {
        val oldFilePath = "FocusMaps/Plan.json"
        assertEquals(oldFilePath, MapFilePaths.rename(oldFilePath, "Plan.json"))
    }
}
