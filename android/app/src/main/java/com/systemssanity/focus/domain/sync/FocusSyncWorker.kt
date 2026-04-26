package com.systemssanity.focus.domain.sync

import android.content.Context
import androidx.work.Constraints
import androidx.work.CoroutineWorker
import androidx.work.ExistingWorkPolicy
import androidx.work.ListenableWorker.Result
import androidx.work.NetworkType
import androidx.work.OneTimeWorkRequestBuilder
import androidx.work.WorkManager
import androidx.work.WorkerParameters
import com.systemssanity.focus.FocusApplication
import com.systemssanity.focus.data.github.GitHubApiException
import com.systemssanity.focus.di.AppContainer
import kotlinx.coroutines.flow.first

class FocusSyncWorker(
    appContext: Context,
    workerParameters: WorkerParameters,
) : CoroutineWorker(appContext, workerParameters) {
    override suspend fun doWork(): Result {
        val container = (applicationContext as? FocusApplication)?.appContainer
            ?: AppContainer(applicationContext)
        val settings = container.preferencesStore.repoSettings.first()
        if (!settings.isComplete) {
            container.preferencesStore.recordSyncFailure("Connection settings are incomplete.", state = "blocked")
            return Result.failure()
        }

        val token = container.tokenStore.getToken(settings.scope)
        if (token.isNullOrBlank()) {
            container.preferencesStore.recordSyncFailure("GitHub token is missing.", state = "blocked")
            return Result.failure()
        }

        return container.createWorkspaceSyncCoordinator(settings, token)
            .processPendingOperations()
            .fold(
                onSuccess = { workspace ->
                    container.preferencesStore.recordSyncSuccess(
                        "Synced queued changes. Pending operations: ${workspace.pendingOperations.size}.",
                    )
                    Result.success()
                },
                onFailure = { error ->
                    container.preferencesStore.recordSyncFailure(error.message ?: "Queued sync failed.")
                    if (error.isPermanentSyncFailure()) Result.failure() else Result.retry()
                },
            )
    }

    companion object {
        private const val UNIQUE_WORK_NAME = "focus_pending_map_sync"

        fun enqueue(context: Context) {
            val request = OneTimeWorkRequestBuilder<FocusSyncWorker>()
                .setConstraints(
                    Constraints.Builder()
                        .setRequiredNetworkType(NetworkType.CONNECTED)
                        .build(),
                )
                .build()
            WorkManager.getInstance(context.applicationContext)
                .enqueueUniqueWork(UNIQUE_WORK_NAME, ExistingWorkPolicy.KEEP, request)
        }
    }
}

private fun Throwable.isPermanentSyncFailure(): Boolean =
    this is GitHubApiException && code in setOf(
        GitHubApiException.Code.Unauthorized,
        GitHubApiException.Code.Forbidden,
        GitHubApiException.Code.NotFound,
    )
