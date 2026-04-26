package com.systemssanity.focus.domain.sync

import com.systemssanity.focus.data.github.GitHubApiException
import com.systemssanity.focus.data.github.GitHubBinarySnapshot
import com.systemssanity.focus.data.github.GitHubMindMapProvider
import com.systemssanity.focus.domain.model.MapSnapshot
import com.systemssanity.focus.domain.model.MindMapDocument

class MindMapRepository(private val provider: GitHubMindMapProvider) {
    suspend fun listFiles(): Result<List<Pair<String, String>>> =
        runCatching { provider.listMapFiles() }

    suspend fun loadMap(filePath: String): Result<MapSnapshot> =
        runCatching { provider.loadMap(filePath) }

    suspend fun saveMap(filePath: String, document: MindMapDocument, revision: String?, commitMessage: String): Result<String> =
        runCatching { provider.saveMap(filePath, document, revision, commitMessage) }

    suspend fun createMap(filePath: String, document: MindMapDocument, commitMessage: String): Result<String> =
        runCatching { provider.createMap(filePath, document, commitMessage) }

    suspend fun deleteMap(filePath: String, revision: String, commitMessage: String): Result<Unit> =
        runCatching { provider.deleteMap(filePath, revision, commitMessage) }

    suspend fun renameMap(
        oldFilePath: String,
        newFilePath: String,
        document: MindMapDocument,
        oldRevision: String,
        commitMessage: String,
    ): Result<String> =
        runCatching {
            val newRevision = provider.createMap(newFilePath, document, commitMessage)
            provider.deleteMap(oldFilePath, oldRevision, commitMessage)
            newRevision
        }

    suspend fun loadAttachment(nodeId: String, relativePath: String, mediaType: String): Result<GitHubBinarySnapshot> =
        runCatching { provider.loadAttachment(nodeId, relativePath, mediaType) }

    suspend fun uploadAttachment(nodeId: String, relativePath: String, base64Content: String, commitMessage: String): Result<String> =
        runCatching { provider.uploadAttachment(nodeId, relativePath, base64Content, commitMessage) }

    suspend fun deleteAttachment(nodeId: String, relativePath: String, expectedRevision: String?, commitMessage: String): Result<Unit> =
        runCatching { provider.deleteAttachment(nodeId, relativePath, expectedRevision, commitMessage) }

    fun Throwable.isStaleState(): Boolean =
        this is GitHubApiException && code == GitHubApiException.Code.Conflict
}
