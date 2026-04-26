package com.systemssanity.focus.data.github

data class GitHubContentEntry(
    val name: String,
    val path: String,
    val type: String,
    val sha: String,
)

data class GitHubFileSnapshot(
    val content: String,
    val versionToken: String,
)

data class GitHubBinarySnapshot(
    val bytes: ByteArray,
    val mediaType: String,
    val versionToken: String,
)

data class GitHubSaveResult(
    val versionToken: String,
    val commitSha: String?,
)

class GitHubApiException(
    val code: Code,
    val status: Int?,
    val contextLabel: String,
    message: String,
    cause: Throwable? = null,
) : Exception(message, cause) {
    enum class Code {
        Network,
        Unauthorized,
        Forbidden,
        NotFound,
        Conflict,
        RateLimit,
        Unknown,
    }
}
