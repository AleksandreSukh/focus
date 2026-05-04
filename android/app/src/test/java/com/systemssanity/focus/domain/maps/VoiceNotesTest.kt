package com.systemssanity.focus.domain.maps

import java.time.Instant
import java.time.ZoneId
import kotlin.test.Test
import kotlin.test.assertEquals

class VoiceNotesTest {
    @Test
    fun exposesAndroidVoiceAttachmentDefaults() {
        assertEquals(5L * 60L * 1000L, VoiceNotes.MaxDurationMillis)
        assertEquals("audio/mp4", VoiceNotes.MediaType)
        assertEquals(".m4a", VoiceNotes.Extension)
    }

    @Test
    fun formatsVoiceNoteNamesLikePwa() {
        val instant = Instant.parse("2026-04-27T10:05:30Z")
        val zone = ZoneId.of("UTC")

        assertEquals("Voice note 2026-04-27 10:05", VoiceNotes.displayName(instant, zone))
        assertEquals("Voice note 2026-04-27 10-05.m4a", VoiceNotes.fileName(instant, zone))
    }

    @Test
    fun formatsElapsedRecordingTime() {
        assertEquals("0:00", VoiceNotes.formatElapsed(0))
        assertEquals("0:00", VoiceNotes.formatElapsed(-100))
        assertEquals("1:05", VoiceNotes.formatElapsed(65_100))
        assertEquals("5:00", VoiceNotes.formatElapsed(VoiceNotes.MaxDurationMillis))
    }
}
