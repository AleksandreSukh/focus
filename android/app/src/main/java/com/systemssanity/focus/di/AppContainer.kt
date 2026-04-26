package com.systemssanity.focus.di

import android.content.Context
import com.systemssanity.focus.data.github.GitHubContentClient
import com.systemssanity.focus.data.github.GitHubMindMapProvider
import com.systemssanity.focus.data.local.FileFocusLocalStore
import com.systemssanity.focus.data.local.PreferencesStore
import com.systemssanity.focus.data.local.RepoSettings
import com.systemssanity.focus.data.local.SecureTokenStore
import com.systemssanity.focus.domain.sync.MindMapRepository
import com.systemssanity.focus.domain.sync.MindMapService
import com.systemssanity.focus.domain.sync.WorkspaceSyncCoordinator

class AppContainer(context: Context) {
    val preferencesStore = PreferencesStore(context.applicationContext)
    val tokenStore = SecureTokenStore(context.applicationContext)
    val localStore = FileFocusLocalStore(context.applicationContext)

    fun createMindMapService(settings: RepoSettings, token: String): MindMapService {
        val client = GitHubContentClient(
            owner = settings.repoOwner,
            repo = settings.repoName,
            branch = settings.repoBranch,
            token = token,
        )
        val provider = GitHubMindMapProvider(
            client = client,
            directoryPath = settings.repoPath,
        )
        return MindMapService(MindMapRepository(provider))
    }

    fun createWorkspaceSyncCoordinator(settings: RepoSettings, token: String): WorkspaceSyncCoordinator =
        WorkspaceSyncCoordinator(
            scope = settings.scope,
            localStore = localStore,
            service = createMindMapService(settings, token),
        )
}
