package com.systemssanity.focus.domain.model

import java.time.Instant
import java.time.format.DateTimeFormatter

object ClockProvider {
    fun nowIsoSeconds(): String =
        DateTimeFormatter.ISO_INSTANT.format(Instant.now().truncatedTo(java.time.temporal.ChronoUnit.SECONDS))
}

object DeviceIdentity {
    const val defaultDeviceName = "focus-android"
}
