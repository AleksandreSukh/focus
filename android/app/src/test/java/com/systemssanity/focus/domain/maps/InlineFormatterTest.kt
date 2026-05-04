package com.systemssanity.focus.domain.maps

import kotlin.test.Test
import kotlin.test.assertEquals
import kotlin.test.assertNull

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

    @Test
    fun tokenizesLinksInsideColorRuns() {
        val link = InlineFormatter.tokenize("[red]Open https://example.com[!]")
            .first { it.url != null }

        assertEquals("https://example.com", link.text)
        assertEquals("https://example.com", link.url)
        assertEquals("red", link.colorName)
    }

    @Test
    fun leavesUnsupportedUrlsAsPlainText() {
        val tokens = InlineFormatter.tokenize("Open https://.")

        assertNull(tokens.firstOrNull { it.url != null })
        assertEquals("Open https://.", tokens.joinToString(separator = "") { it.text })
    }

    @Test
    fun trimsUnmatchedClosingDelimiterAfterUrl() {
        val tokens = InlineFormatter.tokenize("Open (https://example.com/path)")

        assertEquals("https://example.com/path", tokens.first { it.url != null }.url)
        assertEquals(")", tokens.last().text)
    }

    @Test
    fun keepsBalancedUrlDelimiters() {
        val tokens = InlineFormatter.tokenize("Open https://example.com/a(b)")

        assertEquals("https://example.com/a(b)", tokens.first { it.url != null }.url)
    }
}
