package com.systemssanity.focus.data.github

import com.systemssanity.focus.domain.maps.CommitMessages
import com.systemssanity.focus.domain.maps.MapConflictResolver
import com.systemssanity.focus.domain.model.MapSnapshot
import com.systemssanity.focus.domain.model.MindMapDocument
import com.systemssanity.focus.domain.model.MindMapJson

class GitHubMindMapProvider(
    private val client: GitHubContentClient,
    private val directoryPath: String,
) {
    suspend fun listMapFiles(): List<Pair<String, String>> =
        client.listDirectory(directoryPath.trim('/'))
            .filter { it.type == "file" && it.name.endsWith(".json", ignoreCase = true) }
            .map { it.name to it.path }
            .sortedBy { it.first.lowercase() }

    suspend fun loadMap(filePath: String): MapSnapshot {
        val snapshot = client.getTextFile(filePath)
        val fileName = filePath.substringAfterLast('/')
        val mapName = fileName.removeSuffix(".json")
        if (MapConflictResolver.hasConflictMarkers(snapshot.content)) {
            val resolved = MapConflictResolver.tryResolve(snapshot.content)
            if (!resolved.ok || resolved.resolvedContent.isNullOrBlank()) {
                throw UnreadableMapException(
                    reason = UnreadableMapReason.AutoResolveFailed,
                    filePath = filePath,
                    rawText = snapshot.content,
                )
            }
            val saved = client.putTextFile(
                path = filePath,
                content = resolved.resolvedContent,
                expectedRevision = snapshot.versionToken,
                message = CommitMessages.conflictResolve(mapName),
            )
            return MapSnapshot(
                filePath = filePath,
                fileName = fileName,
                mapName = mapName,
                document = MindMapJson.parse(resolved.resolvedContent),
                revision = saved.versionToken,
                loadedAtMillis = System.currentTimeMillis(),
            )
        }
        return MapSnapshot(
            filePath = filePath,
            fileName = fileName,
            mapName = mapName,
            document = parseReadableMap(filePath, snapshot.content),
            revision = snapshot.versionToken,
            loadedAtMillis = System.currentTimeMillis(),
        )
    }

    suspend fun saveMap(filePath: String, document: MindMapDocument, expectedRevision: String?, commitMessage: String): String =
        client.putTextFile(filePath, MindMapJson.serialize(document), expectedRevision, commitMessage).versionToken

    suspend fun createMap(filePath: String, document: MindMapDocument, commitMessage: String): String =
        client.putTextFile(filePath, MindMapJson.serialize(document), null, commitMessage).versionToken

    suspend fun deleteMap(filePath: String, expectedRevision: String, commitMessage: String) {
        client.deleteFile(filePath, expectedRevision, commitMessage)
    }

    suspend fun loadAttachment(nodeId: String, relativePath: String, mediaType: String): GitHubBinarySnapshot =
        client.getBinaryFile(buildAttachmentPath(nodeId, relativePath), mediaType.ifBlank { "application/octet-stream" })

    suspend fun uploadAttachment(nodeId: String, relativePath: String, base64Content: String, commitMessage: String): String =
        client.putBinaryFile(buildAttachmentPath(nodeId, relativePath), base64Content, null, commitMessage).versionToken

    suspend fun deleteAttachment(nodeId: String, relativePath: String, expectedRevision: String?, commitMessage: String) {
        val path = buildAttachmentPath(nodeId, relativePath)
        val revision = expectedRevision ?: client.getBinaryFile(path, "application/octet-stream").versionToken
        client.deleteFile(path, revision, commitMessage)
    }

    suspend fun saveConflictResolution(filePath: String, mapName: String, document: MindMapDocument, expectedRevision: String): String =
        saveMap(filePath, document, expectedRevision, CommitMessages.conflictResolve(mapName))

    private fun buildAttachmentPath(nodeId: String, relativePath: String): String {
        val parts = mutableListOf<String>()
        val normalizedDirectory = directoryPath.trim('/')
        if (normalizedDirectory.isNotBlank()) parts += normalizedDirectory
        parts += "_attachments"
        parts += nodeId.trim().lowercase()
        parts += relativePath.substringAfterLast('/').substringAfterLast('\\')
        return parts.joinToString("/")
    }

    private fun parseReadableMap(filePath: String, content: String): MindMapDocument =
        try {
            MindMapJson.parse(content)
        } catch (error: Exception) {
            throw UnreadableMapException(
                reason = if (MapConflictResolver.hasConflictMarkers(content)) {
                    UnreadableMapReason.MergeConflict
                } else {
                    UnreadableMapReason.InvalidJson
                },
                filePath = filePath,
                rawText = content,
                cause = error,
            )
        }
}

enum class UnreadableMapReason {
    AutoResolveFailed,
    MergeConflict,
    InvalidJson,
    Unknown,
}

class UnreadableMapException(
    val reason: UnreadableMapReason,
    val filePath: String,
    val rawText: String,
    cause: Throwable? = null,
) : Exception("Map \"$filePath\" could not be parsed.", cause)
