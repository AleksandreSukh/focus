package com.systemssanity.focus.domain.maps

import java.time.Instant
import java.time.ZoneId
import java.time.format.DateTimeFormatter
import kotlin.math.max

object VoiceNotes {
    const val MaxDurationMillis: Long = 5L * 60L * 1000L
    const val MediaType: String = "audio/mp4"
    const val Extension: String = ".m4a"

    private val DisplayFormatter: DateTimeFormatter = DateTimeFormatter.ofPattern("yyyy-MM-dd HH:mm")
    private val FileFormatter: DateTimeFormatter = DateTimeFormatter.ofPattern("yyyy-MM-dd HH-mm")

    fun displayName(instant: Instant = Instant.now(), zoneId: ZoneId = ZoneId.systemDefault()): String =
        "Voice note ${DisplayFormatter.format(instant.atZone(zoneId))}"

    fun fileName(instant: Instant = Instant.now(), zoneId: ZoneId = ZoneId.systemDefault()): String =
        "Voice note ${FileFormatter.format(instant.atZone(zoneId))}$Extension"

    fun formatElapsed(elapsedMillis: Long): String {
        val totalSeconds = max(0L, elapsedMillis) / 1000L
        val minutes = totalSeconds / 60L
        val seconds = totalSeconds % 60L
        return "$minutes:${seconds.toString().padStart(2, '0')}"
    }
}
