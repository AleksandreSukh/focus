package com.systemssanity.focus.ui

import android.app.Activity
import androidx.compose.foundation.BorderStroke
import androidx.compose.foundation.background
import androidx.compose.foundation.border
import androidx.compose.foundation.clickable
import androidx.compose.foundation.layout.Box
import androidx.compose.foundation.layout.Row
import androidx.compose.foundation.layout.heightIn
import androidx.compose.foundation.layout.padding
import androidx.compose.foundation.layout.size
import androidx.compose.foundation.shape.CircleShape
import androidx.compose.foundation.shape.RoundedCornerShape
import androidx.compose.material3.FloatingActionButton
import androidx.compose.material3.Icon
import androidx.compose.material3.IconButton
import androidx.compose.material3.MaterialTheme
import androidx.compose.material3.Shapes
import androidx.compose.material3.Surface
import androidx.compose.material3.darkColorScheme
import androidx.compose.material3.lightColorScheme
import androidx.compose.runtime.Composable
import androidx.compose.runtime.CompositionLocalProvider
import androidx.compose.runtime.Immutable
import androidx.compose.runtime.SideEffect
import androidx.compose.runtime.staticCompositionLocalOf
import androidx.compose.ui.Alignment
import androidx.compose.ui.Modifier
import androidx.compose.ui.draw.clip
import androidx.compose.ui.graphics.Color
import androidx.compose.ui.graphics.toArgb
import androidx.compose.ui.graphics.vector.ImageVector
import androidx.compose.ui.platform.LocalView
import androidx.compose.ui.text.font.FontWeight
import androidx.compose.ui.unit.dp
import androidx.core.view.WindowCompat
import com.systemssanity.focus.data.local.ThemePreference
import com.systemssanity.focus.domain.maps.TaskFilter
import com.systemssanity.focus.domain.model.TaskState

@Immutable
data class FocusPalette(
    val isDark: Boolean,
    val pageBackground: Color,
    val panelBackground: Color,
    val panelMuted: Color,
    val inputBackground: Color,
    val text: Color,
    val muted: Color,
    val accent: Color,
    val accentStrong: Color,
    val border: Color,
    val danger: Color,
    val warning: Color,
    val success: Color,
    val taskNoneBorder: Color,
    val taskTodo: Color,
    val taskDoing: Color,
    val taskDone: Color,
) {
    val accentSoft: Color get() = accent.copy(alpha = if (isDark) 0.12f else 0.08f)
    val accentBorder: Color get() = accent.copy(alpha = if (isDark) 0.25f else 0.20f)
    val accentBorderStrong: Color get() = accent.copy(alpha = if (isDark) 0.50f else 0.45f)
    val surfaceHover: Color get() = if (isDark) Color(0xFF1C1C1F) else Color(0xFFF4F4F5)
}

val LocalFocusPalette = staticCompositionLocalOf { focusPalette(isDark = false) }

fun focusPalette(isDark: Boolean): FocusPalette = if (isDark) {
    FocusPalette(
        isDark = true,
        pageBackground = Color(0xFF0A0A0A),
        panelBackground = Color(0xFF111113),
        panelMuted = Color(0xFF18181B),
        inputBackground = Color(0xFF18181B),
        text = Color(0xFFEDEDEF),
        muted = Color(0xFFA1A1AA),
        accent = Color(0xFF06B6D4),
        accentStrong = Color(0xFF22D3EE),
        border = Color(0xFF27272A),
        danger = Color(0xFFF87171),
        warning = Color(0xFFFBBF24),
        success = Color(0xFF34D399),
        taskNoneBorder = Color(0xFF3F3F46),
        taskTodo = Color(0xFF60A5FA),
        taskDoing = Color(0xFFFBBF24),
        taskDone = Color(0xFF34D399),
    )
} else {
    FocusPalette(
        isDark = false,
        pageBackground = Color(0xFFFAFAFA),
        panelBackground = Color(0xFFFFFFFF),
        panelMuted = Color(0xFFF4F4F5),
        inputBackground = Color(0xFFFFFFFF),
        text = Color(0xFF18181B),
        muted = Color(0xFF71717A),
        accent = Color(0xFF0891B2),
        accentStrong = Color(0xFF0E7490),
        border = Color(0xFFE4E4E7),
        danger = Color(0xFFDC2626),
        warning = Color(0xFFD97706),
        success = Color(0xFF059669),
        taskNoneBorder = Color(0xFFD4D4D8),
        taskTodo = Color(0xFF2563EB),
        taskDoing = Color(0xFFEAB308),
        taskDone = Color(0xFF16A34A),
    )
}

