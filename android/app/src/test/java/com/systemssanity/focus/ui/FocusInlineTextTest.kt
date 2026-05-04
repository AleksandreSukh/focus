package com.systemssanity.focus.ui

import androidx.compose.ui.graphics.Color
import androidx.compose.ui.text.style.TextDecoration
import kotlin.test.Test
import kotlin.test.assertEquals
import kotlin.test.assertTrue

class FocusInlineTextTest {
    @Test
    fun mapsPwaInlineColorsForLightAndDarkThemes() {
        assertEquals(Color(0xFFFF0000), inlineColor("red", focusPalette(isDark = false)))
        assertEquals(Color(0xFFFCA5A5), inlineColor("red", focusPalette(isDark = true)))
        assertEquals(Color(0xFF000080), inlineColor("darkblue", focusPalette(isDark = false)))
        assertEquals(Color(0xFF60A5FA), inlineColor("darkblue", focusPalette(isDark = true)))
    }

    @Test
    fun buildsColoredTextAndLinkAnnotations() {
        val annotated = buildInlineAnnotatedString(
            raw = "[red]Hot https://example.com[!]",
            baseColor = Color.Black,
            palette = focusPalette(isDark = false),
        )

        assertEquals("Hot https://example.com", annotated.text)
        val link = annotated.getStringAnnotations(InlineUrlAnnotationTag, 0, annotated.text.length).single()
        assertEquals("https://example.com", link.item)
        assertEquals(4, link.start)
        assertEquals(23, link.end)
        assertTrue(annotated.spanStyles.any { span ->
            span.start == 0 && span.end == 4 && span.item.color == Color(0xFFFF0000)
        })
        assertTrue(annotated.spanStyles.any { span ->
            span.start == 4 &&
                span.end == 23 &&
                span.item.color == Color(0xFFFF0000) &&
                span.item.textDecoration == TextDecoration.Underline
        })
    }

    @Test
    fun darkThemeLinksUseAccentColorLikePwa() {
        val palette = focusPalette(isDark = true)
        val annotated = buildInlineAnnotatedString(
            raw = "[red]https://example.com[!]",
            baseColor = palette.text,
            palette = palette,
        )

        assertTrue(annotated.spanStyles.any { span ->
            span.start == 0 &&
                span.end == annotated.text.length &&
                span.item.color == palette.accentStrong &&
                span.item.textDecoration == TextDecoration.Underline
        })
    }

    @Test
    fun buildsInlinePathWithoutBleedingMarkupAcrossSegments() {
        val palette = focusPalette(isDark = false)
        val annotated = buildInlinePathAnnotatedString(
            pathSegments = listOf("[red]Root", "Child[!] https://example.com"),
            baseColor = palette.accentStrong,
            separatorColor = palette.muted,
            palette = palette,
        )

        assertEquals("Root > Child https://example.com", annotated.text)
        assertEquals("https://example.com", annotated.getStringAnnotations(InlineUrlAnnotationTag, 0, annotated.text.length).single().item)
        assertTrue(annotated.spanStyles.any { span ->
            annotated.text.substring(span.start, span.end) == "Root" && span.item.color == Color(0xFFFF0000)
        })
        assertTrue(annotated.spanStyles.any { span ->
            annotated.text.substring(span.start, span.end) == " > " && span.item.color == palette.muted
        })
        assertTrue(annotated.spanStyles.none { span ->
            annotated.text.substring(span.start, span.end).startsWith("Child") && span.item.color == Color(0xFFFF0000)
        })
    }

    @Test
    fun plainInlineTextStripsMarkupForLabels() {
        assertEquals("Hot plain", plainInlineDisplayText("[red]Hot[!] plain"))
        assertEquals("Root > Child", plainInlinePathText(listOf("[red]Root[!]", "Child")))
        assertEquals("Untitled", plainInlineDisplayText("[red][!]"))
    }
}
