package com.systemssanity.focus.ui

import androidx.compose.foundation.text.ClickableText
import androidx.compose.material3.LocalTextStyle
import androidx.compose.material3.Text
import androidx.compose.runtime.Composable
import androidx.compose.runtime.remember
import androidx.compose.ui.Modifier
import androidx.compose.ui.graphics.Color
import androidx.compose.ui.platform.LocalUriHandler
import androidx.compose.ui.text.AnnotatedString
import androidx.compose.ui.text.SpanStyle
import androidx.compose.ui.text.TextStyle
import androidx.compose.ui.text.buildAnnotatedString
import androidx.compose.ui.text.style.TextDecoration
import androidx.compose.ui.text.style.TextOverflow
import com.systemssanity.focus.domain.maps.InlineFormatter
import com.systemssanity.focus.domain.maps.InlineToken
import com.systemssanity.focus.domain.maps.MapQueries

internal const val InlineUrlAnnotationTag = "focus-url"

@Composable
internal fun FocusInlineText(
    text: String,
    modifier: Modifier = Modifier,
    style: TextStyle = LocalTextStyle.current,
    color: Color = LocalFocusPalette.current.text,
    maxLines: Int = Int.MAX_VALUE,
    overflow: TextOverflow = TextOverflow.Clip,
    onPlainClick: (() -> Unit)? = null,
) {
    val palette = LocalFocusPalette.current
    val uriHandler = LocalUriHandler.current
    val annotated = remember(text, color, palette.isDark) {
        buildInlineAnnotatedString(text, baseColor = color, palette = palette)
    }
    val textStyle = style.copy(color = color)
    if (annotated.hasUrlAnnotations() || onPlainClick != null) {
        @Suppress("DEPRECATION")
        ClickableText(
            text = annotated,
            modifier = modifier,
            style = textStyle,
            maxLines = maxLines,
            overflow = overflow,
            onClick = { offset ->
                val url = annotated
                    .getStringAnnotations(InlineUrlAnnotationTag, offset, offset)
                    .firstOrNull()
                    ?.item
                if (url == null) {
                    onPlainClick?.invoke()
                } else {
                    uriHandler.openUri(url)
                }
            },
        )
    } else {
        Text(
            text = annotated,
            modifier = modifier,
            style = textStyle,
            maxLines = maxLines,
            overflow = overflow,
        )
    }
}

@Composable
internal fun FocusInlinePath(
    pathSegments: List<String>,
    modifier: Modifier = Modifier,
    style: TextStyle = LocalTextStyle.current,
    color: Color = LocalFocusPalette.current.accentStrong,
    maxLines: Int = Int.MAX_VALUE,
    overflow: TextOverflow = TextOverflow.Clip,
    onPlainClick: (() -> Unit)? = null,
) {
    val palette = LocalFocusPalette.current
    val uriHandler = LocalUriHandler.current
    val annotated = remember(pathSegments, color, palette.isDark) {
        buildInlinePathAnnotatedString(
            pathSegments = pathSegments,
            baseColor = color,
            separatorColor = palette.muted,
            palette = palette,
        )
    }
    val textStyle = style.copy(color = color)
    if (annotated.hasUrlAnnotations() || onPlainClick != null) {
        @Suppress("DEPRECATION")
        ClickableText(
            text = annotated,
            modifier = modifier,
            style = textStyle,
            maxLines = maxLines,
            overflow = overflow,
            onClick = { offset ->
                val url = annotated
                    .getStringAnnotations(InlineUrlAnnotationTag, offset, offset)
                    .firstOrNull()
                    ?.item
                if (url == null) {
                    onPlainClick?.invoke()
                } else {
                    uriHandler.openUri(url)
                }
            },
        )
    } else {
        Text(
            text = annotated,
            modifier = modifier,
            style = textStyle,
            maxLines = maxLines,
            overflow = overflow,
        )
    }
}