fun focusTaskColor(taskState: TaskState, palette: FocusPalette): Color = when (taskState) {
    TaskState.None -> palette.taskNoneBorder
    TaskState.Todo -> palette.taskTodo
    TaskState.Doing -> palette.taskDoing
    TaskState.Done -> palette.taskDone
}

fun focusFilterColor(filter: TaskFilter, palette: FocusPalette): Color = when (filter) {
    TaskFilter.Open,
    TaskFilter.Todo -> palette.taskTodo
    TaskFilter.Doing -> palette.taskDoing
    TaskFilter.Done -> palette.taskDone
    TaskFilter.All -> palette.muted
}

fun focusTaskLabel(taskState: TaskState): String = when (taskState) {
    TaskState.None -> "Clear"
    TaskState.Todo -> "Todo"
    TaskState.Doing -> "Doing"
    TaskState.Done -> "Done"
}

@Composable
@Suppress("DEPRECATION")
fun FocusTheme(
    themePreference: ThemePreference,
    content: @Composable () -> Unit,
) {
    val palette = focusPalette(isDark = themePreference == ThemePreference.Dark)
    val colorScheme = if (palette.isDark) {
        darkColorScheme(
            primary = palette.accent,
            onPrimary = Color.White,
            secondary = palette.accentStrong,
            background = palette.pageBackground,
            onBackground = palette.text,
            surface = palette.panelBackground,
            onSurface = palette.text,
            surfaceVariant = palette.panelMuted,
            onSurfaceVariant = palette.muted,
            outline = palette.border,
            error = palette.danger,
        )
    } else {
        lightColorScheme(
            primary = palette.accent,
            onPrimary = Color.White,
            secondary = palette.accentStrong,
            background = palette.pageBackground,
            onBackground = palette.text,
            surface = palette.panelBackground,
            onSurface = palette.text,
            surfaceVariant = palette.panelMuted,
            onSurfaceVariant = palette.muted,
            outline = palette.border,
            error = palette.danger,
        )
    }

    val view = LocalView.current
    if (!view.isInEditMode) {
        SideEffect {
            val window = (view.context as? Activity)?.window ?: return@SideEffect
            window.statusBarColor = palette.pageBackground.toArgb()
            window.navigationBarColor = palette.pageBackground.toArgb()
            WindowCompat.getInsetsController(window, view).apply {
                isAppearanceLightStatusBars = !palette.isDark
                isAppearanceLightNavigationBars = !palette.isDark
            }
        }
    }

    CompositionLocalProvider(LocalFocusPalette provides palette) {
        MaterialTheme(
            colorScheme = colorScheme,
            shapes = Shapes(
                extraSmall = RoundedCornerShape(8.dp),
                small = RoundedCornerShape(10.dp),
                medium = RoundedCornerShape(14.dp),
                large = RoundedCornerShape(16.dp),
                extraLarge = RoundedCornerShape(20.dp),
            ),
            content = content,
        )
    }
}

@Composable
fun FocusCard(
    modifier: Modifier = Modifier,
    onClick: (() -> Unit)? = null,
    content: @Composable () -> Unit,
) {
    val palette = LocalFocusPalette.current
    val shape = RoundedCornerShape(16.dp)
    val clickableModifier = if (onClick == null) {
        Modifier
    } else {
        Modifier
            .clip(shape)
            .clickable(onClick = onClick)
    }
    Surface(
        modifier = modifier.then(clickableModifier),
        shape = shape,
        color = palette.panelBackground,
        contentColor = palette.text,
        border = BorderStroke(1.dp, palette.border),
        shadowElevation = if (onClick == null) 1.dp else 2.dp,
        tonalElevation = 0.dp,
        content = content,
    )
}

