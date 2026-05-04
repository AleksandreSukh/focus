package com.systemssanity.focus.ui

import androidx.compose.ui.graphics.Color
import com.systemssanity.focus.domain.maps.TaskFilter
import com.systemssanity.focus.domain.model.TaskState
import kotlin.test.Test
import kotlin.test.assertEquals

class FocusThemeTest {
    @Test
    fun pwaPaletteMatchesExpectedLightAndDarkAnchors() {
        val light = focusPalette(isDark = false)
        val dark = focusPalette(isDark = true)

        assertEquals(Color(0xFFFAFAFA), light.pageBackground)
        assertEquals(Color(0xFF0891B2), light.accent)
        assertEquals(Color(0xFFE4E4E7), light.border)

        assertEquals(Color(0xFF0A0A0A), dark.pageBackground)
        assertEquals(Color(0xFF06B6D4), dark.accent)
        assertEquals(Color(0xFF27272A), dark.border)
    }

    @Test
    fun taskAndFilterColorsUsePwaTaskTones() {
        val palette = focusPalette(isDark = true)

        assertEquals(Color(0xFF60A5FA), focusTaskColor(TaskState.Todo, palette))
        assertEquals(Color(0xFFFBBF24), focusTaskColor(TaskState.Doing, palette))
        assertEquals(Color(0xFF34D399), focusTaskColor(TaskState.Done, palette))
        assertEquals(Color(0xFF60A5FA), focusFilterColor(TaskFilter.Open, palette))
        assertEquals(palette.muted, focusFilterColor(TaskFilter.All, palette))
    }

    @Test
    fun taskLabelsAreStableForAccessibleControls() {
        assertEquals("Clear", focusTaskLabel(TaskState.None))
        assertEquals("Todo", focusTaskLabel(TaskState.Todo))
        assertEquals("Doing", focusTaskLabel(TaskState.Doing))
        assertEquals("Done", focusTaskLabel(TaskState.Done))
    }
}
