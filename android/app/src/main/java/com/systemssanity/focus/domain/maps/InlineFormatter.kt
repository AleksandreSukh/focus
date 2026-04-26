package com.systemssanity.focus.domain.maps

data class InlineRun(val text: String, val colorName: String?)

data class InlineToken(
    val text: String,
    val colorName: String?,
    val url: String? = null,
)

object InlineFormatter {
    private val colors = setOf(
        "black",
        "darkblue",
        "darkgreen",
        "darkcyan",
        "darkred",
        "darkmagenta",
        "darkyellow",
        "gray",
        "darkgray",
        "blue",
        "green",
        "cyan",
        "red",
        "magenta",
        "yellow",
        "white",
    )
    private val urlRegex = Regex("https?://[^\\s<>\"']+", RegexOption.IGNORE_CASE)

    fun parseRuns(input: String): List<InlineRun> {
        val runs = mutableListOf<InlineRun>()
        val pending = StringBuilder()
        var activeColor: String? = null

        fun flush() {
            if (pending.isEmpty()) return
            val text = pending.toString()
            val last = runs.lastOrNull()
            if (last != null && last.colorName == activeColor) {
                runs[runs.lastIndex] = last.copy(text = last.text + text)
            } else {
                runs += InlineRun(text, activeColor)
            }
            pending.clear()
        }

        var index = 0
        while (index < input.length) {
            if (input[index] != '[') {
                pending.append(input[index])
                index += 1
                continue
            }
            val end = input.indexOf(']', startIndex = index + 1)
            if (end < 0) {
                pending.append(input[index])
                index += 1
                continue
            }
            val command = input.substring(index + 1, end)
            val normalized = command.lowercase()
            when {
                command == "!" -> {
                    flush()
                    activeColor = null
                    index = end + 1
                }
                normalized in colors -> {
                    flush()
                    activeColor = normalized
                    index = end + 1
                }
                else -> {
                    pending.append(input.substring(index, end + 1))
                    index = end + 1
                }
            }
        }
        flush()
        return runs
    }

    fun plainText(input: String): String =
        parseRuns(input).joinToString(separator = "") { it.text }

    fun tokenize(input: String): List<InlineToken> =
        parseRuns(input).flatMap { run -> tokenizeLinks(run) }

    private fun tokenizeLinks(run: InlineRun): List<InlineToken> {
        val tokens = mutableListOf<InlineToken>()
        var index = 0
        urlRegex.findAll(run.text).forEach { match ->
            if (match.range.first > index) {
                tokens += InlineToken(run.text.substring(index, match.range.first), run.colorName)
            }
            val matched = match.value
            val trimmed = trimTrailingUrlPunctuation(matched)
            tokens += InlineToken(trimmed, run.colorName, url = trimmed)
            if (trimmed.length < matched.length) {
                tokens += InlineToken(matched.substring(trimmed.length), run.colorName)
            }
            index = match.range.last + 1
        }
        if (index < run.text.length) {
            tokens += InlineToken(run.text.substring(index), run.colorName)
        }
        return tokens
    }

    private fun trimTrailingUrlPunctuation(candidate: String): String {
        var end = candidate.length
        while (end > 0 && candidate[end - 1] in listOf('.', ',', ';', ':', '!', '?')) {
            end -= 1
        }
        return candidate.substring(0, end)
    }
}