@Composable
fun FocusIconButton(
    imageVector: ImageVector,
    contentDescription: String,
    onClick: () -> Unit,
    modifier: Modifier = Modifier,
    enabled: Boolean = true,
    selected: Boolean = false,
    destructive: Boolean = false,
) {
    val palette = LocalFocusPalette.current
    val contentColor = when {
        !enabled -> palette.muted.copy(alpha = 0.45f)
        destructive -> palette.danger
        selected -> palette.accentStrong
        else -> palette.accentStrong
    }
    val borderColor = when {
        destructive -> palette.danger.copy(alpha = 0.32f)
        selected -> palette.accentBorderStrong
        else -> palette.accentBorder
    }
    IconButton(
        onClick = onClick,
        enabled = enabled,
        modifier = modifier
            .size(42.dp)
            .clip(CircleShape)
            .background(if (selected) palette.accentSoft else Color.Transparent)
            .border(BorderStroke(1.dp, borderColor), CircleShape),
    ) {
        Icon(
            imageVector = imageVector,
            contentDescription = contentDescription,
            tint = contentColor,
            modifier = Modifier.size(20.dp),
        )
    }
}

@Composable
fun FocusPill(
    label: String,
    modifier: Modifier = Modifier,
    selected: Boolean = false,
    toneColor: Color = LocalFocusPalette.current.accent,
    leadingDot: Boolean = false,
    onClick: (() -> Unit)? = null,
) {
    val palette = LocalFocusPalette.current
    val shape = CircleShape
    val clickModifier = if (onClick == null) Modifier else Modifier.clickable(onClick = onClick)
    Row(
        modifier = modifier
            .clip(shape)
            .background(if (selected) toneColor.copy(alpha = if (palette.isDark) 0.18f else 0.10f) else palette.panelBackground)
            .border(
                BorderStroke(1.dp, if (selected) toneColor else palette.border),
                shape,
            )
            .then(clickModifier)
            .heightIn(min = 32.dp)
            .padding(horizontal = 12.dp, vertical = 6.dp),
        verticalAlignment = Alignment.CenterVertically,
    ) {
        if (leadingDot) {
            Box(
                modifier = Modifier
                    .padding(end = 7.dp)
                    .size(8.dp)
                    .clip(CircleShape)
                    .background(toneColor),
            )
        }
        androidx.compose.material3.Text(
            text = label,
            color = if (selected) toneColor else palette.text,
            style = MaterialTheme.typography.labelMedium,
            fontWeight = FontWeight.SemiBold,
        )
    }
}

@Composable
fun TaskDot(
    taskState: TaskState,
    modifier: Modifier = Modifier,
    selected: Boolean = false,
) {
    val palette = LocalFocusPalette.current
    val color = focusTaskColor(taskState, palette)
    Box(
        modifier = modifier
            .size(if (selected) 14.dp else 12.dp)
            .clip(CircleShape)
            .background(if (taskState == TaskState.None) Color.Transparent else color)
            .border(
                BorderStroke(
                    width = if (taskState == TaskState.None || selected) 2.dp else 0.dp,
                    color = if (taskState == TaskState.None) color else color.copy(alpha = if (selected) 0.85f else 0f),
                ),
                CircleShape,
            ),
    )
}

@Composable
fun FocusFab(
    imageVector: ImageVector,
    contentDescription: String,
    onClick: () -> Unit,
    modifier: Modifier = Modifier,
) {
    val palette = LocalFocusPalette.current
    FloatingActionButton(
        onClick = onClick,
        modifier = modifier,
        containerColor = palette.accent,
        contentColor = Color.White,
        shape = CircleShape,
    ) {
        Icon(
            imageVector = imageVector,
            contentDescription = contentDescription,
        )
    }
}
