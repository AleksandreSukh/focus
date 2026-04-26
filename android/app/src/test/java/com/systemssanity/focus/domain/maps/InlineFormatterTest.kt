package com.systemssanity.focus.domain.maps

import kotlin.test.Test
import kotlin.test.assertEquals

class InlineFormatterTest {
    @Test
    fun parsesColorRunsAndPlainText() {
        val runs = InlineFormatter.parseRuns("[red]Hot[!] plain")

        assertEquals(listOf(InlineRun("Hot", "red"), InlineRun(" plain", null)), runs)
        assertEquals("Hot plain", InlineFormatter.plainText("[red]Hot[!] plain"))
    }

    @Test
    fun tokenizesHttpLinksAndTrimsTrailingPeriod() {
        val tokens = InlineFormatter.tokenize("Open https://example.com/path.")

        assertEquals("https://example.com/path", tokens.first { it.url != null }.url)
    }
}
