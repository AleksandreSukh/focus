package com.systemssanity.focus.data.github

import kotlinx.coroutines.Dispatchers
import kotlinx.coroutines.withContext
import kotlinx.serialization.json.Json
import kotlinx.serialization.json.JsonArray
import kotlinx.serialization.json.JsonObject
import kotlinx.serialization.json.JsonPrimitive
import kotlinx.serialization.json.contentOrNull
import kotlinx.serialization.json.jsonArray
import kotlinx.serialization.json.jsonObject
import kotlinx.serialization.json.jsonPrimitive
import okhttp3.MediaType.Companion.toMediaType
import okhttp3.OkHttpClient
import okhttp3.Request
import okhttp3.RequestBody.Companion.toRequestBody
import java.util.Base64

class GitHubContentClient(
    private val owner: String,
    private val repo: String,
    private val branch: String,
    private val token: String,
    private val apiBaseUrl: String = "https://api.github.com",
    private val httpClient: OkHttpClient = OkHttpClient(),
) {
    private val json = Json { ignoreUnknownKeys = true }

    suspend fun probeRepository() {
        requestJson("/repos/$owner/$repo", "GET", contextLabel = "validating repository access")
    }

    suspend fun probeBranch() {
        requestJson(
            "/repos/$owner/$repo/branches/${branch.urlSegment()}",
            "GET",
            contextLabel = "validating branch \"$branch\"",
        )
    }

    suspend fun listDirectory(path: String): List<GitHubContentEntry> {
        val normalized = path.normalizedPath()
        val contentPath = if (normalized.isBlank()) "contents" else "contents/$normalized"
        val element = requestJson(
            "/repos/$owner/$repo/$contentPath?ref=${branch.urlQuery()}",
            "GET",
            contextLabel = "listing the configured maps directory",
        )
        return (element as? JsonArray)?.jsonArray.orEmpty().mapNotNull { item ->
            val obj = item as? JsonObject ?: return@mapNotNull null
            GitHubContentEntry(
                name = obj.string("name"),
                path = obj.string("path"),
                type = obj.string("type"),
                sha = obj.string("sha"),
            )
        }
    }

    suspend fun getTextFile(path: String): GitHubFileSnapshot {
        val obj = requestJson(
            "/repos/$owner/$repo/contents/${path.normalizedPath()}?ref=${branch.urlQuery()}",
            "GET",
            contextLabel = "loading remote file $path",
        ).jsonObject
        val sha = obj.string("sha")
        val encoding = obj.string("encoding")
        val content = if (encoding == "base64") {
            Base64.getMimeDecoder().decode(obj.string("content")).decodeToString()
        } else {
            getBlobBytes(sha, "loading remote blob $sha").decodeToString()
        }
        return GitHubFileSnapshot(content = content, versionToken = sha)
    }

    suspend fun getBinaryFile(path: String, mediaType: String): GitHubBinarySnapshot {
        val obj = requestJson(
            "/repos/$owner/$repo/contents/${path.normalizedPath()}?ref=${branch.urlQuery()}",
            "GET",
            contextLabel = "loading attachment $path",
        ).jsonObject
        val sha = obj.string("sha")
        val bytes = if (obj.string("encoding") == "base64") {
            Base64.getMimeDecoder().decode(obj.string("content"))
        } else {
            getBlobBytes(sha, "loading attachment blob $sha")
        }
        return GitHubBinarySnapshot(bytes = bytes, mediaType = mediaType, versionToken = sha)
    }

    suspend fun putTextFile(path: String, content: String, expectedRevision: String?, message: String): GitHubSaveResult =
        putFile(path, Base64.getEncoder().encodeToString(content.toByteArray()), expectedRevision, message)

    suspend fun putBinaryFile(path: String, base64Content: String, expectedRevision: String?, message: String): GitHubSaveResult =
        putFile(path, base64Content, expectedRevision, message)

    suspend fun deleteFile(path: String, expectedRevision: String, message: String): GitHubSaveResult {
        val payload = JsonObject(
            mapOf(
                "message" to JsonPrimitive(message),
                "sha" to JsonPrimitive(expectedRevision),
                "branch" to JsonPrimitive(branch),
            ),
        )
        val obj = requestJson(
            "/repos/$owner/$repo/contents/${path.normalizedPath()}",
            "DELETE",
            body = payload.toString(),
            contextLabel = "deleting remote file $path",
        ).jsonObject
        return GitHubSaveResult(
            versionToken = "",
            commitSha = obj["commit"]?.jsonObject?.string("sha"),
        )
    }

    private suspend fun putFile(
        path: String,
        base64Content: String,
        expectedRevision: String?,
        message: String,
    ): GitHubSaveResult {
        val fields = buildMap<String, JsonPrimitive> {
            put("message", JsonPrimitive(message))
            put("content", JsonPrimitive(base64Content))
            put("branch", JsonPrimitive(branch))
            if (!expectedRevision.isNullOrBlank()) put("sha", JsonPrimitive(expectedRevision))
        }
        val obj = requestJson(
            "/repos/$owner/$repo/contents/${path.normalizedPath()}",
            "PUT",
            body = JsonObject(fields).toString(),
            contextLabel = "saving remote file $path",
        ).jsonObject
        return GitHubSaveResult(
            versionToken = obj["content"]?.jsonObject?.string("sha").orEmpty(),
            commitSha = obj["commit"]?.jsonObject?.string("sha"),
        )
    }

    private suspend fun getBlobBytes(blobSha: String, contextLabel: String): ByteArray = withContext(Dispatchers.IO) {
        val request = baseRequest("/repos/$owner/$repo/git/blobs/${blobSha.urlSegment()}")
            .header("Accept", "application/vnd.github.raw+json")
            .get()
            .build()
        execute(request, contextLabel).use { response ->
            response.body?.bytes() ?: ByteArray(0)
        }
    }

    private suspend fun requestJson(
        endpoint: String,
        method: String,
        body: String? = null,
        contextLabel: String,
    ) = withContext(Dispatchers.IO) {
        val requestBody = body?.toRequestBody("application/json".toMediaType())
        val request = baseRequest(endpoint)
            .method(method, requestBody)
            .build()
        execute(request, contextLabel).use { response ->
            json.parseToJsonElement(response.body?.string().orEmpty())
        }
    }

    private fun baseRequest(endpoint: String): Request.Builder =
        Request.Builder()
            .url("$apiBaseUrl$endpoint")
            .header("Accept", "application/vnd.github+json")
            .header("Authorization", "Bearer $token")
            .header("X-GitHub-Api-Version", "2022-11-28")

    private fun execute(request: Request, contextLabel: String): okhttp3.Response =
        try {
            httpClient.newCall(request).execute().also { response ->
                if (!response.isSuccessful) {
                    val responseText = response.body?.string().orEmpty()
                    throw GitHubApiException(
                        code = classify(response.code, responseText, response.header("x-ratelimit-remaining"), response.header("retry-after")),
                        status = response.code,
                        contextLabel = contextLabel,
                        message = buildErrorMessage(response.code, contextLabel, responseText),
                    )
                }
            }
        } catch (error: GitHubApiException) {
            throw error
        } catch (error: Exception) {
            throw GitHubApiException(
                code = GitHubApiException.Code.Network,
                status = null,
                contextLabel = contextLabel,
                message = "Unable to reach the GitHub API while $contextLabel.",
                cause = error,
            )
        }

    private fun classify(status: Int, responseText: String, remaining: String?, retryAfter: String?): GitHubApiException.Code =
        when {
            status == 401 -> GitHubApiException.Code.Unauthorized
            status == 404 -> GitHubApiException.Code.NotFound
            status == 409 || status == 422 -> GitHubApiException.Code.Conflict
            status == 403 && (remaining == "0" || retryAfter != null || responseText.contains("rate limit", ignoreCase = true)) -> GitHubApiException.Code.RateLimit
            status == 403 -> GitHubApiException.Code.Forbidden
            else -> GitHubApiException.Code.Unknown
        }

    private fun buildErrorMessage(status: Int, contextLabel: String, responseText: String): String =
        if (responseText.isNotBlank()) {
            "GitHub API request failed while $contextLabel (HTTP $status): ${responseText.take(220)}"
        } else {
            "GitHub API request failed while $contextLabel (HTTP $status)."
        }

    private fun JsonObject.string(name: String): String =
        this[name]?.jsonPrimitive?.contentOrNull.orEmpty()

    private fun String.normalizedPath(): String =
        split('/').filter { it.isNotBlank() }.joinToString("/") { it.urlSegment() }

    private fun String.urlSegment(): String =
        java.net.URLEncoder.encode(this, Charsets.UTF_8.name()).replace("+", "%20")

    private fun String.urlQuery(): String = urlSegment()
}
