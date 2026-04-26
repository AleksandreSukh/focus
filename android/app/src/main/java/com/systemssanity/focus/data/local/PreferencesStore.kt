package com.systemssanity.focus.data.local

import android.content.Context
import androidx.datastore.preferences.core.edit
import androidx.datastore.preferences.core.stringPreferencesKey
import androidx.datastore.preferences.preferencesDataStore
import kotlinx.coroutines.flow.Flow
import kotlinx.coroutines.flow.distinctUntilChanged
import kotlinx.coroutines.flow.map

private val Context.focusDataStore by preferencesDataStore(name = "focus_preferences")

class PreferencesStore(private val context: Context) {
    val repoSettings: Flow<RepoSettings> =
        context.focusDataStore.data.map { preferences ->
            RepoSettings(
                repoOwner = preferences[Keys.repoOwner].orEmpty(),
                repoName = preferences[Keys.repoName].orEmpty(),
                repoBranch = preferences[Keys.repoBranch] ?: "main",
                repoPath = preferences[Keys.repoPath] ?: "/",
            )
        }.distinctUntilChanged()

    val uiPreferences: Flow<UiPreferences> =
        context.focusDataStore.data.map { preferences ->
            UiPreferences(
                theme = when (preferences[Keys.theme]) {
                    "dark" -> ThemePreference.Dark
                    else -> ThemePreference.Light
                },
                fabSide = when (preferences[Keys.fabSide]) {
                    "left" -> FabSidePreference.Left
                    else -> FabSidePreference.Right
                },
            )
        }.distinctUntilChanged()

    val syncMetadata: Flow<SyncMetadata> =
        context.focusDataStore.data.map { preferences ->
            SyncMetadata(
                lastSyncAt = preferences[Keys.lastSyncAt],
                lastSyncState = preferences[Keys.lastSyncState],
                lastMessage = preferences[Keys.lastMessage],
                lastErrorSummary = preferences[Keys.lastErrorSummary],
            )
        }.distinctUntilChanged()

    suspend fun saveRepoSettings(settings: RepoSettings) {
        context.focusDataStore.edit { preferences ->
            preferences[Keys.repoOwner] = settings.repoOwner.trim()
            preferences[Keys.repoName] = settings.repoName.trim()
            preferences[Keys.repoBranch] = settings.repoBranch.trim().ifBlank { "main" }
            preferences[Keys.repoPath] = settings.repoPath.trim().ifBlank { "/" }
        }
    }

    suspend fun saveUiPreferences(preferences: UiPreferences) {
        context.focusDataStore.edit { values ->
            values[Keys.theme] = when (preferences.theme) {
                ThemePreference.Dark -> "dark"
                ThemePreference.Light -> "light"
            }
            values[Keys.fabSide] = when (preferences.fabSide) {
                FabSidePreference.Left -> "left"
                FabSidePreference.Right -> "right"
            }
        }
    }

    suspend fun recordSyncSuccess(message: String) {
        context.focusDataStore.edit { preferences ->
            preferences[Keys.lastSyncAt] = com.systemssanity.focus.domain.model.ClockProvider.nowIsoSeconds()
            preferences[Keys.lastSyncState] = "success"
            preferences[Keys.lastMessage] = message.take(300)
            preferences.remove(Keys.lastErrorSummary)
        }
    }

    suspend fun recordSyncFailure(summary: String, state: String = "error") {
        val trimmed = summary.trim().take(300)
        context.focusDataStore.edit { preferences ->
            preferences[Keys.lastSyncAt] = com.systemssanity.focus.domain.model.ClockProvider.nowIsoSeconds()
            preferences[Keys.lastSyncState] = state
            preferences[Keys.lastMessage] = trimmed
            preferences[Keys.lastErrorSummary] = trimmed
        }
    }

    suspend fun clearAll() {
        context.focusDataStore.edit { it.clear() }
    }

    private object Keys {
        val repoOwner = stringPreferencesKey("repo_owner")
        val repoName = stringPreferencesKey("repo_name")
        val repoBranch = stringPreferencesKey("repo_branch")
        val repoPath = stringPreferencesKey("repo_path")
        val theme = stringPreferencesKey("theme")
        val fabSide = stringPreferencesKey("fab_side")
        val lastSyncAt = stringPreferencesKey("last_sync_at")
        val lastSyncState = stringPreferencesKey("last_sync_state")
        val lastMessage = stringPreferencesKey("last_message")
        val lastErrorSummary = stringPreferencesKey("last_error_summary")
    }
}