internal fun buildInlineAnnotatedString(
    raw: String,
    baseColor: Color,
    palette: FocusPalette,
): AnnotatedString =
    buildAnnotatedString {
        appendInlineTokens(
            tokens = InlineFormatter.tokenize(MapQueries.normalizeNodeDisplayText(raw)),
            baseColor = baseColor,
            palette = palette,
        )
    }

internal fun buildInlinePathAnnotatedString(
    pathSegments: List<String>,
    baseColor: Color,
    separatorColor: Color,
    palette: FocusPalette,
): AnnotatedString =
    buildAnnotatedString {
        val segments = pathSegments.ifEmpty { listOf("") }
        segments.forEachIndexed { index, segment ->
            if (index > 0) {
                val separatorStart = length
                append(" > ")
                addStyle(SpanStyle(color = separatorColor), separatorStart, length)
            }
            appendInlineTokens(
                tokens = InlineFormatter.tokenize(MapQueries.normalizeNodeDisplayText(segment)),
                baseColor = baseColor,
                palette = palette,
            )
        }
    }

internal fun plainInlineDisplayText(raw: String): String {
    val plain = InlineFormatter.plainText(MapQueries.normalizeNodeDisplayText(raw)).trim()
    return plain.ifBlank { "Untitled" }
}

internal fun plainInlinePathText(pathSegments: List<String>): String =
    pathSegments.ifEmpty { listOf("") }
        .joinToString(" > ") { plainInlineDisplayText(it) }

internal fun inlineColor(colorName: String?, palette: FocusPalette): Color? =
    when (colorName?.lowercase()) {
        "black" -> if (palette.isDark) Color(0xFFEDEDEF) else Color(0xFF000000)
        "darkblue" -> if (palette.isDark) Color(0xFF60A5FA) else Color(0xFF000080)
        "darkgreen" -> if (palette.isDark) Color(0xFF4ADE80) else Color(0xFF008000)
        "darkcyan" -> if (palette.isDark) Color(0xFF22D3EE) else Color(0xFF008080)
        "darkred" -> if (palette.isDark) Color(0xFFF87171) else Color(0xFF800000)
        "darkmagenta" -> if (palette.isDark) Color(0xFFE879F9) else Color(0xFF800080)
        "darkyellow" -> if (palette.isDark) Color(0xFFFACC15) else Color(0xFF808000)
        "gray" -> if (palette.isDark) Color(0xFFD1D5DB) else Color(0xFFC0C0C0)
        "darkgray" -> if (palette.isDark) Color(0xFF9CA3AF) else Color(0xFF808080)
        "blue" -> if (palette.isDark) Color(0xFF93C5FD) else Color(0xFF0000FF)
        "green" -> if (palette.isDark) Color(0xFF86EFAC) else Color(0xFF008000)
        "cyan" -> if (palette.isDark) Color(0xFF67E8F9) else Color(0xFF00FFFF)
        "red" -> if (palette.isDark) Color(0xFFFCA5A5) else Color(0xFFFF0000)
        "magenta" -> if (palette.isDark) Color(0xFFF0ABFC) else Color(0xFFFF00FF)
        "yellow" -> if (palette.isDark) Color(0xFFFDE047) else Color(0xFFFFD700)
        "white" -> Color(0xFFFFFFFF)
        else -> null
    }

private fun AnnotatedString.Builder.appendInlineTokens(
    tokens: List<InlineToken>,
    baseColor: Color,
    palette: FocusPalette,
) {
    tokens.forEach { token ->
        val start = length
        append(token.text)
        val end = length
        if (start == end) return@forEach
        val runColor = inlineColor(token.colorName, palette) ?: baseColor
        val displayColor = if (token.url != null && palette.isDark) palette.accentStrong else runColor
        addStyle(
            SpanStyle(
                color = displayColor,
                textDecoration = if (token.url == null) null else TextDecoration.Underline,
            ),
            start,
            end,
        )
        token.url?.let { addStringAnnotation(InlineUrlAnnotationTag, it, start, end) }
    }
}

private fun AnnotatedString.hasUrlAnnotations(): Boolean =
    getStringAnnotations(InlineUrlAnnotationTag, 0, text.length).isNotEmpty()
