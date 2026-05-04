package com.systemssanity.focus.data.github

import com.systemssanity.focus.data.local.RepoSettings

internal interface GitHubAccessProbe {
    suspend fun probeRepository()
    suspend fun probeBranch()
}

internal object GitHubAccessValidation {
    const val SuccessMessage = "GitHub access validated."
    const val MissingSettingsMessage = "Repository owner, repository name, and branch are required."
    const val MissingTokenMessage = "A GitHub personal access token is required to validate this connection."

    suspend fun validate(
        settings: RepoSettings,
        token: String,
        probeFactory: (RepoSettings, String) -> GitHubAccessProbe = ::contentProbe,
    ): Result<Unit> =
        runCatching {
            val probe = probeFactory(settings, token)
            probe.probeRepository()
            probe.probeBranch()
        }

    fun shouldClearTokenAfterValidationFailure(error: Throwable): Boolean =
        error is GitHubApiException &&
            error.code in setOf(GitHubApiException.Code.Unauthorized, GitHubApiException.Code.Forbidden)

    fun failureState(error: Throwable): String =
        if (error is GitHubApiException && error.code == GitHubApiException.Code.RateLimit) {
            "warning"
        } else {
            "error"
        }

    fun failureMessage(error: Throwable): String =
        when (error) {
            is GitHubApiException -> githubFailureMessage(error)
            else -> error.message ?: "Could not validate GitHub access."
        }

    private fun githubFailureMessage(error: GitHubApiException): String {
        val context = error.contextLabel.takeIf { it.isNotBlank() } ?: "validating repository access"
        return when (error.code) {
            GitHubApiException.Code.RateLimit ->
                "GitHub rate limit reached while $context. Wait a moment and try again."
            GitHubApiException.Code.Unauthorized ->
                "Token was rejected (401 Unauthorized) while $context. Please generate a new token."
            GitHubApiException.Code.Forbidden ->
                "Token is valid but lacks required repository access (403 Forbidden) while $context. Update token permissions."
            GitHubApiException.Code.NotFound ->
                if (context.contains("branch", ignoreCase = true)) {
                    "Configured branch was not found (404 Not Found) while $context. Check the branch name."
                } else {
                    "Repository was not found (404 Not Found) while $context. Check the owner, repository name, and token access."
                }
            GitHubApiException.Code.Network ->
                "Unable to reach the GitHub API while $context. Check your network connection and try again."
            GitHubApiException.Code.Conflict,
            GitHubApiException.Code.Unknown -> error.message ?: "Could not validate GitHub access."
        }
    }
}

private fun contentProbe(settings: RepoSettings, token: String): GitHubAccessProbe =
    GitHubContentClientProbe(
        GitHubContentClient(
            owner = settings.repoOwner,
            repo = settings.repoName,
            branch = settings.repoBranch,
            token = token,
        ),
    )

private class GitHubContentClientProbe(private val client: GitHubContentClient) : GitHubAccessProbe {
    override suspend fun probeRepository() {
        client.probeRepository()
    }

    override suspend fun probeBranch() {
        client.probeBranch()
    }
}
