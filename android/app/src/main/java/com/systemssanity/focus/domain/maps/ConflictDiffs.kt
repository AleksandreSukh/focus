package com.systemssanity.focus.domain.maps

import com.systemssanity.focus.domain.model.MindMapDocument
import com.systemssanity.focus.domain.model.MindMapJson

sealed interface ConflictDiffResult {
    data object TooLarge : ConflictDiffResult
    data object NoChanges : ConflictDiffResult
    data class Lines(val lines: List<ConflictDiffLine>) : ConflictDiffResult
}

sealed interface ConflictDiffLine {
    data class Text(val type: ConflictDiffLineType, val text: String) : ConflictDiffLine
    data class Ellipsis(val skipped: Int) : ConflictDiffLine
}

enum class ConflictDiffLineType {
    Context,
    Add,
    Remove,
}

object ConflictDiffs {
    private const val MaxLines = 500

    fun build(remoteDocument: MindMapDocument, localDocument: MindMapDocument): ConflictDiffResult =
        build(
            remoteText = MindMapJson.serialize(remoteDocument),
            localText = MindMapJson.serialize(localDocument),
        )

    fun build(remoteText: String, localText: String): ConflictDiffResult {
        val remoteLines = remoteText.split('\n')
        val localLines = localText.split('\n')
        val diff = computeLineDiff(remoteLines, localLines) ?: return ConflictDiffResult.TooLarge
        if (diff.none { it.type != ConflictDiffLineType.Context }) return ConflictDiffResult.NoChanges
        return ConflictDiffResult.Lines(collapseContext(diff, contextLines = 3))
    }

    internal fun computeLineDiff(
        leftLines: List<String>,
        rightLines: List<String>,
    ): List<ConflictDiffLine.Text>? {
        if (leftLines.size > MaxLines || rightLines.size > MaxLines) return null

        val m = leftLines.size
        val n = rightLines.size
        val dp = Array(m + 1) { IntArray(n + 1) }
        for (i in 1..m) {
            for (j in 1..n) {
                dp[i][j] = if (leftLines[i - 1] == rightLines[j - 1]) {
                    dp[i - 1][j - 1] + 1
                } else {
                    maxOf(dp[i - 1][j], dp[i][j - 1])
                }
            }
        }

        val result = mutableListOf<ConflictDiffLine.Text>()
        var i = m
        var j = n
        while (i > 0 || j > 0) {
            if (i > 0 && j > 0 && leftLines[i - 1] == rightLines[j - 1]) {
                result += ConflictDiffLine.Text(ConflictDiffLineType.Context, leftLines[i - 1])
                i--
                j--
            } else if (j > 0 && (i == 0 || dp[i][j - 1] >= dp[i - 1][j])) {
                result += ConflictDiffLine.Text(ConflictDiffLineType.Add, rightLines[j - 1])
                j--
            } else {
                result += ConflictDiffLine.Text(ConflictDiffLineType.Remove, leftLines[i - 1])
                i--
            }
        }
        return result.asReversed()
    }

    internal fun collapseContext(
        lines: List<ConflictDiffLine.Text>,
        contextLines: Int,
    ): List<ConflictDiffLine> {
        val shown = LinkedHashSet<Int>()
        lines.forEachIndexed { index, line ->
            if (line.type != ConflictDiffLineType.Context) {
                for (offset in -contextLines..contextLines) {
                    val candidate = index + offset
                    if (candidate in lines.indices) shown += candidate
                }
            }
        }
        if (shown.isEmpty()) return emptyList()

        val result = mutableListOf<ConflictDiffLine>()
        var previous = -1
        lines.forEachIndexed { index, line ->
            if (index !in shown) return@forEachIndexed
            if (previous == -1 && index > 0) {
                result += ConflictDiffLine.Ellipsis(index)
            } else if (previous != -1 && index > previous + 1) {
                result += ConflictDiffLine.Ellipsis(index - previous - 1)
            }
            result += line
            previous = index
        }
        if (previous != -1 && previous < lines.lastIndex) {
            result += ConflictDiffLine.Ellipsis(lines.lastIndex - previous)
        }
        return result
    }
}
