package com.systemssanity.focus.data.local

import com.systemssanity.focus.domain.maps.MapMutation
import com.systemssanity.focus.domain.model.MapSnapshot
import kotlinx.serialization.Serializable

data class RepoSettings(
    val repoOwner: String = "",
    val repoName: String = "",
    val repoBranch: String = "main",
    val repoPath: String = "/",
) {
    val isComplete: Boolean
        get() = repoOwner.isNotBlank() && repoName.isNotBlank() && repoBranch.isNotBlank()

    val scope: String
        get() = listOf(repoOwner.ifBlank { "owner" }, repoName.ifBlank { "repo" }, repoBranch, repoPath)
            .joinToString("::")
            .lowercase()

    fun describe(): String {
        val path = if (repoPath.isBlank() || repoPath == "/") "/" else "/${repoPath.trim('/')}"
        return "$repoOwner/$repoName@$repoBranch$path"
    }
}

data class SyncMetadata(
    val lastSyncAt: String? = null,
    val lastSyncState: String? = null,
    val lastMessage: String? = null,
    val lastErrorSummary: String? = null,
)

enum class ThemePreference { Light, Dark }

enum class FabSidePreference { Left, Right }

data class UiPreferences(
    val theme: ThemePreference = ThemePreference.Light,
    val fabSide: FabSidePreference = FabSidePreference.Right,
)

@Serializable
data class PendingMapOperation(
    val id: String,
    val scope: String,
    val operation: MapMutation,
    val enqueuedAtMillis: Long,
)

data class CachedWorkspace(
    val snapshots: List<MapSnapshot> = emptyList(),
    val pendingOperations: List<PendingMapOperation> = emptyList(),
)
