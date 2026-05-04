package com.systemssanity.focus.domain.maps

object MapFilePaths {
    fun build(repoPath: String, mapName: String): String {
        val directory = repoPath.trim('/')
        val fileName = sanitizeFileName(mapName)
        return if (directory.isBlank()) fileName else "$directory/$fileName"
    }

    fun rename(oldFilePath: String, newRootTitle: String): String {
        val directory = oldFilePath.substringBeforeLast('/', missingDelimiterValue = "")
        val fileName = sanitizeFileName(newRootTitle)
        return if (directory.isBlank()) fileName else "$directory/$fileName"
    }

    fun fileName(filePath: String): String =
        filePath.substringAfterLast('/')

    fun mapName(filePath: String): String =
        fileName(filePath).removeSuffix(".json")

    fun sanitizeFileName(mapName: String): String {
        val sanitized = mapName
            .trim()
            .replace(Regex("[\\\\/:*?\"<>|]+"), "-")
            .replace(Regex("\\s+"), " ")
            .trim('.', ' ')
            .ifBlank { "Untitled" }
        val withoutExtension = if (sanitized.endsWith(".json", ignoreCase = true)) {
            sanitized.dropLast(5)
        } else {
            sanitized
        }.ifBlank { "Untitled" }
        return "$withoutExtension.json"
    }
}
