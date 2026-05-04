package com.systemssanity.focus.data.github

import com.systemssanity.focus.data.local.RepoSettings
import kotlinx.coroutines.runBlocking
import kotlin.test.Test
import kotlin.test.assertEquals
import kotlin.test.assertFailsWith
import kotlin.test.assertTrue

class GitHubAccessValidationTest {
    @Test
    fun validatesRepositoryBeforeBranch() = runBlocking {
        val probe = FakeGitHubAccessProbe()
        val result = GitHubAccessValidation.validate(settings, "token") { _, _ -> probe }

        assertTrue(result.isSuccess)
        assertEquals(listOf("repository", "branch"), probe.calls)
    }

    @Test
    fun stopsWhenRepositoryProbeFails() = runBlocking {
        val probe = FakeGitHubAccessProbe(repositoryError = IllegalStateException("repo failed"))
        val error = assertFailsWith<IllegalStateException> {
            GitHubAccessValidation.validate(settings, "token") { _, _ -> probe }.getOrThrow()
        }

        assertEquals("repo failed", error.message)
        assertEquals(listOf("repository"), probe.calls)
    }

    @Test
    fun returnsBranchProbeFailures() = runBlocking {
        val probe = FakeGitHubAccessProbe(branchError = IllegalStateException("branch failed"))
        val error = assertFailsWith<IllegalStateException> {
            GitHubAccessValidation.validate(settings, "token") { _, _ -> probe }.getOrThrow()
        }

        assertEquals("branch failed", error.message)
        assertEquals(listOf("repository", "branch"), probe.calls)
    }

    private val settings = RepoSettings(
        repoOwner = "systems",
        repoName = "focus",
        repoBranch = "main",
        repoPath = "FocusMaps",
    )
}

private class FakeGitHubAccessProbe(
    private val repositoryError: Throwable? = null,
    private val branchError: Throwable? = null,
) : GitHubAccessProbe {
    val calls = mutableListOf<String>()

    override suspend fun probeRepository() {
        calls += "repository"
        repositoryError?.let { throw it }
    }

    override suspend fun probeBranch() {
        calls += "branch"
        branchError?.let { throw it }
    }
}
