package com.systemssanity.focus.ui

import android.Manifest
import android.app.Activity
import android.content.Intent
import android.content.pm.PackageManager
import android.graphics.Bitmap
import android.graphics.BitmapFactory
import android.graphics.pdf.PdfRenderer
import android.media.MediaPlayer
import android.net.Uri
import android.os.ParcelFileDescriptor

import androidx.activity.compose.BackHandler
import androidx.activity.compose.rememberLauncherForActivityResult
import androidx.activity.result.contract.ActivityResultContracts
import androidx.compose.foundation.Image
import androidx.compose.foundation.BorderStroke
import androidx.compose.foundation.background
import androidx.compose.foundation.border
import androidx.compose.foundation.clickable
import androidx.compose.foundation.horizontalScroll
import androidx.compose.foundation.layout.Arrangement
import androidx.compose.foundation.layout.Box
import androidx.compose.foundation.layout.Column
import androidx.compose.foundation.layout.PaddingValues
import androidx.compose.foundation.layout.Row
import androidx.compose.foundation.layout.Spacer
import androidx.compose.foundation.layout.fillMaxSize
import androidx.compose.foundation.layout.fillMaxWidth
import androidx.compose.foundation.layout.height
import androidx.compose.foundation.layout.heightIn
import androidx.compose.foundation.layout.padding
import androidx.compose.foundation.layout.size
import androidx.compose.foundation.layout.width
import androidx.compose.foundation.layout.widthIn
import androidx.compose.foundation.lazy.LazyColumn
import androidx.compose.foundation.lazy.items
import androidx.compose.foundation.rememberScrollState
import androidx.compose.foundation.shape.CircleShape
import androidx.compose.foundation.shape.RoundedCornerShape
import androidx.compose.foundation.verticalScroll
import androidx.compose.material.icons.Icons
import androidx.compose.material.icons.automirrored.filled.ArrowBack
import androidx.compose.material.icons.automirrored.filled.ArrowForward
import androidx.compose.material.icons.automirrored.filled.NoteAdd
import androidx.compose.material.icons.filled.Add
import androidx.compose.material.icons.filled.AddTask
import androidx.compose.material.icons.filled.AttachFile
import androidx.compose.material.icons.filled.CloudDownload
import androidx.compose.material.icons.filled.DarkMode
import androidx.compose.material.icons.filled.Delete
import androidx.compose.material.icons.filled.Download
import androidx.compose.material.icons.filled.LightMode
import androidx.compose.material.icons.filled.Map
import androidx.compose.material.icons.filled.MoreVert
import androidx.compose.material.icons.filled.Refresh
import androidx.compose.material.icons.filled.Remove
import androidx.compose.material.icons.filled.Settings
import androidx.compose.material.icons.filled.Star
import androidx.compose.material.icons.filled.StarBorder
import androidx.compose.material.icons.filled.Sync
import androidx.compose.material.icons.filled.Visibility
import androidx.compose.material.icons.filled.VisibilityOff
import androidx.compose.material3.AlertDialog
import androidx.compose.material3.Button
import androidx.compose.material3.ButtonDefaults
import androidx.compose.material3.DropdownMenu
import androidx.compose.material3.DropdownMenuItem
import androidx.compose.material3.Icon
import androidx.compose.material3.MaterialTheme
import androidx.compose.material3.OutlinedButton
import androidx.compose.material3.OutlinedTextField
import androidx.compose.material3.OutlinedTextFieldDefaults
import androidx.compose.material3.Scaffold
import androidx.compose.material3.Surface
import androidx.compose.material3.Text
import androidx.compose.material3.TextButton
import androidx.compose.runtime.Composable
import androidx.compose.runtime.DisposableEffect
import androidx.compose.runtime.LaunchedEffect
import androidx.compose.runtime.getValue
import androidx.compose.runtime.mutableStateOf
import androidx.compose.runtime.remember
import androidx.compose.runtime.rememberCoroutineScope
import androidx.compose.runtime.setValue
import androidx.compose.ui.Alignment
import androidx.compose.ui.Modifier
import androidx.compose.ui.draw.clip
import androidx.compose.ui.focus.FocusRequester
import androidx.compose.ui.focus.focusRequester
import androidx.compose.ui.graphics.Color
import androidx.compose.ui.graphics.asImageBitmap
import androidx.compose.ui.graphics.vector.ImageVector
import androidx.compose.ui.platform.LocalContext
import androidx.compose.ui.semantics.Role
import androidx.compose.ui.semantics.contentDescription
import androidx.compose.ui.semantics.semantics
import androidx.compose.ui.semantics.stateDescription
import androidx.compose.ui.layout.ContentScale
import androidx.compose.ui.text.font.FontWeight
import androidx.compose.ui.text.font.FontFamily
import androidx.compose.ui.text.style.TextOverflow
import androidx.compose.ui.unit.dp
import androidx.lifecycle.viewmodel.compose.viewModel
import androidx.core.content.ContextCompat
import androidx.core.content.FileProvider
import com.systemssanity.focus.domain.maps.AttachmentExports
import com.systemssanity.focus.domain.maps.AttachmentViewerKind
import com.systemssanity.focus.domain.maps.AttachmentViewers
import com.systemssanity.focus.data.local.FabSidePreference
import com.systemssanity.focus.data.local.RepoSettings
import com.systemssanity.focus.data.local.ThemePreference
import com.systemssanity.focus.domain.maps.AttachmentUploads
import com.systemssanity.focus.domain.maps.BlockedPendingMapEntry
import com.systemssanity.focus.domain.maps.BlockedPendingMaps
import com.systemssanity.focus.domain.maps.ConflictDiffLine
import com.systemssanity.focus.domain.maps.ConflictDiffLineType
import com.systemssanity.focus.domain.maps.ConflictDiffResult
import com.systemssanity.focus.domain.maps.ConflictDiffs
import com.systemssanity.focus.domain.maps.LocalMapRepairs
import com.systemssanity.focus.domain.maps.MapQueries
import com.systemssanity.focus.domain.maps.NodeRecord
import com.systemssanity.focus.domain.maps.PendingConflictMapEntry
import com.systemssanity.focus.domain.maps.PendingConflictResolution
import com.systemssanity.focus.domain.maps.PendingConflicts
import com.systemssanity.focus.domain.maps.RelatedNodeEntry
import com.systemssanity.focus.domain.maps.RelatedNodes
import com.systemssanity.focus.domain.maps.TaskEntry
import com.systemssanity.focus.domain.maps.TaskFilter
import com.systemssanity.focus.domain.maps.UnreadableMapEntry
import com.systemssanity.focus.domain.maps.UnreadableMaps
import com.systemssanity.focus.domain.maps.VoiceNotes
import com.systemssanity.focus.domain.model.MapSnapshot
import com.systemssanity.focus.domain.model.MindMapDocument
import com.systemssanity.focus.domain.model.Node
import com.systemssanity.focus.domain.model.NodeAttachment
import com.systemssanity.focus.domain.model.NodeMetadataSources
import com.systemssanity.focus.domain.model.TaskState
import kotlinx.coroutines.Dispatchers
import kotlinx.coroutines.delay
import kotlinx.coroutines.launch
import kotlinx.coroutines.withContext
import java.io.File
import java.time.Instant
import java.time.ZoneId
import java.time.format.DateTimeFormatter

@Composable
internal fun FocusApp(
    viewModel: FocusViewModel = viewModel(),
    routeRequest: NativeRouteRequest? = null,
) {
    val uiState = viewModel.uiState

    FocusTheme(themePreference = uiState.uiPreferences.theme) {
        val palette = LocalFocusPalette.current
        Surface(
            modifier = Modifier.fillMaxSize(),
            color = palette.pageBackground,
            contentColor = palette.text,
        ) {
            var routeHistory by remember { mutableStateOf(NativeRouteHistory()) }
            var showConnection by remember { mutableStateOf(true) }
            var closeConnectionAfterLoad by remember { mutableStateOf(false) }
            var initialScreenResolved by remember { mutableStateOf(false) }
            var appliedRouteRequestVersion by remember { mutableStateOf(0L) }
            var showSyncStatus by remember { mutableStateOf(false) }
            val currentRoute = routeHistory.current
            val selectedSnapshot = (currentRoute as? FocusRoute.Map)
                ?.let { route -> uiState.snapshots.firstOrNull { it.filePath == route.filePath } }
                ?: uiState.selectedMapFilePath?.let { filePath -> uiState.snapshots.firstOrNull { it.filePath == filePath } }
            val currentMapDocument = (currentRoute as? FocusRoute.Map)?.let { selectedSnapshot?.document }
            val screen = if (showConnection) AppScreen.Connection else currentRoute.toAppScreen()
            val showWorkspaceTabs = !showConnection && shouldShowWorkspaceTabs(currentRoute, currentMapDocument)
            val syncStatusInfo = syncStatusPanelInfo(uiState)

            fun applyRoute(route: FocusRoute, replace: Boolean = false) {
                val resolution = resolveFocusRoute(route, uiState.snapshots, uiState.unreadableMaps)
                routeHistory = if (replace || resolution.canonicalized) {
                    routeHistory.replace(resolution.route)
                } else {
                    routeHistory.push(resolution.route)
                }
                showConnection = false
                (resolution.route as? FocusRoute.Map)?.let { mapRoute ->
                    uiState.snapshots.firstOrNull { it.filePath == mapRoute.filePath }?.let(viewModel::openMap)
                }
                if (resolution.statusMessage.isNotBlank()) {
                    viewModel.showStatusMessage(resolution.statusMessage)
                }
            }

            fun goBack() {
                routeHistory = routeHistory.goBack()
                showConnection = false
            }

            fun goForward() {
                routeHistory = routeHistory.goForward()
                showConnection = false
            }

            LaunchedEffect(uiState.connectionStateLoaded, uiState.repoSettings.isComplete, uiState.tokenPresent) {
                if (!initialScreenResolved) {
                    resolveInitialAppScreen(
                        connectionStateLoaded = uiState.connectionStateLoaded,
                        repoConfigured = uiState.repoSettings.isComplete,
                        tokenPresent = uiState.tokenPresent,
                    )?.let { initialScreen ->
                        showConnection = initialScreen == AppScreen.Connection
                        if (initialScreen != AppScreen.Connection) {
                            routeHistory = routeHistory.replace(FocusRoute.Maps)
                        }
                        initialScreenResolved = true
                    }
                }
            }
            LaunchedEffect(showConnection, currentRoute, uiState.loading, uiState.snapshots, uiState.unreadableMaps) {
                if (!showConnection && !uiState.loading) {
                    val resolution = resolveFocusRoute(currentRoute, uiState.snapshots, uiState.unreadableMaps)
                    if (resolution.route != currentRoute) {
                        routeHistory = routeHistory.replace(resolution.route)
                    }
                    (resolution.route as? FocusRoute.Map)?.let { mapRoute ->
                        val snapshot = uiState.snapshots.firstOrNull { it.filePath == mapRoute.filePath }
                        if (snapshot != null && uiState.selectedMapFilePath != snapshot.filePath) {
                            viewModel.openMap(snapshot)
                        }
                    }
                    if (resolution.statusMessage.isNotBlank()) {
                        viewModel.showStatusMessage(resolution.statusMessage)
                    }
                }
            }
            LaunchedEffect(routeRequest?.version, uiState.connectionStateLoaded, uiState.loading, uiState.snapshots, uiState.unreadableMaps) {
                val request = routeRequest ?: return@LaunchedEffect
                if (
                    request.version != appliedRouteRequestVersion &&
                    uiState.connectionStateLoaded &&
                    !uiState.loading
                ) {
                    appliedRouteRequestVersion = request.version
                    applyRoute(request.route)
                }
            }
            LaunchedEffect(uiState.workspaceLoadResultVersion) {
                if (
                    shouldCloseConnectionAfterWorkspaceLoad(
                        closeConnectionAfterLoad = closeConnectionAfterLoad,
                        workspaceLoadResultVersion = uiState.workspaceLoadResultVersion,
                        workspaceLoadSucceeded = uiState.workspaceLoadSucceeded,
                    ) &&
                    showConnection
                ) {
                    applyRoute(FocusRoute.Maps, replace = true)
                }
                if (uiState.workspaceLoadResultVersion > 0 && closeConnectionAfterLoad) {
                    closeConnectionAfterLoad = false
                }
            }
            val focusedRecordForBack = (currentRoute as? FocusRoute.Map)
                ?.let { route -> selectedSnapshot?.document?.let { resolveFocusedNodeRecord(it, route.nodeId) } }
            BackHandler(
                enabled = !showConnection &&
                    currentRoute is FocusRoute.Map &&
                    (routeHistory.canGoBack || focusedRecordForBack?.parent != null),
            ) {
                if (routeHistory.canGoBack) {
                    goBack()
                } else {
                    val mapRoute = currentRoute as FocusRoute.Map
                    focusedRecordForBack?.parent?.let { parent ->
                        applyRoute(FocusRoute.Map(mapRoute.filePath, parent.uniqueIdentifier), replace = true)
                    }
                }
            }
            Scaffold(
                containerColor = palette.pageBackground,
                topBar = {
                    FocusTopBar(
                        screen = screen,
                        themePreference = uiState.uiPreferences.theme,
                        syncStatusInfo = syncStatusInfo,
                        refreshAvailable = topBarRefreshAvailable(uiState),
                        refreshEnabled = topBarRefreshEnabled(uiState),
                        refreshDescription = topBarRefreshDescription(uiState),
                        onRefreshFromGitHub = viewModel::refreshWorkspaceFromGitHub,
                        onSyncStatusRequested = { showSyncStatus = true },
                        canGoBack = routeHistory.canGoBack,
                        canGoForward = routeHistory.canGoForward,
                        onGoBack = ::goBack,
                        onGoForward = ::goForward,
                        onThemeToggle = {
                            viewModel.setThemePreference(
                                if (uiState.uiPreferences.theme == ThemePreference.Dark) {
                                    ThemePreference.Light
                                } else {
                                    ThemePreference.Dark
                                },
                            )
                        },
                        showWorkspaceTabs = showWorkspaceTabs,
                        onScreenChanged = { nextScreen ->
                            when (nextScreen) {
                                AppScreen.Connection -> showConnection = true
                                AppScreen.Maps -> applyRoute(FocusRoute.Maps)
                                AppScreen.Tasks -> applyRoute(FocusRoute.Tasks)
                                AppScreen.Map -> Unit
                            }
                        },
                    )
                },
            ) { padding ->
                Box(
                    modifier = Modifier
                        .padding(padding)
                        .fillMaxSize(),
                ) {
                    when (screen) {
                        AppScreen.Connection -> ConnectionScreen(
                            uiState = uiState,
                            onFabSideChange = viewModel::setFabSidePreference,
                            onSave = { settings, token ->
                                closeConnectionAfterLoad = true
                                viewModel.saveConnection(settings, token)
                            },
                            onLoad = {
                                closeConnectionAfterLoad = true
                                viewModel.loadWorkspace(forceRefresh = true)
                            },
                            onRevalidate = viewModel::revalidateGitHubAccess,
                            onClearSavedToken = viewModel::clearSavedToken,
                            onHardReset = {
                                closeConnectionAfterLoad = true
                                viewModel.hardResetAndReloadFromGitHub()
                            },
                        )
                        AppScreen.Maps -> MapsScreen(
                            snapshots = uiState.snapshots,
                            unreadableMaps = uiState.unreadableMaps,
                            unreadablePendingCounts = uiState.unreadablePendingCounts,
                            blockedPendingMaps = uiState.blockedPendingMaps,
                            blockedPendingCounts = uiState.blockedPendingCounts,
                            pendingConflictMaps = uiState.pendingConflictMaps,
                            pendingConflictCounts = uiState.pendingConflictCounts,
                            localMapRepairState = uiState.localMapRepairState,
                            fabSide = uiState.uiPreferences.fabSide,
                            loading = uiState.loading,
                            onOpenMap = { snapshot ->
                                applyRoute(
                                    FocusRoute.Map(
                                        filePath = snapshot.filePath,
                                        nodeId = snapshot.document.rootNode.uniqueIdentifier,
                                    ),
                                )
                            },
                            onCreateMap = viewModel::createMap,
                            onDeleteMap = viewModel::deleteMap,
                            onOpenLocalRepair = { entry -> viewModel.openLocalMapRepair(entry.filePath) },
                            onSaveLocalRepair = viewModel::saveLocalMapRepair,
                            onCloseLocalRepair = viewModel::closeLocalMapRepair,
                            onResetUnreadableMap = { entry -> viewModel.resetUnreadableMapFromGitHub(entry.filePath) },
                            onResetBlockedMap = { entry -> viewModel.resetUnreadableMapFromGitHub(entry.filePath) },
                            onOpenRawUnreadableMap = { entry -> viewModel.openUnreadableMapViewer(entry.filePath) },
                            onRetryUnreadableMap = { viewModel.loadWorkspace(forceRefresh = true) },
                            onRetryBlockedMap = { viewModel.loadWorkspace(forceRefresh = true) },
                            onDiscardBlockedMap = { entry -> viewModel.discardPendingOperationsForBlockedMap(entry.filePath) },
                            onResolveConflictMap = { entry -> viewModel.openPendingConflictResolver(entry.filePath) },
                            onRetryConflictMap = { viewModel.syncPendingNow() },
                        )
                        AppScreen.Tasks -> TasksScreen(
                            snapshots = uiState.snapshots,
                            filter = uiState.taskFilter,
                            onFilterChanged = viewModel::setTaskFilter,
                            onOpenTask = { entry ->
                                applyRoute(FocusRoute.Map(entry.filePath, entry.nodeId))
                            },
                            onSetTaskState = { entry, taskState ->
                                viewModel.setTaskState(entry.filePath, entry.nodeId, taskState)
                            },
                        )
                        AppScreen.Map -> selectedSnapshot?.let { snapshot ->
                            val mapRoute = currentRoute as? FocusRoute.Map
                            MapEditorScreen(
                                snapshot = snapshot,
                                allSnapshots = uiState.snapshots,
                                focusedNodeId = mapRoute?.nodeId ?: snapshot.document.rootNode.uniqueIdentifier,
                                fabSide = uiState.uiPreferences.fabSide,
                                attachmentUploadState = uiState.attachmentUploadState,
                                attachmentDeleteState = uiState.attachmentDeleteState,
                                onSetTaskState = viewModel::setTaskState,
                                onEditNode = viewModel::editNodeText,
                                onAddChild = viewModel::addChild,
                                onUploadAttachment = viewModel::uploadAttachment,
                                onUploadVoiceAttachment = viewModel::uploadPreparedAttachment,
                                onOpenAttachment = viewModel::openAttachmentViewer,
                                onDeleteAttachment = viewModel::deleteAttachment,
                                onDeleteNode = viewModel::deleteNode,
                                onToggleHideDone = viewModel::toggleHideDone,
                                onToggleStarred = viewModel::toggleStarred,
                                onFocusNode = { nodeId ->
                                    applyRoute(FocusRoute.Map(snapshot.filePath, nodeId))
                                },
                                onOpenRelatedNode = { entry ->
                                    applyRoute(FocusRoute.Map(entry.mapPath, entry.nodeId))
                                },
                            )
                        } ?: EmptyPanel("No map is loaded.")
                    }
                }
            }
            AttachmentViewerDialog(
                snapshots = uiState.snapshots,
                viewerState = uiState.attachmentViewerState,
                deleteState = uiState.attachmentDeleteState,
                onDismiss = viewModel::closeAttachmentViewer,
                onDeleteAttachment = viewModel::deleteAttachment,
            )
            PendingConflictResolutionDialog(
                state = uiState.conflictResolutionState,
                onDismiss = viewModel::closeConflictResolution,
                onChoice = viewModel::setConflictResolutionChoice,
                onAccept = viewModel::acceptConflictResolution,
            )
            SyncStatusDialog(
                visible = showSyncStatus,
                info = syncStatusInfo,
                onDismiss = { showSyncStatus = false },
                onRetry = { action ->
                    when (action) {
                        SyncStatusRetryAction.SyncPending -> viewModel.syncPendingNow()
                        SyncStatusRetryAction.ReloadWorkspace -> viewModel.loadWorkspace(forceRefresh = true)
                        SyncStatusRetryAction.None -> Unit
                    }
                },
            )
        }
    }
}

internal enum class AppScreen { Connection, Maps, Tasks, Map }

private fun FocusRoute.toAppScreen(): AppScreen =
    when (this) {
        FocusRoute.Maps -> AppScreen.Maps
        FocusRoute.Tasks -> AppScreen.Tasks
        is FocusRoute.Map -> AppScreen.Map
    }

internal fun resolveInitialAppScreen(
    connectionStateLoaded: Boolean,
    repoConfigured: Boolean,
    tokenPresent: Boolean,
): AppScreen? {
    if (!connectionStateLoaded) return null
    return if (repoConfigured && tokenPresent) AppScreen.Maps else AppScreen.Connection
}

internal fun shouldCloseConnectionAfterWorkspaceLoad(
    closeConnectionAfterLoad: Boolean,
    workspaceLoadResultVersion: Long,
    workspaceLoadSucceeded: Boolean,
): Boolean =
    closeConnectionAfterLoad &&
        workspaceLoadResultVersion > 0 &&
        workspaceLoadSucceeded

internal enum class SyncStatusTone {
    Idle,
    Pending,
    Success,
    Warning,
    Error,
}

internal enum class SyncStatusRetryAction {
    None,
    SyncPending,
    ReloadWorkspace,
}

internal data class SyncStatusPanelInfo(
    val state: String,
    val message: String,
    val detail: String,
    val tone: SyncStatusTone,
    val pendingCount: Int,
    val pendingText: String,
    val lastSyncText: String,
    val lastMessage: String,
    val lastError: String,
    val repository: String,
    val retryAction: SyncStatusRetryAction,
) {
    val canRetry: Boolean get() = retryAction != SyncStatusRetryAction.None
}

internal fun syncStatusPanelInfo(uiState: FocusUiState): SyncStatusPanelInfo {
    val currentState = currentSyncStatusState(uiState)
    val retryAction = syncStatusRetryAction(
        loading = uiState.loading,
        pendingCount = uiState.pendingCount,
        state = currentState,
    )
    return SyncStatusPanelInfo(
        state = currentState,
        message = uiState.statusMessage.ifBlank { uiState.syncMetadata.lastMessage ?: "Ready" },
        detail = uiState.repoSettings.describe(),
        tone = syncStatusTone(currentState),
        pendingCount = uiState.pendingCount,
        pendingText = pendingChangesText(uiState.pendingCount),
        lastSyncText = formatSyncStatusTime(uiState.syncMetadata.lastSyncAt),
        lastMessage = uiState.syncMetadata.lastMessage?.takeIf { it.isNotBlank() } ?: "None",
        lastError = uiState.syncMetadata.lastErrorSummary?.takeIf { it.isNotBlank() } ?: "None",
        repository = uiState.repoSettings.describe(),
        retryAction = retryAction,
    )
}

internal fun currentSyncStatusState(uiState: FocusUiState): String =
    when {
        uiState.loading -> "syncing"
        uiState.pendingConflictMaps.isNotEmpty() -> "conflict"
        uiState.unreadableMaps.isNotEmpty() || uiState.blockedPendingMaps.isNotEmpty() -> "blocked"
        uiState.pendingCount > 0 -> "pending"
        !uiState.syncMetadata.lastSyncState.isNullOrBlank() -> uiState.syncMetadata.lastSyncState.orEmpty()
        else -> "idle"
    }

internal fun syncStatusTone(state: String): SyncStatusTone =
    when (state.lowercase()) {
        "syncing",
        "loadingremote",
        "pending" -> SyncStatusTone.Pending
        "success" -> SyncStatusTone.Success
        "blocked",
        "conflict",
        "warning" -> SyncStatusTone.Warning
        "error" -> SyncStatusTone.Error
        else -> SyncStatusTone.Idle
    }

internal fun pendingChangesText(pendingCount: Int): String =
    "$pendingCount pending change${if (pendingCount == 1) "" else "s"}"

internal fun syncStatusRetryAction(
    loading: Boolean,
    pendingCount: Int,
    state: String,
): SyncStatusRetryAction =
    when {
        loading -> SyncStatusRetryAction.None
        pendingCount > 0 -> SyncStatusRetryAction.SyncPending
        state.lowercase() in setOf("error", "blocked", "conflict", "warning") -> SyncStatusRetryAction.ReloadWorkspace
        else -> SyncStatusRetryAction.None
    }

internal fun syncStatusRetryLabel(action: SyncStatusRetryAction): String =
    when (action) {
        SyncStatusRetryAction.SyncPending -> "Retry queued sync"
        SyncStatusRetryAction.ReloadWorkspace -> "Retry sync"
        SyncStatusRetryAction.None -> "Retry sync"
    }

internal fun syncStatusIconDescription(info: SyncStatusPanelInfo): String =
    "Open sync status, ${info.state}, ${info.message}"

internal fun formatSyncStatusTime(
    value: String?,
    zoneId: ZoneId = ZoneId.systemDefault(),
): String {
    if (value.isNullOrBlank()) return "Never synced"
    return runCatching {
        DateTimeFormatter.ofPattern("yyyy-MM-dd HH:mm")
            .withZone(zoneId)
            .format(Instant.parse(value))
    }.getOrElse { value }
}

internal fun topBarRefreshAvailable(uiState: FocusUiState): Boolean =
    uiState.repoSettings.isComplete && uiState.tokenPresent

internal fun topBarRefreshEnabled(uiState: FocusUiState): Boolean =
    topBarRefreshAvailable(uiState) && !uiState.loading

internal fun topBarRefreshDescription(uiState: FocusUiState): String =
    if (topBarRefreshEnabled(uiState)) "Refresh from GitHub" else "Refresh from GitHub unavailable"

internal fun syncStatusIconSelected(info: SyncStatusPanelInfo): Boolean =
    info.pendingCount > 0

internal fun revalidateAccessAvailable(uiState: FocusUiState): Boolean =
    uiState.repoSettings.isComplete && uiState.tokenPresent && !uiState.loading

internal fun clearSavedTokenAvailable(uiState: FocusUiState): Boolean =
    uiState.tokenPresent && !uiState.loading

internal fun hardResetAvailable(uiState: FocusUiState): Boolean =
    uiState.repoSettings.isComplete && uiState.tokenPresent && !uiState.loading

internal enum class MapEditorFabAction {
    HideDone,
    AddTask,
    AddNote,
}

internal fun fabAlignment(side: FabSidePreference): Alignment =
    when (side) {
        FabSidePreference.Left -> Alignment.BottomStart
        FabSidePreference.Right -> Alignment.BottomEnd
    }

internal fun fabSideLabel(side: FabSidePreference): String =
    when (side) {
        FabSidePreference.Left -> "Left"
        FabSidePreference.Right -> "Right"
    }

internal fun fabSideContentDescription(side: FabSidePreference, selected: Boolean): String =
    "Floating buttons on ${fabSideLabel(side).lowercase()}${if (selected) ", selected" else ""}"

internal fun mapEditorFabActionsForSide(
    side: FabSidePreference,
    showHideDone: Boolean,
    showAddActions: Boolean,
): List<MapEditorFabAction> {
    val actions = when (side) {
        FabSidePreference.Left -> listOf(
            MapEditorFabAction.AddNote,
            MapEditorFabAction.AddTask,
            MapEditorFabAction.HideDone,
        )
        FabSidePreference.Right -> listOf(
            MapEditorFabAction.HideDone,
            MapEditorFabAction.AddTask,
            MapEditorFabAction.AddNote,
        )
    }
    return actions.filter { action ->
        when (action) {
            MapEditorFabAction.HideDone -> showHideDone
            MapEditorFabAction.AddTask,
            MapEditorFabAction.AddNote -> showAddActions
        }
    }
}

@Composable
private fun FocusTopBar(
    screen: AppScreen,
    themePreference: ThemePreference,
    syncStatusInfo: SyncStatusPanelInfo,
    refreshAvailable: Boolean,
    refreshEnabled: Boolean,
    refreshDescription: String,
    onRefreshFromGitHub: () -> Unit,
    onSyncStatusRequested: () -> Unit,
    canGoBack: Boolean,
    canGoForward: Boolean,
    onGoBack: () -> Unit,
    onGoForward: () -> Unit,
    onThemeToggle: () -> Unit,
    showWorkspaceTabs: Boolean,
    onScreenChanged: (AppScreen) -> Unit,
) {
    val palette = LocalFocusPalette.current
    Surface(color = palette.pageBackground, contentColor = palette.text) {
        Column(
            modifier = Modifier
                .fillMaxWidth()
                .padding(horizontal = 16.dp, vertical = 10.dp),
            verticalArrangement = Arrangement.spacedBy(10.dp),
        ) {
            Row(
                modifier = Modifier.fillMaxWidth(),
                horizontalArrangement = Arrangement.spacedBy(12.dp),
                verticalAlignment = Alignment.CenterVertically,
            ) {
                Column(modifier = Modifier.weight(1f)) {
                    Text(
                        text = "Focus",
                        style = MaterialTheme.typography.headlineSmall,
                        fontWeight = FontWeight.Bold,
                        color = palette.text,
                    )
                    Text(
                        text = if (syncStatusInfo.pendingCount > 0) syncStatusInfo.pendingText else syncStatusInfo.message,
                        style = MaterialTheme.typography.bodySmall,
                        color = if (syncStatusInfo.pendingCount > 0) palette.accentStrong else palette.muted,
                        maxLines = 1,
                        overflow = TextOverflow.Ellipsis,
                    )
                }
                FocusIconButton(
                    imageVector = Icons.AutoMirrored.Filled.ArrowBack,
                    contentDescription = nativeRouteBackLabel(canGoBack),
                    onClick = onGoBack,
                    enabled = canGoBack,
                )
                FocusIconButton(
                    imageVector = Icons.AutoMirrored.Filled.ArrowForward,
                    contentDescription = nativeRouteForwardLabel(canGoForward),
                    onClick = onGoForward,
                    enabled = canGoForward,
                )
                if (refreshAvailable) {
                    FocusIconButton(
                        imageVector = Icons.Filled.Refresh,
                        contentDescription = refreshDescription,
                        onClick = onRefreshFromGitHub,
                        enabled = refreshEnabled,
                    )
                }
                FocusIconButton(
                    imageVector = Icons.Filled.Sync,
                    contentDescription = syncStatusIconDescription(syncStatusInfo),
                    onClick = onSyncStatusRequested,
                    selected = syncStatusIconSelected(syncStatusInfo),
                )
                FocusIconButton(
                    imageVector = if (themePreference == ThemePreference.Dark) Icons.Filled.LightMode else Icons.Filled.DarkMode,
                    contentDescription = if (themePreference == ThemePreference.Dark) "Switch to light theme" else "Switch to dark theme",
                    onClick = onThemeToggle,
                )
                FocusIconButton(
                    imageVector = Icons.Filled.Settings,
                    contentDescription = "Open connection settings",
                    onClick = { onScreenChanged(AppScreen.Connection) },
                    selected = screen == AppScreen.Connection,
                )
            }

            if (showWorkspaceTabs) {
                Row(
                    modifier = Modifier.horizontalScroll(rememberScrollState()),
                    horizontalArrangement = Arrangement.spacedBy(8.dp),
                ) {
                    FocusNavTab(
                        label = "Maps",
                        imageVector = Icons.Filled.Map,
                        selected = screen == AppScreen.Maps || screen == AppScreen.Map,
                        onClick = { onScreenChanged(AppScreen.Maps) },
                    )
                    FocusNavTab(
                        label = "Tasks",
                        imageVector = Icons.Filled.AddTask,
                        selected = screen == AppScreen.Tasks,
                        onClick = { onScreenChanged(AppScreen.Tasks) },
                    )
                }
            }
        }
    }
}

@Composable
private fun FocusNavTab(
    label: String,
    imageVector: ImageVector,
    selected: Boolean,
    onClick: () -> Unit,
) {
    val palette = LocalFocusPalette.current
    val shape = RoundedCornerShape(12.dp)
    Row(
        modifier = Modifier
            .clip(shape)
            .background(if (selected) palette.accentSoft else palette.panelMuted)
            .border(
                BorderStroke(1.dp, if (selected) palette.accentBorderStrong else palette.border),
                shape,
            )
            .clickable(onClick = onClick)
            .heightIn(min = 44.dp)
            .padding(horizontal = 14.dp, vertical = 8.dp),
        verticalAlignment = Alignment.CenterVertically,
        horizontalArrangement = Arrangement.spacedBy(8.dp),
    ) {
        Icon(
            imageVector = imageVector,
            contentDescription = null,
            tint = if (selected) palette.accentStrong else palette.muted,
            modifier = Modifier.size(18.dp),
        )
        Text(
            text = label,
            style = MaterialTheme.typography.labelLarge,
            fontWeight = FontWeight.SemiBold,
            color = if (selected) palette.accentStrong else palette.text,
        )
    }
}

@Composable
private fun ConnectionScreen(
    uiState: FocusUiState,
    onFabSideChange: (FabSidePreference) -> Unit,
    onSave: (RepoSettings, String) -> Unit,
    onLoad: () -> Unit,
    onRevalidate: () -> Unit,
    onClearSavedToken: () -> Unit,
    onHardReset: () -> Unit,
) {
    var owner by remember(uiState.repoSettings.repoOwner) { mutableStateOf(uiState.repoSettings.repoOwner) }
    var repo by remember(uiState.repoSettings.repoName) { mutableStateOf(uiState.repoSettings.repoName) }
    var branch by remember(uiState.repoSettings.repoBranch) { mutableStateOf(uiState.repoSettings.repoBranch) }
    var path by remember(uiState.repoSettings.repoPath) { mutableStateOf(uiState.repoSettings.repoPath) }
    var token by remember { mutableStateOf("") }
    val palette = LocalFocusPalette.current

    Box(
        modifier = Modifier
            .fillMaxSize()
            .verticalScroll(rememberScrollState())
            .padding(16.dp),
        contentAlignment = Alignment.TopCenter,
    ) {
        FocusCard(modifier = Modifier.fillMaxWidth().widthIn(max = 720.dp)) {
            Column(
                modifier = Modifier.padding(18.dp),
                verticalArrangement = Arrangement.spacedBy(12.dp),
            ) {
                Text("GitHub connection", style = MaterialTheme.typography.headlineSmall, fontWeight = FontWeight.SemiBold)
                Text(
                    text = uiState.repoSettings.describe(),
                    style = MaterialTheme.typography.bodySmall,
                    color = palette.muted,
                    maxLines = 1,
                    overflow = TextOverflow.Ellipsis,
                )
                FocusTextField(value = owner, onValueChange = { owner = it }, label = "Repository owner")
                FocusTextField(value = repo, onValueChange = { repo = it }, label = "Repository name")
                FocusTextField(value = branch, onValueChange = { branch = it }, label = "Branch")
                FocusTextField(value = path, onValueChange = { path = it }, label = "FocusMaps folder path")
                FocusTextField(
                    value = token,
                    onValueChange = { token = it },
                    label = if (uiState.tokenPresent) "Personal access token (saved)" else "Personal access token",
                )
                FabSidePreferenceSelector(
                    selectedSide = uiState.uiPreferences.fabSide,
                    enabled = !uiState.loading,
                    onSideChange = onFabSideChange,
                )
                PrimaryActionButton(
                    label = "Save, validate, and load",
                    imageVector = Icons.Filled.Settings,
                    enabled = !uiState.loading,
                    onClick = {
                        onSave(
                            RepoSettings(
                                repoOwner = owner,
                                repoName = repo,
                                repoBranch = branch,
                                repoPath = path,
                            ),
                            token,
                        )
                    },
                    modifier = Modifier.fillMaxWidth(),
                )
                SecondaryActionButton(
                    label = "Revalidate access",
                    imageVector = Icons.Filled.Sync,
                    enabled = revalidateAccessAvailable(uiState),
                    onClick = onRevalidate,
                    modifier = Modifier.fillMaxWidth(),
                )
                DangerActionButton(
                    label = "Clear saved token",
                    imageVector = Icons.Filled.Delete,
                    enabled = clearSavedTokenAvailable(uiState),
                    onClick = onClearSavedToken,
                    modifier = Modifier.fillMaxWidth(),
                )
                SecondaryActionButton(
                    label = "Reload workspace",
                    imageVector = Icons.Filled.Refresh,
                    enabled = !uiState.loading && uiState.tokenPresent,
                    onClick = onLoad,
                    modifier = Modifier.fillMaxWidth(),
                )
                FocusPill(
                    label = uiState.statusMessage.ifBlank { "Ready" },
                    toneColor = if (uiState.loading) palette.warning else palette.accent,
                    selected = true,
                )
                Text(
                    "The native app stores the PAT in Android Keystore-backed encrypted preferences and caches maps locally for offline reading.",
                    style = MaterialTheme.typography.bodySmall,
                    color = palette.muted,
                )
                Column(verticalArrangement = Arrangement.spacedBy(8.dp)) {
                    Text(
                        "Danger zone",
                        style = MaterialTheme.typography.titleSmall,
                        color = palette.text,
                        fontWeight = FontWeight.SemiBold,
                    )
                    Text(
                        "Hard reset discards queued local changes and cached map data for this repository, then reloads everything from GitHub.",
                        style = MaterialTheme.typography.bodySmall,
                        color = palette.muted,
                    )
                    DangerActionButton(
                        label = "Hard reset - discard local changes and reload from GitHub",
                        imageVector = Icons.Filled.Delete,
                        enabled = hardResetAvailable(uiState),
                        onClick = onHardReset,
                        modifier = Modifier.fillMaxWidth(),
                    )
                }
            }
        }
    }
}

@Composable
private fun FabSidePreferenceSelector(
    selectedSide: FabSidePreference,
    enabled: Boolean,
    onSideChange: (FabSidePreference) -> Unit,
) {
    val palette = LocalFocusPalette.current
    Column(verticalArrangement = Arrangement.spacedBy(8.dp)) {
        Text(
            text = "Floating buttons",
            style = MaterialTheme.typography.labelLarge,
            color = palette.text,
            fontWeight = FontWeight.SemiBold,
        )
        Row(
            modifier = Modifier.fillMaxWidth(),
            horizontalArrangement = Arrangement.spacedBy(8.dp),
        ) {
            listOf(FabSidePreference.Left, FabSidePreference.Right).forEach { side ->
                val selected = selectedSide == side
                val modifier = Modifier
                    .weight(1f)
                    .semantics {
                        contentDescription = fabSideContentDescription(side, selected)
                        stateDescription = if (selected) "Selected" else "Not selected"
                    }
                if (selected) {
                    Button(
                        onClick = { onSideChange(side) },
                        enabled = enabled,
                        modifier = modifier,
                        colors = ButtonDefaults.buttonColors(
                            containerColor = palette.accent,
                            contentColor = Color.White,
                        ),
                    ) {
                        Text(fabSideLabel(side))
                    }
                } else {
                    OutlinedButton(
                        onClick = { onSideChange(side) },
                        enabled = enabled,
                        modifier = modifier,
                        colors = ButtonDefaults.outlinedButtonColors(
                            contentColor = palette.accentStrong,
                        ),
                    ) {
                        Text(fabSideLabel(side))
                    }
                }
            }
        }
    }
}

@Composable
private fun MapsScreen(
    snapshots: List<MapSnapshot>,
    unreadableMaps: List<UnreadableMapEntry>,
    unreadablePendingCounts: Map<String, Int>,
    blockedPendingMaps: List<BlockedPendingMapEntry>,
    blockedPendingCounts: Map<String, Int>,
    pendingConflictMaps: List<PendingConflictMapEntry>,
    pendingConflictCounts: Map<String, Int>,
    localMapRepairState: LocalMapRepairUiState,
    fabSide: FabSidePreference,
    loading: Boolean,
    onOpenMap: (MapSnapshot) -> Unit,
    onCreateMap: (String) -> Unit,
    onDeleteMap: (String) -> Unit,
    onOpenLocalRepair: (UnreadableMapEntry) -> Unit,
    onSaveLocalRepair: (String, String) -> Unit,
    onCloseLocalRepair: () -> Unit,
    onResetUnreadableMap: (UnreadableMapEntry) -> Unit,
    onResetBlockedMap: (BlockedPendingMapEntry) -> Unit,
    onOpenRawUnreadableMap: (UnreadableMapEntry) -> Unit,
    onRetryUnreadableMap: (UnreadableMapEntry) -> Unit,
    onRetryBlockedMap: (BlockedPendingMapEntry) -> Unit,
    onDiscardBlockedMap: (BlockedPendingMapEntry) -> Unit,
    onResolveConflictMap: (PendingConflictMapEntry) -> Unit,
    onRetryConflictMap: (PendingConflictMapEntry) -> Unit,
) {
    var showCreateDialog by remember { mutableStateOf(false) }
    var deleteCandidate by remember { mutableStateOf<MapSnapshot?>(null) }
    val summaries = snapshots.map(MapQueries::buildMapSummary).sortedByDescending { it.updatedAt }

    Box(modifier = Modifier.fillMaxSize()) {
        LazyColumn(
            modifier = Modifier.fillMaxSize(),
            contentPadding = PaddingValues(start = 16.dp, top = 16.dp, end = 16.dp, bottom = 96.dp),
            verticalArrangement = Arrangement.spacedBy(10.dp),
        ) {
            if (unreadableMaps.isNotEmpty() || blockedPendingMaps.isNotEmpty() || pendingConflictMaps.isNotEmpty()) {
                item(key = "unreadable-map-recovery") {
                    UnreadableMapRecoverySection(
                        unreadableMaps = unreadableMaps,
                        unreadablePendingCounts = unreadablePendingCounts,
                        blockedPendingMaps = blockedPendingMaps,
                        blockedPendingCounts = blockedPendingCounts,
                        pendingConflictMaps = pendingConflictMaps,
                        pendingConflictCounts = pendingConflictCounts,
                        localMapRepairState = localMapRepairState,
                        loading = loading,
                        onRepairLocal = onOpenLocalRepair,
                        onSaveLocalRepair = onSaveLocalRepair,
                        onCloseLocalRepair = onCloseLocalRepair,
                        onResetToGitHub = onResetUnreadableMap,
                        onResetBlockedToGitHub = onResetBlockedMap,
                        onViewRaw = onOpenRawUnreadableMap,
                        onRetry = onRetryUnreadableMap,
                        onRetryBlocked = onRetryBlockedMap,
                        onDiscardBlocked = onDiscardBlockedMap,
                        onResolveConflict = onResolveConflictMap,
                        onRetryConflict = onRetryConflictMap,
                    )
                }
            }
            if (snapshots.isEmpty() && unreadableMaps.isEmpty() && blockedPendingMaps.isEmpty() && pendingConflictMaps.isEmpty()) {
                item {
                    EmptyCard("No maps are loaded.")
                }
            }
            items(summaries, key = { it.filePath }) { summary ->
                val snapshot = snapshots.first { it.filePath == summary.filePath }
                MapSummaryCard(
                    snapshot = snapshot,
                    loading = loading,
                    onOpenMap = { onOpenMap(snapshot) },
                    onDeleteMap = { deleteCandidate = snapshot },
                )
            }
        }
        FocusFab(
            imageVector = Icons.Filled.Add,
            contentDescription = "New map",
            onClick = { showCreateDialog = true },
            modifier = Modifier
                .align(fabAlignment(fabSide))
                .padding(20.dp),
        )
    }

    if (showCreateDialog) {
        CreateMapDialog(
            loading = loading,
            onDismiss = { showCreateDialog = false },
            onCreate = { name ->
                showCreateDialog = false
                onCreateMap(name)
            },
        )
    }
    deleteCandidate?.let { snapshot ->
        DeleteMapDialog(
            snapshot = snapshot,
            onDismiss = { deleteCandidate = null },
            onConfirm = {
                deleteCandidate = null
                onDeleteMap(snapshot.filePath)
            },
        )
    }
}

@Composable
private fun UnreadableMapRecoverySection(
    unreadableMaps: List<UnreadableMapEntry>,
    unreadablePendingCounts: Map<String, Int>,
    blockedPendingMaps: List<BlockedPendingMapEntry>,
    blockedPendingCounts: Map<String, Int>,
    pendingConflictMaps: List<PendingConflictMapEntry>,
    pendingConflictCounts: Map<String, Int>,
    localMapRepairState: LocalMapRepairUiState,
    loading: Boolean,
    onRepairLocal: (UnreadableMapEntry) -> Unit,
    onSaveLocalRepair: (String, String) -> Unit,
    onCloseLocalRepair: () -> Unit,
    onResetToGitHub: (UnreadableMapEntry) -> Unit,
    onResetBlockedToGitHub: (BlockedPendingMapEntry) -> Unit,
    onViewRaw: (UnreadableMapEntry) -> Unit,
    onRetry: (UnreadableMapEntry) -> Unit,
    onRetryBlocked: (BlockedPendingMapEntry) -> Unit,
    onDiscardBlocked: (BlockedPendingMapEntry) -> Unit,
    onResolveConflict: (PendingConflictMapEntry) -> Unit,
    onRetryConflict: (PendingConflictMapEntry) -> Unit,
) {
    val palette = LocalFocusPalette.current
    val context = LocalContext.current
    val coroutineScope = rememberCoroutineScope()
    var pendingDownload by remember { mutableStateOf<RawUnreadableMapSaveRequest?>(null) }
    var downloadMessage by remember { mutableStateOf("") }
    var downloadError by remember { mutableStateOf("") }
    val downloadLauncher = rememberLauncherForActivityResult(ActivityResultContracts.StartActivityForResult()) { result ->
        val request = pendingDownload
        pendingDownload = null
        if (result.resultCode != Activity.RESULT_OK) {
            return@rememberLauncherForActivityResult
        }
        val uri = result.data?.data
        if (request == null || uri == null) {
            downloadMessage = ""
            downloadError = "Could not save raw map: no file was selected."
            return@rememberLauncherForActivityResult
        }
        coroutineScope.launch {
            val writeResult = withContext(Dispatchers.IO) {
                runCatching {
                    context.contentResolver.openOutputStream(uri)?.use { output ->
                        output.write(request.bytes)
                    } ?: error("Could not open the selected file.")
                }
            }
            writeResult
                .onSuccess {
                    downloadError = ""
                    downloadMessage = "Saved ${request.fileName}."
                }
                .onFailure { error ->
                    downloadMessage = ""
                    downloadError = "Could not save raw map: ${error.message ?: "unknown error"}"
                }
        }
    }
    FocusCard(modifier = Modifier.fillMaxWidth()) {
        Column(
            modifier = Modifier.padding(14.dp),
            verticalArrangement = Arrangement.spacedBy(12.dp),
        ) {
            Text(
                text = "Recovery",
                style = MaterialTheme.typography.labelSmall,
                color = palette.accentStrong,
                fontWeight = FontWeight.SemiBold,
            )
            Text(
                text = "Needs repair",
                style = MaterialTheme.typography.titleMedium.copy(fontWeight = FontWeight.SemiBold),
                color = palette.text,
            )
            Text(
                text = "Readable maps remain available. Repair locally, reset this device to GitHub, or retry after another fix.",
                style = MaterialTheme.typography.bodySmall,
                color = palette.muted,
            )
            if (downloadMessage.isNotBlank()) {
                Text(downloadMessage, color = palette.success, style = MaterialTheme.typography.bodySmall)
            }
            if (downloadError.isNotBlank()) {
                Text(downloadError, color = palette.danger, style = MaterialTheme.typography.bodySmall)
            }
            unreadableMaps.forEach { item ->
                UnreadableMapRecoveryRow(
                    item = item,
                    pendingCount = unreadablePendingCounts[item.filePath] ?: 0,
                    loading = loading,
                    onRepairLocal = { onRepairLocal(item) },
                    onResetToGitHub = { onResetToGitHub(item) },
                    onViewRaw = { onViewRaw(item) },
                    onDownloadRaw = {
                        downloadMessage = ""
                        downloadError = ""
                        val request = RawUnreadableMapSaveRequest(
                            fileName = UnreadableMaps.rawFileName(item),
                            bytes = UnreadableMaps.rawBytes(item),
                        )
                        pendingDownload = request
                        val intent = Intent(Intent.ACTION_CREATE_DOCUMENT).apply {
                            addCategory(Intent.CATEGORY_OPENABLE)
                            type = UnreadableMaps.RawMapMediaType
                            putExtra(Intent.EXTRA_TITLE, request.fileName)
                        }
                        runCatching {
                            downloadLauncher.launch(intent)
                        }.onFailure { error ->
                            pendingDownload = null
                            downloadError = "Could not open save dialog: ${error.message ?: "unknown error"}"
                        }
                    },
                    onRetry = { onRetry(item) },
                )
            }
            blockedPendingMaps.forEach { item ->
                BlockedPendingMapRecoveryRow(
                    item = item,
                    pendingCount = blockedPendingCounts[item.filePath] ?: 0,
                    loading = loading,
                    onRepairLocal = {
                        onRepairLocal(BlockedPendingMaps.toRepairEntry(item))
                    },
                    onResetToGitHub = { onResetBlockedToGitHub(item) },
                    onRetry = { onRetryBlocked(item) },
                    onDiscard = { onDiscardBlocked(item) },
                )
            }
            pendingConflictMaps.forEach { item ->
                PendingConflictMapRecoveryRow(
                    item = item,
                    pendingCount = pendingConflictCounts[item.filePath] ?: 0,
                    loading = loading,
                    onResolve = { onResolveConflict(item) },
                    onRetry = { onRetryConflict(item) },
                )
            }
        }
    }

    val repairEntry = localMapRepairState.entry
        ?.takeIf { it.filePath == localMapRepairState.targetPath }
        ?: unreadableMaps.firstOrNull { it.filePath == localMapRepairState.targetPath }
    if (repairEntry != null) {
        LocalMapRepairDialog(
            entry = repairEntry,
            pendingCount = unreadablePendingCounts[repairEntry.filePath]
                ?: blockedPendingCounts[repairEntry.filePath]
                ?: 0,
            repairState = localMapRepairState.forTarget(repairEntry.filePath),
            onDismiss = onCloseLocalRepair,
            onSave = { rawJson -> onSaveLocalRepair(repairEntry.filePath, rawJson) },
        )
    }
}

@Composable
private fun UnreadableMapRecoveryRow(
    item: UnreadableMapEntry,
    pendingCount: Int,
    loading: Boolean,
    onRepairLocal: () -> Unit,
    onResetToGitHub: () -> Unit,
    onViewRaw: () -> Unit,
    onDownloadRaw: () -> Unit,
    onRetry: () -> Unit,
) {
    val palette = LocalFocusPalette.current
    Column(
        modifier = Modifier
            .fillMaxWidth()
            .clip(RoundedCornerShape(12.dp))
            .border(BorderStroke(1.dp, palette.border), RoundedCornerShape(12.dp))
            .background(palette.inputBackground)
            .padding(12.dp),
        verticalArrangement = Arrangement.spacedBy(10.dp),
    ) {
        Column(
            verticalArrangement = Arrangement.spacedBy(5.dp),
        ) {
            Text(
                text = unreadableMapTitle(item),
                style = MaterialTheme.typography.titleSmall.copy(fontWeight = FontWeight.SemiBold),
                color = palette.text,
                maxLines = 2,
                overflow = TextOverflow.Ellipsis,
            )
            Text(
                text = "${UnreadableMaps.reasonLabel(item.reason)}. ${item.message}",
                style = MaterialTheme.typography.bodySmall,
                color = palette.muted,
            )
            Text(
                text = item.filePath,
                style = MaterialTheme.typography.bodySmall.copy(fontFamily = FontFamily.Monospace),
                color = palette.muted,
                maxLines = 1,
                overflow = TextOverflow.Ellipsis,
            )
            Text(
                text = unreadablePendingText(pendingCount),
                style = MaterialTheme.typography.bodySmall,
                color = if (pendingCount > 0) palette.warning else palette.muted,
                fontWeight = if (pendingCount > 0) FontWeight.SemiBold else FontWeight.Normal,
            )
        }
        Row(
            modifier = Modifier.horizontalScroll(rememberScrollState()),
            horizontalArrangement = Arrangement.spacedBy(8.dp),
            verticalAlignment = Alignment.CenterVertically,
        ) {
            OutlinedButton(
                onClick = onRepairLocal,
                enabled = !loading,
                modifier = Modifier.semantics {
                    contentDescription = LocalMapRepairs.repairActionLabel(item)
                },
                contentPadding = PaddingValues(horizontal = 10.dp, vertical = 6.dp),
            ) {
                Icon(
                    imageVector = Icons.Filled.Settings,
                    contentDescription = null,
                    modifier = Modifier.size(16.dp),
                )
                Spacer(modifier = Modifier.width(6.dp))
                Text("Repair locally")
            }
            OutlinedButton(
                onClick = onResetToGitHub,
                enabled = !loading,
                modifier = Modifier.semantics {
                    contentDescription = UnreadableMaps.resetToGitHubLabel(item)
                },
                contentPadding = PaddingValues(horizontal = 10.dp, vertical = 6.dp),
            ) {
                Icon(
                    imageVector = Icons.Filled.CloudDownload,
                    contentDescription = null,
                    modifier = Modifier.size(16.dp),
                )
                Spacer(modifier = Modifier.width(6.dp))
                Text("Reset to GitHub")
            }
            OutlinedButton(
                onClick = onViewRaw,
                enabled = !loading,
                modifier = Modifier.semantics {
                    contentDescription = UnreadableMaps.viewRawLabel(item)
                },
                contentPadding = PaddingValues(horizontal = 10.dp, vertical = 6.dp),
            ) {
                Icon(
                    imageVector = Icons.Filled.Visibility,
                    contentDescription = null,
                    modifier = Modifier.size(16.dp),
                )
                Spacer(modifier = Modifier.width(6.dp))
                Text("View raw file")
            }
            OutlinedButton(
                onClick = onDownloadRaw,
                enabled = !loading,
                modifier = Modifier.semantics {
                    contentDescription = UnreadableMaps.downloadRawLabel(item)
                },
                contentPadding = PaddingValues(horizontal = 10.dp, vertical = 6.dp),
            ) {
                Icon(
                    imageVector = Icons.Filled.Download,
                    contentDescription = null,
                    modifier = Modifier.size(16.dp),
                )
                Spacer(modifier = Modifier.width(6.dp))
                Text("Download raw file")
            }
            OutlinedButton(
                onClick = onRetry,
                enabled = !loading,
                modifier = Modifier.semantics {
                    contentDescription = UnreadableMaps.retryLabel(item)
                },
                contentPadding = PaddingValues(horizontal = 10.dp, vertical = 6.dp),
            ) {
                Icon(
                    imageVector = Icons.Filled.Refresh,
                    contentDescription = null,
                    modifier = Modifier.size(16.dp),
                )
                Spacer(modifier = Modifier.width(6.dp))
                Text("Retry")
            }
        }
    }
}

@Composable
private fun BlockedPendingMapRecoveryRow(
    item: BlockedPendingMapEntry,
    pendingCount: Int,
    loading: Boolean,
    onRepairLocal: () -> Unit,
    onResetToGitHub: () -> Unit,
    onRetry: () -> Unit,
    onDiscard: () -> Unit,
) {
    val palette = LocalFocusPalette.current
    Column(
        modifier = Modifier
            .fillMaxWidth()
            .clip(RoundedCornerShape(12.dp))
            .border(BorderStroke(1.dp, palette.border), RoundedCornerShape(12.dp))
            .background(palette.inputBackground)
            .padding(12.dp),
        verticalArrangement = Arrangement.spacedBy(10.dp),
    ) {
        Column(
            verticalArrangement = Arrangement.spacedBy(5.dp),
        ) {
            Text(
                text = item.mapName.ifBlank { item.fileName.ifBlank { item.filePath } },
                style = MaterialTheme.typography.titleSmall.copy(fontWeight = FontWeight.SemiBold),
                color = palette.text,
                maxLines = 2,
                overflow = TextOverflow.Ellipsis,
            )
            Text(
                text = item.message,
                style = MaterialTheme.typography.bodySmall,
                color = palette.muted,
            )
            Text(
                text = item.filePath,
                style = MaterialTheme.typography.bodySmall.copy(fontFamily = FontFamily.Monospace),
                color = palette.muted,
                maxLines = 1,
                overflow = TextOverflow.Ellipsis,
            )
            Text(
                text = unreadablePendingText(pendingCount),
                style = MaterialTheme.typography.bodySmall,
                color = palette.warning,
                fontWeight = FontWeight.SemiBold,
            )
        }
        Row(
            modifier = Modifier.horizontalScroll(rememberScrollState()),
            horizontalArrangement = Arrangement.spacedBy(8.dp),
            verticalAlignment = Alignment.CenterVertically,
        ) {
            OutlinedButton(
                onClick = onRepairLocal,
                enabled = !loading,
                modifier = Modifier.semantics {
                    contentDescription = BlockedPendingMaps.repairActionLabel(item)
                },
                contentPadding = PaddingValues(horizontal = 10.dp, vertical = 6.dp),
            ) {
                Icon(
                    imageVector = Icons.Filled.Settings,
                    contentDescription = null,
                    modifier = Modifier.size(16.dp),
                )
                Spacer(modifier = Modifier.width(6.dp))
                Text("Repair locally")
            }
            OutlinedButton(
                onClick = onResetToGitHub,
                enabled = !loading,
                modifier = Modifier.semantics {
                    contentDescription = BlockedPendingMaps.resetToGitHubLabel(item)
                },
                contentPadding = PaddingValues(horizontal = 10.dp, vertical = 6.dp),
            ) {
                Icon(
                    imageVector = Icons.Filled.CloudDownload,
                    contentDescription = null,
                    modifier = Modifier.size(16.dp),
                )
                Spacer(modifier = Modifier.width(6.dp))
                Text("Reset to GitHub")
            }
            OutlinedButton(
                onClick = onRetry,
                enabled = !loading,
                modifier = Modifier.semantics {
                    contentDescription = BlockedPendingMaps.retryLabel(item)
                },
                contentPadding = PaddingValues(horizontal = 10.dp, vertical = 6.dp),
            ) {
                Icon(
                    imageVector = Icons.Filled.Refresh,
                    contentDescription = null,
                    modifier = Modifier.size(16.dp),
                )
                Spacer(modifier = Modifier.width(6.dp))
                Text("Retry")
            }
            OutlinedButton(
                onClick = onDiscard,
                enabled = !loading && pendingCount > 0,
                modifier = Modifier.semantics {
                    contentDescription = BlockedPendingMaps.discardLabel(item)
                },
                contentPadding = PaddingValues(horizontal = 10.dp, vertical = 6.dp),
                colors = ButtonDefaults.outlinedButtonColors(contentColor = palette.danger),
            ) {
                Icon(
                    imageVector = Icons.Filled.Delete,
                    contentDescription = null,
                    modifier = Modifier.size(16.dp),
                )
                Spacer(modifier = Modifier.width(6.dp))
                Text("Discard queued changes")
            }
        }
    }
}

@Composable
private fun PendingConflictMapRecoveryRow(
    item: PendingConflictMapEntry,
    pendingCount: Int,
    loading: Boolean,
    onResolve: () -> Unit,
    onRetry: () -> Unit,
) {
    val palette = LocalFocusPalette.current
    Column(
        modifier = Modifier
            .fillMaxWidth()
            .clip(RoundedCornerShape(12.dp))
            .border(BorderStroke(1.dp, palette.warning.copy(alpha = 0.45f)), RoundedCornerShape(12.dp))
            .background(palette.inputBackground)
            .padding(12.dp),
        verticalArrangement = Arrangement.spacedBy(10.dp),
    ) {
        Column(
            verticalArrangement = Arrangement.spacedBy(5.dp),
        ) {
            Text(
                text = item.mapName.ifBlank { item.fileName.ifBlank { item.filePath } },
                style = MaterialTheme.typography.titleSmall.copy(fontWeight = FontWeight.SemiBold),
                color = palette.text,
                maxLines = 2,
                overflow = TextOverflow.Ellipsis,
            )
            Text(
                text = item.message,
                style = MaterialTheme.typography.bodySmall,
                color = palette.muted,
            )
            Text(
                text = item.filePath,
                style = MaterialTheme.typography.bodySmall.copy(fontFamily = FontFamily.Monospace),
                color = palette.muted,
                maxLines = 1,
                overflow = TextOverflow.Ellipsis,
            )
            Text(
                text = PendingConflicts.pendingText(pendingCount),
                style = MaterialTheme.typography.bodySmall,
                color = palette.warning,
                fontWeight = FontWeight.SemiBold,
            )
        }
        Row(
            modifier = Modifier.horizontalScroll(rememberScrollState()),
            horizontalArrangement = Arrangement.spacedBy(8.dp),
            verticalAlignment = Alignment.CenterVertically,
        ) {
            OutlinedButton(
                onClick = onResolve,
                enabled = !loading && pendingCount > 0,
                modifier = Modifier.semantics {
                    contentDescription = PendingConflicts.resolveLabel(item)
                },
                contentPadding = PaddingValues(horizontal = 10.dp, vertical = 6.dp),
            ) {
                Icon(
                    imageVector = Icons.Filled.Settings,
                    contentDescription = null,
                    modifier = Modifier.size(16.dp),
                )
                Spacer(modifier = Modifier.width(6.dp))
                Text("Resolve conflict")
            }
            OutlinedButton(
                onClick = onRetry,
                enabled = !loading && pendingCount > 0,
                modifier = Modifier.semantics {
                    contentDescription = PendingConflicts.retryLabel(item)
                },
                contentPadding = PaddingValues(horizontal = 10.dp, vertical = 6.dp),
            ) {
                Icon(
                    imageVector = Icons.Filled.Refresh,
                    contentDescription = null,
                    modifier = Modifier.size(16.dp),
                )
                Spacer(modifier = Modifier.width(6.dp))
                Text("Retry")
            }
        }
    }
}

@Composable
private fun LocalMapRepairDialog(
    entry: UnreadableMapEntry,
    pendingCount: Int,
    repairState: LocalMapRepairUiState,
    onDismiss: () -> Unit,
    onSave: (String) -> Unit,
) {
    val context = LocalContext.current
    val coroutineScope = rememberCoroutineScope()
    val palette = LocalFocusPalette.current
    var draftText by remember(repairState.targetPath, repairState.draftText) { mutableStateOf(repairState.draftText) }
    var pendingDownload by remember(repairState.targetPath) { mutableStateOf<RawUnreadableMapSaveRequest?>(null) }
    var downloadMessage by remember(repairState.targetPath) { mutableStateOf("") }
    var downloadError by remember(repairState.targetPath) { mutableStateOf("") }
    val downloadLauncher = rememberLauncherForActivityResult(ActivityResultContracts.StartActivityForResult()) { result ->
        val request = pendingDownload
        pendingDownload = null
        if (result.resultCode != Activity.RESULT_OK) {
            return@rememberLauncherForActivityResult
        }
        val uri = result.data?.data
        if (request == null || uri == null) {
            downloadMessage = ""
            downloadError = "Could not save repair draft: no file was selected."
            return@rememberLauncherForActivityResult
        }
        coroutineScope.launch {
            val writeResult = withContext(Dispatchers.IO) {
                runCatching {
                    context.contentResolver.openOutputStream(uri)?.use { output ->
                        output.write(request.bytes)
                    } ?: error("Could not open the selected file.")
                }
            }
            writeResult
                .onSuccess {
                    downloadError = ""
                    downloadMessage = "Saved ${request.fileName}."
                }
                .onFailure { error ->
                    downloadMessage = ""
                    downloadError = "Could not save repair draft: ${error.message ?: "unknown error"}"
                }
        }
    }

    FocusDialog(
        title = "Repair ${unreadableMapTitle(entry)}",
        onDismiss = onDismiss,
        confirmButton = {
            Row(horizontalArrangement = Arrangement.spacedBy(8.dp)) {
                TextButton(
                    enabled = !repairState.saving,
                    onClick = {
                        downloadMessage = ""
                        downloadError = ""
                        val request = RawUnreadableMapSaveRequest(
                            fileName = LocalMapRepairs.repairFileName(entry),
                            bytes = draftText.toByteArray(Charsets.UTF_8),
                        )
                        pendingDownload = request
                        val intent = Intent(Intent.ACTION_CREATE_DOCUMENT).apply {
                            addCategory(Intent.CATEGORY_OPENABLE)
                            type = UnreadableMaps.RawMapMediaType
                            putExtra(Intent.EXTRA_TITLE, request.fileName)
                        }
                        runCatching {
                            downloadLauncher.launch(intent)
                        }.onFailure { error ->
                            pendingDownload = null
                            downloadError = "Could not open save dialog: ${error.message ?: "unknown error"}"
                        }
                    },
                    modifier = Modifier.semantics {
                        contentDescription = LocalMapRepairs.downloadRepairDraftLabel(entry)
                    },
                    colors = focusTextButtonColors(),
                ) {
                    Text("Download")
                }
                TextButton(
                    enabled = !repairState.saving && draftText.trim().isNotEmpty(),
                    onClick = { onSave(draftText) },
                    colors = focusTextButtonColors(),
                ) {
                    Text(if (repairState.saving) "Saving..." else "Save local copy")
                }
            }
        },
        dismissButton = {
            TextButton(onClick = onDismiss, enabled = !repairState.saving, colors = focusTextButtonColors()) {
                Text("Cancel")
            }
        },
    ) {
        Column(verticalArrangement = Arrangement.spacedBy(10.dp)) {
            Text(
                text = "Local repair",
                style = MaterialTheme.typography.labelSmall,
                color = palette.accentStrong,
                fontWeight = FontWeight.SemiBold,
            )
            Text(
                text = entry.filePath,
                style = MaterialTheme.typography.bodySmall.copy(fontFamily = FontFamily.Monospace),
                color = palette.muted,
                maxLines = 1,
                overflow = TextOverflow.Ellipsis,
            )
            Text(
                text = repairState.helperText.ifBlank { LocalMapRepairs.helperText(entry) },
                style = MaterialTheme.typography.bodySmall,
                color = palette.muted,
            )
            if (pendingCount > 0) {
                Text(
                    text = unreadablePendingText(pendingCount),
                    style = MaterialTheme.typography.bodySmall,
                    color = palette.warning,
                    fontWeight = FontWeight.SemiBold,
                )
            }
            if (repairState.errorMessage.isNotBlank()) {
                Text(repairState.errorMessage, color = palette.danger, style = MaterialTheme.typography.bodySmall)
            }
            if (downloadMessage.isNotBlank()) {
                Text(downloadMessage, color = palette.success, style = MaterialTheme.typography.bodySmall)
            }
            if (downloadError.isNotBlank()) {
                Text(downloadError, color = palette.danger, style = MaterialTheme.typography.bodySmall)
            }
            OutlinedTextField(
                value = draftText,
                onValueChange = { draftText = it },
                label = { Text("Map JSON") },
                minLines = 18,
                textStyle = MaterialTheme.typography.bodySmall.copy(fontFamily = FontFamily.Monospace),
                modifier = Modifier.fillMaxWidth(),
                enabled = !repairState.saving,
                shape = RoundedCornerShape(10.dp),
                colors = focusTextFieldColors(),
            )
        }
    }
}

@Composable
private fun PendingConflictResolutionDialog(
    state: ConflictResolutionUiState,
    onDismiss: () -> Unit,
    onChoice: (String, PendingConflictResolution) -> Unit,
    onAccept: () -> Unit,
) {
    if (state.targetPath.isBlank()) return
    val palette = LocalFocusPalette.current
    val remoteDocument = state.remoteDocument
    val localDocument = state.localDocument
    val diff = if (!state.loading && remoteDocument != null && localDocument != null) {
        ConflictDiffs.build(remoteDocument, localDocument)
    } else {
        null
    }
    FocusDialog(
        title = "Resolve conflicts in ${state.mapName.ifBlank { state.targetPath }}",
        onDismiss = onDismiss,
        confirmButton = {
            Button(
                enabled = conflictResolutionCanAccept(state),
                onClick = onAccept,
                colors = ButtonDefaults.buttonColors(
                    containerColor = palette.accent,
                    contentColor = Color.White,
                    disabledContainerColor = palette.panelMuted,
                    disabledContentColor = palette.muted,
                ),
            ) {
                Text(if (state.loading) "Saving..." else "Accept")
            }
        },
        dismissButton = {
            TextButton(onClick = onDismiss, colors = focusTextButtonColors()) {
                Text("Cancel")
            }
        },
    ) {
        Column(
            modifier = Modifier
                .fillMaxWidth()
                .heightIn(max = 560.dp)
                .verticalScroll(rememberScrollState()),
            verticalArrangement = Arrangement.spacedBy(12.dp),
        ) {
            Text(
                text = "Choose local to keep a queued Android change, or remote to discard that one change.",
                style = MaterialTheme.typography.bodySmall,
                color = palette.muted,
            )
            if (state.loading) {
                Text("Loading conflict details...", color = palette.muted, style = MaterialTheme.typography.bodySmall)
            }
            if (state.errorMessage.isNotBlank()) {
                Text(state.errorMessage, color = palette.danger, style = MaterialTheme.typography.bodySmall)
            }
            if (diff != null) {
                ConflictDiffView(diff, state.mapName.ifBlank { "map" })
            }
            if (state.items.isNotEmpty()) {
                Column(verticalArrangement = Arrangement.spacedBy(8.dp)) {
                    state.items.forEach { item ->
                        ConflictResolutionItemRow(
                            item = item,
                            enabled = !state.loading,
                            onChoice = onChoice,
                        )
                    }
                }
            }
        }
    }
}

@Composable
private fun SyncStatusDialog(
    visible: Boolean,
    info: SyncStatusPanelInfo,
    onDismiss: () -> Unit,
    onRetry: (SyncStatusRetryAction) -> Unit,
) {
    if (!visible) return
    val palette = LocalFocusPalette.current
    FocusDialog(
        title = "Sync status",
        onDismiss = onDismiss,
        confirmButton = {
            Button(
                enabled = info.canRetry,
                onClick = { onRetry(info.retryAction) },
                colors = ButtonDefaults.buttonColors(
                    containerColor = palette.accent,
                    contentColor = Color.White,
                    disabledContainerColor = palette.panelMuted,
                    disabledContentColor = palette.muted,
                ),
            ) {
                Text(syncStatusRetryLabel(info.retryAction))
            }
        },
        dismissButton = {
            TextButton(onClick = onDismiss, colors = focusTextButtonColors()) {
                Text("Close")
            }
        },
    ) {
        Column(
            modifier = Modifier
                .fillMaxWidth()
                .heightIn(max = 520.dp)
                .verticalScroll(rememberScrollState()),
            verticalArrangement = Arrangement.spacedBy(14.dp),
        ) {
            Column(
                modifier = Modifier
                    .fillMaxWidth()
                    .clip(RoundedCornerShape(12.dp))
                    .border(BorderStroke(1.dp, palette.border), RoundedCornerShape(12.dp))
                    .background(palette.inputBackground)
                    .padding(12.dp),
                verticalArrangement = Arrangement.spacedBy(6.dp),
            ) {
                Text(
                    text = info.message,
                    style = MaterialTheme.typography.bodyMedium,
                    color = syncStatusToneColor(info.tone, palette),
                    fontWeight = FontWeight.SemiBold,
                )
                Text(
                    text = info.detail,
                    style = MaterialTheme.typography.bodySmall,
                    color = palette.muted,
                    maxLines = 2,
                    overflow = TextOverflow.Ellipsis,
                )
            }
            Column(verticalArrangement = Arrangement.spacedBy(8.dp)) {
                Text(
                    text = "Diagnostics",
                    style = MaterialTheme.typography.titleSmall,
                    color = palette.text,
                    fontWeight = FontWeight.SemiBold,
                )
                SyncStatusDiagnosticRow("State", info.state)
                SyncStatusDiagnosticRow("Pending changes", info.pendingText)
                SyncStatusDiagnosticRow("Last sync time", info.lastSyncText)
                SyncStatusDiagnosticRow("Last message", info.lastMessage)
                SyncStatusDiagnosticRow("Last error", info.lastError)
            }
            Column(verticalArrangement = Arrangement.spacedBy(6.dp)) {
                Text(
                    text = "Repository",
                    style = MaterialTheme.typography.titleSmall,
                    color = palette.text,
                    fontWeight = FontWeight.SemiBold,
                )
                Text(
                    text = info.repository,
                    style = MaterialTheme.typography.bodySmall.copy(fontFamily = FontFamily.Monospace),
                    color = palette.muted,
                    maxLines = 2,
                    overflow = TextOverflow.Ellipsis,
                )
            }
        }
    }
}

@Composable
private fun SyncStatusDiagnosticRow(label: String, value: String) {
    val palette = LocalFocusPalette.current
    Row(
        modifier = Modifier.fillMaxWidth(),
        horizontalArrangement = Arrangement.spacedBy(12.dp),
        verticalAlignment = Alignment.Top,
    ) {
        Text(
            text = label,
            modifier = Modifier.widthIn(min = 112.dp, max = 132.dp),
            style = MaterialTheme.typography.bodySmall,
            color = palette.muted,
            fontWeight = FontWeight.SemiBold,
        )
        Text(
            text = value,
            modifier = Modifier.weight(1f),
            style = MaterialTheme.typography.bodySmall,
            color = palette.text,
        )
    }
}

private fun syncStatusToneColor(tone: SyncStatusTone, palette: FocusPalette): Color =
    when (tone) {
        SyncStatusTone.Pending -> palette.warning
        SyncStatusTone.Success -> palette.success
        SyncStatusTone.Warning -> palette.warning
        SyncStatusTone.Error -> palette.danger
        SyncStatusTone.Idle -> palette.accentStrong
    }

@Composable
private fun ConflictResolutionItemRow(
    item: ConflictResolutionUiItem,
    enabled: Boolean,
    onChoice: (String, PendingConflictResolution) -> Unit,
) {
    val palette = LocalFocusPalette.current
    Column(
        modifier = Modifier
            .fillMaxWidth()
            .clip(RoundedCornerShape(10.dp))
            .border(BorderStroke(1.dp, palette.border), RoundedCornerShape(10.dp))
            .background(palette.inputBackground)
            .padding(10.dp),
        verticalArrangement = Arrangement.spacedBy(8.dp),
    ) {
        Text(
            text = item.description,
            style = MaterialTheme.typography.bodyMedium,
            color = palette.text,
        )
        Row(
            modifier = Modifier.horizontalScroll(rememberScrollState()),
            horizontalArrangement = Arrangement.spacedBy(8.dp),
        ) {
            ConflictChoiceButton(
                label = "Take local",
                selected = item.choice == PendingConflictResolution.Local,
                enabled = enabled,
                onClick = { onChoice(item.pendingOperationId, PendingConflictResolution.Local) },
            )
            ConflictChoiceButton(
                label = "Take remote",
                selected = item.choice == PendingConflictResolution.Remote,
                enabled = enabled,
                onClick = { onChoice(item.pendingOperationId, PendingConflictResolution.Remote) },
            )
        }
        item.choice?.let { choice ->
            Text(
                text = PendingConflicts.choiceLabel(choice),
                style = MaterialTheme.typography.bodySmall,
                color = palette.accentStrong,
                fontWeight = FontWeight.SemiBold,
            )
        }
    }
}

@Composable
private fun ConflictChoiceButton(
    label: String,
    selected: Boolean,
    enabled: Boolean,
    onClick: () -> Unit,
) {
    val palette = LocalFocusPalette.current
    OutlinedButton(
        onClick = onClick,
        enabled = enabled,
        border = BorderStroke(1.dp, if (selected) palette.accent else palette.border),
        colors = ButtonDefaults.outlinedButtonColors(
            containerColor = if (selected) palette.panelMuted else Color.Transparent,
            contentColor = if (selected) palette.accentStrong else palette.text,
            disabledContentColor = palette.muted.copy(alpha = 0.45f),
        ),
        contentPadding = PaddingValues(horizontal = 10.dp, vertical = 6.dp),
    ) {
        Text(label)
    }
}

@Composable
private fun ConflictDiffView(diff: ConflictDiffResult, mapName: String) {
    val palette = LocalFocusPalette.current
    when (diff) {
        ConflictDiffResult.TooLarge -> {
            Text(
                text = "Document too large to display inline diff.",
                color = palette.muted,
                style = MaterialTheme.typography.bodySmall,
            )
        }
        ConflictDiffResult.NoChanges -> {
            Text(
                text = "No textual differences detected between local and remote.",
                color = palette.muted,
                style = MaterialTheme.typography.bodySmall,
            )
        }
        is ConflictDiffResult.Lines -> {
            Column(
                modifier = Modifier
                    .fillMaxWidth()
                    .heightIn(max = 260.dp)
                    .clip(RoundedCornerShape(10.dp))
                    .border(BorderStroke(1.dp, palette.border), RoundedCornerShape(10.dp))
                    .background(palette.panelMuted)
                    .padding(8.dp)
                    .verticalScroll(rememberScrollState())
                    .horizontalScroll(rememberScrollState()),
                verticalArrangement = Arrangement.spacedBy(2.dp),
            ) {
                Text(
                    text = "--- remote/$mapName.json    +++ local/$mapName.json",
                    style = MaterialTheme.typography.bodySmall.copy(fontFamily = FontFamily.Monospace),
                    color = palette.muted,
                )
                diff.lines.forEach { line ->
                    ConflictDiffLineText(line)
                }
            }
        }
    }
}

@Composable
private fun ConflictDiffLineText(line: ConflictDiffLine) {
    val palette = LocalFocusPalette.current
    when (line) {
        is ConflictDiffLine.Ellipsis -> Text(
            text = "@@ ... ${line.skipped} unchanged line${if (line.skipped == 1) "" else "s"} ... @@",
            style = MaterialTheme.typography.bodySmall.copy(fontFamily = FontFamily.Monospace),
            color = palette.muted,
        )
        is ConflictDiffLine.Text -> {
            val prefix = when (line.type) {
                ConflictDiffLineType.Add -> "+"
                ConflictDiffLineType.Remove -> "-"
                ConflictDiffLineType.Context -> " "
            }
            val color = when (line.type) {
                ConflictDiffLineType.Add -> palette.success
                ConflictDiffLineType.Remove -> palette.danger
                ConflictDiffLineType.Context -> palette.text
            }
            Text(
                text = prefix + line.text,
                style = MaterialTheme.typography.bodySmall.copy(fontFamily = FontFamily.Monospace),
                color = color,
                maxLines = 1,
                overflow = TextOverflow.Clip,
            )
        }
    }
}

private data class RawUnreadableMapSaveRequest(
    val fileName: String,
    val bytes: ByteArray,
)

internal fun unreadableMapTitle(item: UnreadableMapEntry): String =
    item.mapName.ifBlank { item.fileName.ifBlank { item.filePath } }

internal fun unreadablePendingText(pendingCount: Int): String =
    if (pendingCount > 0) {
        "Paused $pendingCount pending change${if (pendingCount == 1) "" else "s"}"
    } else {
        "No queued local changes"
    }

@Composable
private fun MapSummaryCard(
    snapshot: MapSnapshot,
    loading: Boolean,
    onOpenMap: () -> Unit,
    onDeleteMap: () -> Unit,
) {
    val palette = LocalFocusPalette.current
    val summary = MapQueries.buildMapSummary(snapshot)
    val plainRootTitle = plainInlineDisplayText(summary.rootTitle)
    FocusCard(
        modifier = Modifier.fillMaxWidth(),
        onClick = onOpenMap,
    ) {
        Column(
            modifier = Modifier.padding(14.dp),
            verticalArrangement = Arrangement.spacedBy(10.dp),
        ) {
            Row(
                horizontalArrangement = Arrangement.spacedBy(10.dp),
                verticalAlignment = Alignment.Top,
            ) {
                Column(modifier = Modifier.weight(1f)) {
                    FocusInlineText(
                        text = summary.rootTitle,
                        style = MaterialTheme.typography.titleMedium.copy(fontWeight = FontWeight.SemiBold),
                        maxLines = 2,
                        overflow = TextOverflow.Ellipsis,
                        onPlainClick = onOpenMap,
                    )
                    Text(
                        summary.filePath,
                        style = MaterialTheme.typography.bodySmall,
                        color = palette.muted,
                        maxLines = 1,
                        overflow = TextOverflow.Ellipsis,
                    )
                }
                FocusIconButton(
                    imageVector = Icons.Filled.Map,
                    contentDescription = "Open $plainRootTitle",
                    onClick = onOpenMap,
                    enabled = !loading,
                )
                DeleteOverflowMenu(
                    targetLabel = plainRootTitle,
                    enabled = !loading,
                    onDelete = onDeleteMap,
                )
            }
            TaskCountNumbers(summary.taskCounts.open, summary.taskCounts.todo, summary.taskCounts.doing, summary.taskCounts.done)
            Text(
                text = "Updated ${summary.updatedAt}",
                style = MaterialTheme.typography.bodySmall,
                color = palette.muted,
                maxLines = 1,
                overflow = TextOverflow.Ellipsis,
            )
        }
    }
}

@Composable
private fun TaskCountNumbers(open: Int, todo: Int, doing: Int, done: Int) {
    val palette = LocalFocusPalette.current
    Row(
        modifier = Modifier.horizontalScroll(rememberScrollState()),
        horizontalArrangement = Arrangement.spacedBy(7.dp),
        verticalAlignment = Alignment.CenterVertically,
    ) {
        TaskCountNumber(open, palette.muted)
        TaskCountSeparator()
        TaskCountNumber(todo, palette.taskTodo)
        TaskCountSeparator()
        TaskCountNumber(doing, palette.taskDoing)
        TaskCountSeparator()
        TaskCountNumber(done, palette.taskDone)
    }
}

@Composable
private fun TaskCountNumber(value: Int, color: Color) {
    Text(
        text = value.toString(),
        color = color,
        style = MaterialTheme.typography.bodyMedium,
        fontWeight = FontWeight.SemiBold,
    )
}

@Composable
private fun TaskCountSeparator() {
    Text(
        text = "·",
        color = LocalFocusPalette.current.muted,
        style = MaterialTheme.typography.bodySmall,
    )
}

@Composable
private fun TasksScreen(
    snapshots: List<MapSnapshot>,
    filter: TaskFilter,
    onFilterChanged: (TaskFilter) -> Unit,
    onOpenTask: (TaskEntry) -> Unit,
    onSetTaskState: (TaskEntry, TaskState) -> Unit,
) {
    val palette = LocalFocusPalette.current
    val entries = snapshots.flatMap { MapQueries.collectTaskEntries(it, filter) }
    Column(modifier = Modifier.fillMaxSize().padding(16.dp)) {
        Row(
            modifier = Modifier.horizontalScroll(rememberScrollState()),
            horizontalArrangement = Arrangement.spacedBy(8.dp),
        ) {
            listOf(TaskFilter.Open, TaskFilter.Todo, TaskFilter.Doing, TaskFilter.Done, TaskFilter.All).forEach { candidate ->
                FocusPill(
                    label = candidate.name,
                    selected = filter == candidate,
                    toneColor = focusFilterColor(candidate, palette),
                    leadingDot = true,
                    onClick = { onFilterChanged(candidate) },
                )
            }
        }
        Spacer(Modifier.height(12.dp))
        if (entries.isEmpty()) {
            EmptyCard("No tasks match the selected filter.")
            return
        }
        LazyColumn(
            modifier = Modifier.fillMaxSize(),
            verticalArrangement = Arrangement.spacedBy(10.dp),
            contentPadding = PaddingValues(bottom = 16.dp),
        ) {
            items(entries, key = { "${it.filePath}:${it.nodeId}" }) { entry ->
                TaskEntryCard(
                    entry = entry,
                    onOpenTask = { onOpenTask(entry) },
                    onSetTaskState = { onSetTaskState(entry, it) },
                )
            }
        }
    }
}

@Composable
private fun TaskEntryCard(
    entry: TaskEntry,
    onOpenTask: () -> Unit,
    onSetTaskState: (TaskState) -> Unit,
) {
    val palette = LocalFocusPalette.current
    FocusCard(
        modifier = Modifier.fillMaxWidth(),
        onClick = onOpenTask,
    ) {
        Column(
            modifier = Modifier.padding(14.dp),
            verticalArrangement = Arrangement.spacedBy(9.dp),
        ) {
            Row(horizontalArrangement = Arrangement.spacedBy(10.dp), verticalAlignment = Alignment.Top) {
                TaskDot(taskState = entry.taskState, modifier = Modifier.padding(top = 5.dp), selected = true)
                Column(modifier = Modifier.weight(1f)) {
                    FocusInlineText(
                        text = entry.nodeName,
                        style = MaterialTheme.typography.titleMedium.copy(fontWeight = FontWeight.SemiBold),
                        maxLines = 2,
                        overflow = TextOverflow.Ellipsis,
                        onPlainClick = onOpenTask,
                    )
                    Text(entry.mapName, style = MaterialTheme.typography.bodyMedium, color = palette.accentStrong)
                    FocusInlinePath(
                        pathSegments = entry.nodePathSegments,
                        style = MaterialTheme.typography.bodySmall,
                        color = palette.muted,
                        maxLines = 2,
                        overflow = TextOverflow.Ellipsis,
                        onPlainClick = onOpenTask,
                    )
                }
            }
            TaskStateButtons(currentState = entry.taskState, onSetTaskState = onSetTaskState)
        }
    }
}

@Composable
private fun EmptyPanel(message: String) {
    Box(modifier = Modifier.fillMaxSize().padding(16.dp), contentAlignment = Alignment.Center) {
        EmptyCard(message)
    }
}

@Composable
private fun EmptyCard(message: String) {
    val palette = LocalFocusPalette.current
    FocusCard(modifier = Modifier.fillMaxWidth()) {
        Text(
            text = message,
            modifier = Modifier.padding(16.dp),
            style = MaterialTheme.typography.bodyMedium,
            color = palette.muted,
        )
    }
}

@Composable
private fun MapEditorScreen(
    snapshot: MapSnapshot,
    allSnapshots: List<MapSnapshot>,
    focusedNodeId: String,
    fabSide: FabSidePreference,
    attachmentUploadState: AttachmentUploadUiState,
    attachmentDeleteState: AttachmentDeleteUiState,
    onSetTaskState: (String, String, TaskState) -> Unit,
    onEditNode: (String, String, String) -> Unit,
    onAddChild: (String, String, String, Boolean) -> Unit,
    onUploadAttachment: (String, String, Uri) -> Unit,
    onUploadVoiceAttachment: (String, String, String, String, String, ByteArray) -> Unit,
    onOpenAttachment: (String, String, NodeAttachment) -> Unit,
    onDeleteAttachment: (String, String, String, String?) -> Unit,
    onDeleteNode: (String, String) -> Unit,
    onToggleHideDone: (String, String) -> Unit,
    onToggleStarred: (String, String) -> Unit,
    onFocusNode: (String) -> Unit,
    onOpenRelatedNode: (RelatedNodeEntry) -> Unit,
) {
    var editingNodeId by remember(snapshot.filePath) { mutableStateOf<String?>(null) }
    var addingChild by remember(snapshot.filePath) { mutableStateOf<AddChildTarget?>(null) }
    var deleteCandidate by remember(snapshot.filePath) { mutableStateOf<Node?>(null) }
    var attachmentDeleteCandidate by remember(snapshot.filePath) { mutableStateOf<AttachmentDeleteTarget?>(null) }
    var collapsedOverrides by remember(snapshot.filePath) {
        mutableStateOf<Map<String, Boolean>>(emptyMap())
    }
    var expandedRelatedNodeGroupKey by remember(snapshot.filePath) { mutableStateOf("") }
    val focusedRecord = resolveFocusedNodeRecord(snapshot.document, focusedNodeId)
    val focusedNode = focusedRecord.node
    val focusedAddTarget = focusedAddTargetNode(snapshot.document, focusedNode.uniqueIdentifier)
    val focusedNodeHideDone = MapQueries.getNodeHideDoneState(snapshot.document, focusedNode.uniqueIdentifier)
    val showHideDoneFab = shouldShowFocusedHideDoneAction(snapshot.document, focusedNode.uniqueIdentifier)
    val focusedAttachments = nodeAttachments(focusedNode)
    val mapEditorFabActions = mapEditorFabActionsForSide(
        side = fabSide,
        showHideDone = showHideDoneFab,
        showAddActions = focusedAddTarget != null,
    )
    val relatedGroups = remember(focusedNode, allSnapshots) {
        relatedNodeGroups(
            outgoing = RelatedNodes.collectOutgoing(focusedNode, allSnapshots),
            backlinks = RelatedNodes.collectBacklinks(focusedNode.uniqueIdentifier, allSnapshots),
        )
    }
    val visibleNodes = remember(snapshot.document, focusedNode.uniqueIdentifier, collapsedOverrides) {
        focusedVisibleNodes(snapshot.document, focusedNode.uniqueIdentifier, collapsedOverrides)
    }
    val editingNode = editingNodeId
        ?.let { nodeId -> MapQueries.findNode(snapshot.document, nodeId)?.node }
        ?.takeIf { it.canEditText }
    LaunchedEffect(snapshot.filePath, focusedNode.uniqueIdentifier, relatedGroups) {
        if (
            expandedRelatedNodeGroupKey.isNotBlank() &&
            relatedGroups.none { group -> relatedNodeGroupKey(snapshot.filePath, focusedNode.uniqueIdentifier, group.relationLabel) == expandedRelatedNodeGroupKey }
        ) {
            expandedRelatedNodeGroupKey = ""
        }
    }
    LaunchedEffect(snapshot.document, addingChild?.parent?.uniqueIdentifier, addingChild?.asTask) {
        val target = addingChild ?: return@LaunchedEffect
        val currentParent = MapQueries.findNode(snapshot.document, target.parent.uniqueIdentifier)?.node
        addingChild = currentParent
            ?.takeIf { it.canEditText }
            ?.let { target.copy(parent = it) }
    }
    LaunchedEffect(snapshot.document, editingNodeId) {
        if (editingNodeId != null && editingNode == null) {
            editingNodeId = null
        }
    }
    Box(modifier = Modifier.fillMaxSize()) {
        LazyColumn(
            modifier = Modifier.fillMaxSize(),
            contentPadding = PaddingValues(start = 16.dp, top = 16.dp, end = 16.dp, bottom = 96.dp),
            verticalArrangement = Arrangement.spacedBy(8.dp),
        ) {
            item {
                MapHeader(
                    snapshot = snapshot,
                    focusedRecord = focusedRecord,
                    onFocusParent = { focusedRecord.parent?.let { onFocusNode(it.uniqueIdentifier) } },
                )
            }
            items(visibleNodes, key = { it.node.uniqueIdentifier }) { item ->
                Column(verticalArrangement = Arrangement.spacedBy(8.dp)) {
                    NodeRow(
                        item = item,
                        isMapRoot = item.node.uniqueIdentifier == snapshot.document.rootNode.uniqueIdentifier,
                        isFocusedNode = item.node.uniqueIdentifier == focusedNode.uniqueIdentifier,
                        onNodeClick = {
                            when (
                                focusedNodeClickAction(
                                    clickedNodeId = item.node.uniqueIdentifier,
                                    focusedNodeId = focusedNode.uniqueIdentifier,
                                    node = item.node,
                                )
                            ) {
                                FocusedNodeClickAction.OpenPrimaryAttachment -> {
                                    openablePrimaryAttachment(item.node)?.let { attachment ->
                                        onOpenAttachment(snapshot.filePath, item.node.uniqueIdentifier, attachment)
                                    } ?: run {
                                        editingNodeId = item.node.uniqueIdentifier
                                    }
                                }
                                FocusedNodeClickAction.EditText -> {
                                    editingNodeId = item.node.uniqueIdentifier
                                }
                                FocusedNodeClickAction.FocusNode -> {
                                    onFocusNode(item.node.uniqueIdentifier)
                                    expandedRelatedNodeGroupKey = ""
                                }
                            }
                        },
                        onSetTaskState = { taskState ->
                            onSetTaskState(snapshot.filePath, item.node.uniqueIdentifier, taskState)
                        },
                        onToggleStarred = {
                            onToggleStarred(snapshot.filePath, item.node.uniqueIdentifier)
                        },
                        onToggleCollapsed = {
                            collapsedOverrides = toggleCollapsed(item.node, collapsedOverrides)
                        },
                        onDelete = { deleteCandidate = item.node },
                    )
                    if (item.node.uniqueIdentifier == focusedNode.uniqueIdentifier) {
                        if (focusedAttachments.isNotEmpty()) {
                            AttachmentListSection(
                                attachments = focusedAttachments,
                                onOpenAttachment = { attachment ->
                                    onOpenAttachment(snapshot.filePath, focusedNode.uniqueIdentifier, attachment)
                                },
                                onDeleteAttachment = if (canDeleteAttachmentsFromNode(focusedNode)) {
                                    { attachment ->
                                        attachmentDeleteCandidate = attachmentDeleteTarget(
                                            filePath = snapshot.filePath,
                                            nodeId = focusedNode.uniqueIdentifier,
                                            attachment = attachment,
                                        )
                                    }
                                } else {
                                    null
                                },
                                isDeletingAttachment = { attachment ->
                                    attachmentDeleteState
                                        .forTarget(snapshot.filePath, focusedNode.uniqueIdentifier, attachment.id)
                                        .deleting
                                },
                                deleteErrorForAttachment = { attachment ->
                                    attachmentDeleteState
                                        .forTarget(snapshot.filePath, focusedNode.uniqueIdentifier, attachment.id)
                                        .errorMessage
                                },
                            )
                        }
                        if (relatedGroups.isNotEmpty()) {
                            RelatedNodesSection(
                                groups = relatedGroups,
                                expandedGroupKey = expandedRelatedNodeGroupKey,
                                sourceMapPath = snapshot.filePath,
                                sourceNodeId = focusedNode.uniqueIdentifier,
                                onToggleGroup = { group ->
                                    val key = relatedNodeGroupKey(snapshot.filePath, focusedNode.uniqueIdentifier, group.relationLabel)
                                    expandedRelatedNodeGroupKey = if (expandedRelatedNodeGroupKey == key) "" else key
                                },
                                onOpenRelatedNode = onOpenRelatedNode,
                            )
                        }
                    }
                }
            }
        }
        Row(
            modifier = Modifier
                .align(fabAlignment(fabSide))
                .padding(20.dp),
            horizontalArrangement = Arrangement.spacedBy(10.dp),
            verticalAlignment = Alignment.CenterVertically,
        ) {
            mapEditorFabActions.forEach { action ->
                when (action) {
                    MapEditorFabAction.HideDone -> {
                        FocusIconButton(
                            imageVector = if (focusedNodeHideDone) Icons.Filled.Visibility else Icons.Filled.VisibilityOff,
                            contentDescription = if (focusedNodeHideDone) "Show done items" else "Hide done items",
                            onClick = { onToggleHideDone(snapshot.filePath, focusedNode.uniqueIdentifier) },
                            selected = focusedNodeHideDone,
                        )
                    }
                    MapEditorFabAction.AddTask -> {
                        focusedAddTarget?.let { target ->
                            FocusFab(
                                imageVector = Icons.Filled.AddTask,
                                contentDescription = addChildActionLabel(target, asTask = true),
                                onClick = { addingChild = AddChildTarget(target, asTask = true) },
                            )
                        }
                    }
                    MapEditorFabAction.AddNote -> {
                        focusedAddTarget?.let { target ->
                            FocusFab(
                                imageVector = Icons.AutoMirrored.Filled.NoteAdd,
                                contentDescription = addChildActionLabel(target, asTask = false),
                                onClick = { addingChild = AddChildTarget(target, asTask = false) },
                            )
                        }
                    }
                }
            }
        }
    }

    editingNode?.let { node ->
        EditNodeDialog(
            filePath = snapshot.filePath,
            node = node,
            attachmentUploadState = attachmentUploadState.forTarget(snapshot.filePath, node.uniqueIdentifier),
            attachmentDeleteState = attachmentDeleteState,
            onDismiss = { editingNodeId = null },
            onSave = { text ->
                editingNodeId = null
                onEditNode(snapshot.filePath, node.uniqueIdentifier, text)
            },
            onAttachFile = { uri -> onUploadAttachment(snapshot.filePath, node.uniqueIdentifier, uri) },
            onAttachVoiceNote = { displayName, fileName, mediaType, bytes ->
                onUploadVoiceAttachment(snapshot.filePath, node.uniqueIdentifier, displayName, fileName, mediaType, bytes)
            },
            onOpenAttachment = { attachment ->
                onOpenAttachment(snapshot.filePath, node.uniqueIdentifier, attachment)
            },
            onDeleteAttachment = { attachment ->
                attachmentDeleteCandidate = attachmentDeleteTarget(
                    filePath = snapshot.filePath,
                    nodeId = node.uniqueIdentifier,
                    attachment = attachment,
                )
            },
        )
    }
    addingChild?.let { target ->
        AddChildDialog(
            target = target,
            onDismiss = { addingChild = null },
            onSave = { text ->
                onAddChild(snapshot.filePath, target.parent.uniqueIdentifier, text, target.asTask)
                if (shouldDismissAfterAddChild(target.asTask)) {
                    addingChild = null
                }
            },
        )
    }
    deleteCandidate?.let { node ->
        DeleteNodeDialog(
            node = node,
            onDismiss = { deleteCandidate = null },
            onConfirm = {
                val fallbackFocusId = focusAfterDeletingNode(snapshot.document, focusedNode.uniqueIdentifier, node.uniqueIdentifier)
                deleteCandidate = null
                onFocusNode(fallbackFocusId)
                onDeleteNode(snapshot.filePath, node.uniqueIdentifier)
            },
        )
    }
    attachmentDeleteCandidate?.let { target ->
        DeleteAttachmentDialog(
            target = target,
            deleting = attachmentDeleteState.forTarget(target.filePath, target.nodeId, target.attachmentId).deleting,
            onDismiss = { attachmentDeleteCandidate = null },
            onConfirm = {
                attachmentDeleteCandidate = null
                onDeleteAttachment(target.filePath, target.nodeId, target.attachmentId, target.expectedRevision)
            },
        )
    }
}

@Composable
private fun MapHeader(
    snapshot: MapSnapshot,
    focusedRecord: NodeRecord,
    onFocusParent: () -> Unit,
) {
    val palette = LocalFocusPalette.current
    Row(
        modifier = Modifier
            .fillMaxWidth()
            .padding(bottom = 6.dp),
        horizontalArrangement = Arrangement.spacedBy(10.dp),
        verticalAlignment = Alignment.Top,
    ) {
        if (focusedRecord.parent != null) {
            FocusIconButton(
                imageVector = Icons.AutoMirrored.Filled.ArrowBack,
                contentDescription = "Focus parent node",
                onClick = onFocusParent,
            )
        }
        Column(
            modifier = Modifier.weight(1f),
            verticalArrangement = Arrangement.spacedBy(2.dp),
        ) {
            Text(snapshot.mapName, style = MaterialTheme.typography.headlineSmall, fontWeight = FontWeight.SemiBold)
            FocusInlinePath(
                pathSegments = focusedRecord.pathSegments,
                style = MaterialTheme.typography.bodySmall,
                color = palette.accentStrong,
                maxLines = 1,
                overflow = TextOverflow.Ellipsis,
            )
            Text(
                snapshot.filePath,
                style = MaterialTheme.typography.bodySmall,
                color = palette.muted,
                maxLines = 1,
                overflow = TextOverflow.Ellipsis,
            )
        }
    }
}

@Composable
private fun AttachmentListSection(
    attachments: List<NodeAttachment>,
    modifier: Modifier = Modifier,
    onOpenAttachment: ((NodeAttachment) -> Unit)? = null,
    onDeleteAttachment: ((NodeAttachment) -> Unit)? = null,
    isDeletingAttachment: (NodeAttachment) -> Boolean = { false },
    deleteErrorForAttachment: (NodeAttachment) -> String = { "" },
) {
    if (attachments.isEmpty()) return
    val palette = LocalFocusPalette.current
    Column(
        modifier = modifier
            .fillMaxWidth()
            .clip(RoundedCornerShape(14.dp))
            .background(palette.panelMuted)
            .border(BorderStroke(1.dp, palette.border), RoundedCornerShape(14.dp))
            .padding(10.dp),
        verticalArrangement = Arrangement.spacedBy(8.dp),
    ) {
        Text(
            text = "Attachments",
            style = MaterialTheme.typography.labelMedium,
            fontWeight = FontWeight.SemiBold,
            color = palette.muted,
        )
        attachments.forEach { attachment ->
            AttachmentRow(
                attachment = attachment,
                onOpenAttachment = onOpenAttachment,
                onDeleteAttachment = onDeleteAttachment,
                deleting = isDeletingAttachment(attachment),
            )
            val deleteError = deleteErrorForAttachment(attachment)
            if (deleteError.isNotBlank()) {
                Text(
                    text = deleteError,
                    style = MaterialTheme.typography.bodySmall,
                    color = palette.danger,
                )
            }
        }
    }
}

@Composable
private fun AttachmentRow(
    attachment: NodeAttachment,
    onOpenAttachment: ((NodeAttachment) -> Unit)? = null,
    onDeleteAttachment: ((NodeAttachment) -> Unit)? = null,
    deleting: Boolean = false,
) {
    val palette = LocalFocusPalette.current
    val shape = RoundedCornerShape(10.dp)
    val canView = canViewAttachment(attachment) && onOpenAttachment != null
    val canDelete = canDeleteAttachment(attachment) && onDeleteAttachment != null
    Row(
        modifier = Modifier
            .fillMaxWidth()
            .clip(shape)
            .background(palette.panelBackground)
            .border(BorderStroke(1.dp, palette.border), shape)
            .semantics {
                contentDescription = attachmentAccessibilityLabel(attachment)
            }
            .padding(horizontal = 10.dp, vertical = 8.dp),
        horizontalArrangement = Arrangement.spacedBy(9.dp),
        verticalAlignment = Alignment.CenterVertically,
    ) {
        Text(
            text = attachmentTypeLabel(attachment),
            style = MaterialTheme.typography.labelSmall,
            fontWeight = FontWeight.Bold,
            color = palette.accentStrong,
            modifier = Modifier
                .clip(CircleShape)
                .background(palette.accentSoft)
                .border(BorderStroke(1.dp, palette.accentBorder), CircleShape)
                .padding(horizontal = 7.dp, vertical = 4.dp),
        )
        Text(
            text = attachmentDisplayName(attachment),
            style = MaterialTheme.typography.bodySmall,
            color = palette.text,
            maxLines = 1,
            overflow = TextOverflow.Ellipsis,
            modifier = Modifier.weight(1f),
        )
        if (onOpenAttachment != null) {
            TextButton(
                enabled = canView,
                onClick = { onOpenAttachment(attachment) },
                colors = focusTextButtonColors(),
            ) {
                Text("View")
            }
        }
        if (onDeleteAttachment != null) {
            TextButton(
                enabled = canDelete && !deleting,
                onClick = { onDeleteAttachment(attachment) },
                modifier = Modifier.semantics {
                    contentDescription = attachmentDeleteActionLabel(attachment)
                },
                colors = ButtonDefaults.textButtonColors(
                    contentColor = palette.danger,
                    disabledContentColor = palette.muted.copy(alpha = 0.45f),
                ),
            ) {
                Text(if (deleting) "Removing..." else "Remove")
            }
        }
    }
}

private data class AttachmentSaveRequest(
    val exportFile: com.systemssanity.focus.domain.maps.AttachmentExportFile,
    val bytes: ByteArray,
)

@Composable
private fun AttachmentViewerDialog(
    snapshots: List<MapSnapshot>,
    viewerState: AttachmentViewerUiState,
    deleteState: AttachmentDeleteUiState,
    onDismiss: () -> Unit,
    onDeleteAttachment: (String, String, String, String?) -> Unit,
) {
    val request = viewerState.request ?: return
    val context = LocalContext.current
    val coroutineScope = rememberCoroutineScope()
    var deleteCandidate by remember(request.attachment.id, viewerState.versionToken) { mutableStateOf<AttachmentDeleteTarget?>(null) }
    var exportMessage by remember(request.attachment.id, viewerState.bytes) { mutableStateOf("") }
    var exportError by remember(request.attachment.id, viewerState.bytes) { mutableStateOf("") }
    var pendingSave by remember(request.attachment.id, viewerState.bytes) { mutableStateOf<AttachmentSaveRequest?>(null) }
    val bytes = viewerState.bytes
    val canExport = AttachmentExports.canExport(
        bytes = bytes,
        loading = viewerState.loading,
        errorMessage = viewerState.errorMessage,
    )
    val exportFile = remember(request.attachment, request.kind, viewerState.mediaType) {
        AttachmentExports.exportFile(
            attachment = request.attachment,
            kind = request.kind,
            loadedMediaType = viewerState.mediaType,
        )
    }
    val deleteTarget = remember(snapshots, viewerState) {
        viewerAttachmentDeleteTarget(snapshots, viewerState)
    }
    val targetDeleteState = deleteTarget
        ?.let { deleteState.forTarget(it.filePath, it.nodeId, it.attachmentId) }
        ?: AttachmentDeleteUiState()
    val saveLauncher = rememberLauncherForActivityResult(ActivityResultContracts.StartActivityForResult()) { result ->
        val saveRequest = pendingSave
        pendingSave = null
        if (result.resultCode != Activity.RESULT_OK) {
            return@rememberLauncherForActivityResult
        }
        val uri = result.data?.data
        if (saveRequest == null || uri == null) {
            exportMessage = ""
            exportError = "Could not save attachment: no file was selected."
            return@rememberLauncherForActivityResult
        }
        coroutineScope.launch {
            val writeResult = withContext(Dispatchers.IO) {
                runCatching {
                    context.contentResolver.openOutputStream(uri)?.use { output ->
                        output.write(saveRequest.bytes)
                    } ?: error("Could not open the selected file.")
                }
            }
            writeResult
                .onSuccess {
                    exportError = ""
                    exportMessage = "Saved ${saveRequest.exportFile.fileName}."
                }
                .onFailure { error ->
                    exportMessage = ""
                    exportError = "Could not save attachment: ${error.message ?: "unknown error"}"
                }
        }
    }
    FocusDialog(
        title = AttachmentViewers.title(request.attachment),
        onDismiss = onDismiss,
        confirmButton = {
            Row(horizontalArrangement = Arrangement.spacedBy(8.dp)) {
                if (canExport && bytes != null) {
                    val exportBytes = bytes
                    TextButton(
                        modifier = Modifier.semantics {
                            contentDescription = AttachmentExports.saveActionLabel(request.attachment, request.kind)
                        },
                        onClick = {
                            exportMessage = ""
                            exportError = ""
                            pendingSave = AttachmentSaveRequest(exportFile, exportBytes)
                            val intent = Intent(Intent.ACTION_CREATE_DOCUMENT).apply {
                                addCategory(Intent.CATEGORY_OPENABLE)
                                type = exportFile.mimeType
                                putExtra(Intent.EXTRA_TITLE, exportFile.fileName)
                            }
                            runCatching {
                                saveLauncher.launch(intent)
                            }.onFailure { error ->
                                pendingSave = null
                                exportError = "Could not open save dialog: ${error.message ?: "unknown error"}"
                            }
                        },
                        colors = focusTextButtonColors(),
                    ) {
                        Text("Save")
                    }
                    TextButton(
                        modifier = Modifier.semantics {
                            contentDescription = AttachmentExports.shareActionLabel(request.attachment, request.kind)
                        },
                        onClick = {
                            exportMessage = ""
                            exportError = ""
                            coroutineScope.launch {
                                val exportResult = withContext(Dispatchers.IO) {
                                    runCatching {
                                        AttachmentExports.writeAttachmentExportFile(
                                            cacheDir = context.cacheDir,
                                            fileName = exportFile.fileName,
                                            bytes = exportBytes,
                                        )
                                    }
                                }
                                exportResult
                                    .onSuccess { file ->
                                        runCatching {
                                            val uri = FileProvider.getUriForFile(
                                                context,
                                                "${context.packageName}.fileprovider",
                                                file,
                                            )
                                            val shareIntent = Intent(Intent.ACTION_SEND).apply {
                                                type = exportFile.mimeType
                                                putExtra(Intent.EXTRA_STREAM, uri)
                                                putExtra(Intent.EXTRA_SUBJECT, exportFile.fileName)
                                                addFlags(Intent.FLAG_GRANT_READ_URI_PERMISSION)
                                            }
                                            context.startActivity(
                                                Intent.createChooser(
                                                    shareIntent,
                                                    "Share ${exportFile.fileName}",
                                                ),
                                            )
                                        }
                                            .onSuccess {
                                                exportMessage = "Share sheet opened for ${exportFile.fileName}."
                                            }
                                            .onFailure { error ->
                                                exportError = "Could not share attachment: ${error.message ?: "unknown error"}"
                                            }
                                    }
                                    .onFailure { error ->
                                        exportError = "Could not prepare attachment for sharing: ${error.message ?: "unknown error"}"
                                    }
                            }
                        },
                        colors = focusTextButtonColors(),
                    ) {
                        Text("Share")
                    }
                }
                if (deleteTarget != null) {
                    DestructiveTextButton(
                        enabled = !targetDeleteState.deleting,
                        onClick = { deleteCandidate = deleteTarget },
                        label = if (targetDeleteState.deleting) "Deleting..." else "Delete",
                    )
                }
                TextButton(onClick = onDismiss, colors = focusTextButtonColors()) {
                    Text("Close")
                }
            }
        },
        dismissButton = {},
    ) {
        AttachmentViewerBody(
            viewerState = viewerState,
            exportMessage = exportMessage,
            exportError = exportError,
            deleteError = targetDeleteState.errorMessage,
        )
    }
    deleteCandidate?.let { target ->
        DeleteAttachmentDialog(
            target = target,
            deleting = deleteState.forTarget(target.filePath, target.nodeId, target.attachmentId).deleting,
            onDismiss = { deleteCandidate = null },
            onConfirm = {
                deleteCandidate = null
                onDeleteAttachment(target.filePath, target.nodeId, target.attachmentId, target.expectedRevision)
            },
        )
    }
}

@Composable
private fun AttachmentViewerBody(
    viewerState: AttachmentViewerUiState,
    exportMessage: String,
    exportError: String,
    deleteError: String,
) {
    val request = viewerState.request ?: return
    val palette = LocalFocusPalette.current
    Column(
        modifier = Modifier
            .fillMaxWidth()
            .heightIn(max = 520.dp)
            .verticalScroll(rememberScrollState()),
        verticalArrangement = Arrangement.spacedBy(10.dp),
    ) {
        if (viewerState.loading) {
            Text("Loading attachment...", color = palette.muted)
            return@Column
        }
        if (viewerState.errorMessage.isNotBlank()) {
            Text(viewerState.errorMessage, color = palette.danger)
            return@Column
        }

        val bytes = viewerState.bytes
        if (bytes == null) {
            Text("Attachment content is unavailable.", color = palette.danger)
            return@Column
        }

        Text(
            text = "${AttachmentViewers.formatByteSize(bytes.size)} • ${viewerState.mediaType.ifBlank { request.attachment.mediaType.ifBlank { "application/octet-stream" } }}",
            style = MaterialTheme.typography.labelMedium,
            color = palette.muted,
        )
        if (exportMessage.isNotBlank()) {
            Text(exportMessage, color = palette.success)
        }
        if (exportError.isNotBlank()) {
            Text(exportError, color = palette.danger)
        }
        if (deleteError.isNotBlank()) {
            Text(deleteError, color = palette.danger)
        }
        when (request.kind) {
            AttachmentViewerKind.Text -> TextAttachmentPreview(bytes)
            AttachmentViewerKind.Audio -> AudioAttachmentPreview(bytes, AttachmentViewers.title(request.attachment))
            AttachmentViewerKind.Pdf -> PdfAttachmentPreview(bytes)
            AttachmentViewerKind.Image -> ImageAttachmentPreview(bytes, AttachmentViewers.title(request.attachment))
        }
    }
}

@Composable
private fun ImageAttachmentPreview(bytes: ByteArray, title: String) {
    val palette = LocalFocusPalette.current
    val bitmap = remember(bytes) {
        BitmapFactory.decodeByteArray(bytes, 0, bytes.size)
    }
    if (bitmap == null) {
        Text(AttachmentViewers.unsupportedPreviewMessage(AttachmentViewerKind.Image), color = palette.danger)
        return
    }
    Image(
        bitmap = bitmap.asImageBitmap(),
        contentDescription = title,
        contentScale = ContentScale.Fit,
        modifier = Modifier
            .fillMaxWidth()
            .heightIn(min = 180.dp, max = 420.dp)
            .clip(RoundedCornerShape(12.dp))
            .background(palette.panelMuted),
    )
}

@Composable
private fun TextAttachmentPreview(bytes: ByteArray) {
    val palette = LocalFocusPalette.current
    val text = remember(bytes) { bytes.toString(Charsets.UTF_8) }
    Text(
        text = text,
        style = MaterialTheme.typography.bodySmall,
        fontFamily = FontFamily.Monospace,
        color = palette.text,
        modifier = Modifier
            .fillMaxWidth()
            .clip(RoundedCornerShape(12.dp))
            .background(palette.panelMuted)
            .border(BorderStroke(1.dp, palette.border), RoundedCornerShape(12.dp))
            .padding(10.dp),
    )
}

private data class PdfPreviewPage(
    val bitmap: Bitmap,
    val pageIndex: Int,
    val pageCount: Int,
)

@Composable
private fun PdfAttachmentPreview(bytes: ByteArray) {
    val context = LocalContext.current
    val palette = LocalFocusPalette.current
    var pageIndex by remember(bytes) { mutableStateOf(0) }
    val preview = remember(bytes, pageIndex) {
        renderPdfPage(context.cacheDir, bytes, pageIndex)
    }
    preview
        .onSuccess { page ->
            Column(verticalArrangement = Arrangement.spacedBy(8.dp)) {
                Image(
                    bitmap = page.bitmap.asImageBitmap(),
                    contentDescription = "PDF page ${page.pageIndex + 1}",
                    contentScale = ContentScale.Fit,
                    modifier = Modifier
                        .fillMaxWidth()
                        .heightIn(min = 240.dp, max = 460.dp)
                        .clip(RoundedCornerShape(12.dp))
                        .background(Color.White),
                )
                Row(
                    horizontalArrangement = Arrangement.spacedBy(8.dp),
                    verticalAlignment = Alignment.CenterVertically,
                ) {
                    TextButton(
                        enabled = page.pageIndex > 0,
                        onClick = { pageIndex = page.pageIndex - 1 },
                        colors = focusTextButtonColors(),
                    ) {
                        Text("Previous")
                    }
                    Text(
                        text = "Page ${page.pageIndex + 1} of ${page.pageCount}",
                        style = MaterialTheme.typography.labelMedium,
                        color = palette.muted,
                    )
                    TextButton(
                        enabled = page.pageIndex < page.pageCount - 1,
                        onClick = { pageIndex = page.pageIndex + 1 },
                        colors = focusTextButtonColors(),
                    ) {
                        Text("Next")
                    }
                }
            }
        }
        .onFailure {
            Text(AttachmentViewers.unsupportedPreviewMessage(AttachmentViewerKind.Pdf), color = palette.danger)
        }
}

private fun renderPdfPage(cacheDir: File, bytes: ByteArray, requestedPageIndex: Int): Result<PdfPreviewPage> =
    runCatching {
        val file = File.createTempFile("focus-pdf-", ".pdf", cacheDir)
        try {
            file.writeBytes(bytes)
            ParcelFileDescriptor.open(file, ParcelFileDescriptor.MODE_READ_ONLY).use { descriptor ->
                PdfRenderer(descriptor).use { renderer ->
                    if (renderer.pageCount <= 0) {
                        error("PDF has no pages.")
                    }
                    val pageIndex = requestedPageIndex.coerceIn(0, renderer.pageCount - 1)
                    renderer.openPage(pageIndex).use { page ->
                        val maxDimension = 1600f
                        val scale = minOf(1f, maxDimension / maxOf(page.width, page.height).coerceAtLeast(1))
                        val width = (page.width * scale).toInt().coerceAtLeast(1)
                        val height = (page.height * scale).toInt().coerceAtLeast(1)
                        val bitmap = Bitmap.createBitmap(width, height, Bitmap.Config.ARGB_8888)
                        bitmap.eraseColor(android.graphics.Color.WHITE)
                        page.render(bitmap, null, null, PdfRenderer.Page.RENDER_MODE_FOR_DISPLAY)
                        PdfPreviewPage(bitmap, pageIndex, renderer.pageCount)
                    }
                }
            }
        } finally {
            file.delete()
        }
    }

@Composable
private fun AudioAttachmentPreview(bytes: ByteArray, title: String) {
    val context = LocalContext.current
    val palette = LocalFocusPalette.current
    var player by remember(bytes) { mutableStateOf<MediaPlayer?>(null) }
    var prepared by remember(bytes) { mutableStateOf(false) }
    var playing by remember(bytes) { mutableStateOf(false) }
    var errorMessage by remember(bytes) { mutableStateOf("") }

    DisposableEffect(bytes) {
        val file = File.createTempFile("focus-audio-", ".bin", context.cacheDir)
        val mediaPlayer = MediaPlayer()
        runCatching {
            file.writeBytes(bytes)
            mediaPlayer.setDataSource(file.absolutePath)
            mediaPlayer.prepare()
            mediaPlayer.setOnCompletionListener {
                playing = false
                runCatching { it.seekTo(0) }
            }
            player = mediaPlayer
            prepared = true
        }.onFailure { error ->
            errorMessage = error.message ?: AttachmentViewers.unsupportedPreviewMessage(AttachmentViewerKind.Audio)
            mediaPlayer.release()
            file.delete()
        }
        onDispose {
            runCatching { mediaPlayer.stop() }
            mediaPlayer.release()
            file.delete()
            player = null
            prepared = false
            playing = false
        }
    }

    Column(verticalArrangement = Arrangement.spacedBy(8.dp)) {
        Text(title, style = MaterialTheme.typography.bodyMedium, color = palette.text)
        if (errorMessage.isNotBlank()) {
            Text(errorMessage, color = palette.danger)
            return@Column
        }
        Row(
            horizontalArrangement = Arrangement.spacedBy(8.dp),
            verticalAlignment = Alignment.CenterVertically,
        ) {
            TextButton(
                enabled = prepared,
                onClick = {
                    player?.let { mediaPlayer ->
                        if (mediaPlayer.isPlaying) {
                            mediaPlayer.pause()
                            playing = false
                        } else {
                            mediaPlayer.start()
                            playing = true
                        }
                    }
                },
                colors = focusTextButtonColors(),
            ) {
                Text(if (playing) "Pause" else "Play")
            }
            TextButton(
                enabled = prepared,
                onClick = {
                    player?.let { mediaPlayer ->
                        mediaPlayer.seekTo(0)
                        mediaPlayer.start()
                        playing = true
                    }
                },
                colors = focusTextButtonColors(),
            ) {
                Text("Restart")
            }
        }
    }
}

@Composable
private fun RelatedNodesSection(
    groups: List<RelatedNodeGroup>,
    expandedGroupKey: String,
    sourceMapPath: String,
    sourceNodeId: String,
    onToggleGroup: (RelatedNodeGroup) -> Unit,
    onOpenRelatedNode: (RelatedNodeEntry) -> Unit,
) {
    val expandedGroup = groups.firstOrNull { group ->
        relatedNodeGroupKey(sourceMapPath, sourceNodeId, group.relationLabel) == expandedGroupKey
    }
    Column(
        modifier = Modifier.fillMaxWidth(),
        verticalArrangement = Arrangement.spacedBy(8.dp),
    ) {
        Row(
            modifier = Modifier.horizontalScroll(rememberScrollState()),
            horizontalArrangement = Arrangement.spacedBy(8.dp),
            verticalAlignment = Alignment.CenterVertically,
        ) {
            groups.forEach { group ->
                val expanded = group == expandedGroup
                RelatedNodeTogglePill(
                    group = group,
                    expanded = expanded,
                    onClick = { onToggleGroup(group) },
                )
            }
        }
        expandedGroup?.let { group ->
            RelatedNodePanel(group = group, onOpenRelatedNode = onOpenRelatedNode)
        }
    }
}

@Composable
private fun RelatedNodeTogglePill(
    group: RelatedNodeGroup,
    expanded: Boolean,
    onClick: () -> Unit,
) {
    val palette = LocalFocusPalette.current
    val shape = CircleShape
    val label = relatedNodeToggleLabel(group, expanded)
    Row(
        modifier = Modifier
            .clip(shape)
            .background(if (expanded) palette.accentSoft else palette.panelBackground)
            .border(BorderStroke(1.dp, if (expanded) palette.accentBorderStrong else palette.border), shape)
            .semantics {
                contentDescription = label
                stateDescription = if (expanded) "Expanded" else "Collapsed"
            }
            .clickable(
                onClickLabel = label,
                role = Role.Button,
                onClick = onClick,
            )
            .heightIn(min = 32.dp)
            .padding(horizontal = 12.dp, vertical = 6.dp),
        horizontalArrangement = Arrangement.spacedBy(8.dp),
        verticalAlignment = Alignment.CenterVertically,
    ) {
        Text(
            text = group.relationLabel,
            style = MaterialTheme.typography.labelMedium,
            fontWeight = FontWeight.SemiBold,
            color = if (expanded) palette.accentStrong else palette.text,
            maxLines = 1,
            overflow = TextOverflow.Ellipsis,
            modifier = Modifier.widthIn(max = 180.dp),
        )
        Text(
            text = group.entries.size.toString(),
            style = MaterialTheme.typography.labelSmall,
            fontWeight = FontWeight.Bold,
            color = palette.accentStrong,
            modifier = Modifier
                .clip(CircleShape)
                .background(palette.panelMuted)
                .padding(horizontal = 7.dp, vertical = 3.dp),
        )
    }
}

@Composable
private fun RelatedNodePanel(
    group: RelatedNodeGroup,
    onOpenRelatedNode: (RelatedNodeEntry) -> Unit,
) {
    val palette = LocalFocusPalette.current
    Column(
        modifier = Modifier
            .fillMaxWidth()
            .clip(RoundedCornerShape(14.dp))
            .background(palette.panelMuted)
            .border(BorderStroke(1.dp, palette.accentBorder), RoundedCornerShape(14.dp))
            .padding(10.dp),
        verticalArrangement = Arrangement.spacedBy(7.dp),
    ) {
        group.entries.forEach { entry ->
            RelatedNodeRow(entry = entry, onOpenRelatedNode = { onOpenRelatedNode(entry) })
        }
    }
}

@Composable
private fun RelatedNodeRow(
    entry: RelatedNodeEntry,
    onOpenRelatedNode: () -> Unit,
) {
    val palette = LocalFocusPalette.current
    val shape = RoundedCornerShape(12.dp)
    val label = relatedNodeEntryActionLabel(entry)
    Row(
        modifier = Modifier
            .fillMaxWidth()
            .clip(shape)
            .background(palette.panelBackground)
            .border(BorderStroke(1.dp, palette.accentBorder), shape)
            .clickable(
                onClickLabel = label,
                role = Role.Button,
                onClick = onOpenRelatedNode,
            )
            .padding(horizontal = 12.dp, vertical = 10.dp),
        verticalAlignment = Alignment.Top,
    ) {
        Column(
            modifier = Modifier.weight(1f),
            verticalArrangement = Arrangement.spacedBy(2.dp),
        ) {
            FocusInlineText(
                text = entry.nodeName,
                style = MaterialTheme.typography.titleSmall.copy(fontWeight = FontWeight.SemiBold),
                maxLines = 2,
                overflow = TextOverflow.Ellipsis,
                onPlainClick = onOpenRelatedNode,
            )
            Text(
                text = entry.mapName,
                style = MaterialTheme.typography.bodySmall,
                color = palette.accentStrong,
                maxLines = 1,
                overflow = TextOverflow.Ellipsis,
            )
            FocusInlinePath(
                pathSegments = entry.nodePathSegments,
                style = MaterialTheme.typography.bodySmall,
                color = palette.muted,
                maxLines = 2,
                overflow = TextOverflow.Ellipsis,
                onPlainClick = onOpenRelatedNode,
            )
        }
    }
}

@Composable
private fun NodeRow(
    item: VisibleNode,
    isMapRoot: Boolean,
    isFocusedNode: Boolean,
    onNodeClick: () -> Unit,
    onSetTaskState: (TaskState) -> Unit,
    onToggleStarred: () -> Unit,
    onToggleCollapsed: () -> Unit,
    onDelete: () -> Unit,
) {
    val node = item.node
    val isRoot = isMapRoot
    val palette = LocalFocusPalette.current
    val nodeName = MapQueries.normalizeNodeDisplayText(node.name)
    val plainNodeName = plainInlineDisplayText(nodeName)
    FocusCard(
        modifier = Modifier
            .fillMaxWidth()
            .padding(start = (item.depth * 14).coerceAtMost(72).dp),
        onClick = onNodeClick,
    ) {
        Column(
            modifier = Modifier.padding(12.dp),
            verticalArrangement = Arrangement.spacedBy(9.dp),
        ) {
            Row(horizontalArrangement = Arrangement.spacedBy(10.dp), verticalAlignment = Alignment.Top) {
                CollapseToggleButton(
                    node = node,
                    collapsed = item.collapsed,
                    hasVisibleChildren = item.hasVisibleChildren,
                    onToggle = onToggleCollapsed,
                    modifier = Modifier.padding(top = 1.dp),
                )
                TaskMarkerButton(
                    taskState = node.taskState,
                    enabled = canQuickMarkTodo(isRoot = isRoot, node = node),
                    onMarkTodo = { onSetTaskState(TaskState.Todo) },
                    modifier = Modifier.padding(top = 1.dp),
                )
                Column(modifier = Modifier.weight(1f)) {
                    Row(
                        modifier = Modifier.fillMaxWidth(),
                        horizontalArrangement = Arrangement.spacedBy(6.dp),
                        verticalAlignment = Alignment.Top,
                    ) {
                        FocusInlineText(
                            text = nodeName,
                            style = (if (isRoot) MaterialTheme.typography.titleLarge else MaterialTheme.typography.titleMedium)
                                .copy(fontWeight = if (isRoot) FontWeight.Bold else FontWeight.SemiBold),
                            maxLines = 3,
                            overflow = TextOverflow.Ellipsis,
                            modifier = Modifier.weight(1f),
                            color = if (isFocusedNode) palette.accentStrong else palette.text,
                            onPlainClick = onNodeClick,
                        )
                        if (canToggleStar(isRoot = isRoot, node = node)) {
                            FocusIconButton(
                                imageVector = if (node.starred) Icons.Filled.Star else Icons.Filled.StarBorder,
                                contentDescription = starActionLabel(node),
                                onClick = onToggleStarred,
                                selected = node.starred,
                                modifier = Modifier.semantics {
                                    stateDescription = if (node.starred) "Starred" else "Not starred"
                                },
                            )
                        } else if (node.starred) {
                            Icon(
                                imageVector = Icons.Filled.Star,
                                contentDescription = "Starred",
                                tint = palette.accentStrong,
                                modifier = Modifier.size(19.dp),
                            )
                        }
                        if (!isRoot && !node.isIdeaTag) {
                            DeleteOverflowMenu(
                                targetLabel = plainNodeName,
                                onDelete = onDelete,
                            )
                        }
                    }
                    if (node.isIdeaTag) {
                        Text("Idea", style = MaterialTheme.typography.bodySmall, color = palette.muted)
                    }
                }
            }
            if (shouldShowTaskStateModifiers(isRoot = isRoot, node = node, isFocusedNode = isFocusedNode)) {
                TaskStateButtons(currentState = node.taskState, onSetTaskState = onSetTaskState)
            }
        }
    }
}

@Composable
private fun CollapseToggleButton(
    node: Node,
    collapsed: Boolean,
    hasVisibleChildren: Boolean,
    onToggle: () -> Unit,
    modifier: Modifier = Modifier,
) {
    val palette = LocalFocusPalette.current
    val shape = CircleShape
    if (!hasVisibleChildren) {
        Spacer(modifier = modifier.size(30.dp))
        return
    }

    val label = collapseActionLabel(node, collapsed)
    Box(
        modifier = modifier
            .size(30.dp)
            .clip(shape)
            .background(if (collapsed) Color.Transparent else palette.accentSoft)
            .border(BorderStroke(1.dp, if (collapsed) palette.accentBorder else palette.accentBorderStrong), shape)
            .semantics {
                contentDescription = label
                stateDescription = if (collapsed) "Collapsed" else "Expanded"
            }
            .clickable(
                onClickLabel = label,
                role = Role.Button,
                onClick = onToggle,
            ),
        contentAlignment = Alignment.Center,
    ) {
        Icon(
            imageVector = if (collapsed) Icons.Filled.Add else Icons.Filled.Remove,
            contentDescription = null,
            tint = palette.accentStrong,
            modifier = Modifier.size(16.dp),
        )
    }
}

@Composable
private fun TaskMarkerButton(
    taskState: TaskState,
    enabled: Boolean,
    onMarkTodo: () -> Unit,
    modifier: Modifier = Modifier,
) {
    val shape = CircleShape
    if (!enabled) {
        TaskDot(taskState = taskState, modifier = modifier.padding(top = 5.dp))
        return
    }

    Box(
        modifier = modifier
            .size(24.dp)
            .clip(shape)
            .semantics {
                contentDescription = "Mark as Todo"
                stateDescription = "Not selected"
            }
            .clickable(
                onClickLabel = "Mark as Todo",
                role = Role.Button,
                onClick = onMarkTodo,
            ),
        contentAlignment = Alignment.Center,
    ) {
        TaskDot(taskState = taskState)
    }
}

@Composable
private fun DeleteOverflowMenu(
    targetLabel: String,
    enabled: Boolean = true,
    onDelete: () -> Unit,
) {
    var expanded by remember { mutableStateOf(false) }
    val palette = LocalFocusPalette.current
    Box {
        FocusIconButton(
            imageVector = Icons.Filled.MoreVert,
            contentDescription = "More actions for $targetLabel",
            onClick = { expanded = true },
            enabled = enabled,
        )
        DropdownMenu(
            expanded = expanded,
            onDismissRequest = { expanded = false },
            containerColor = palette.panelBackground,
        ) {
            DropdownMenuItem(
                text = { Text("Delete", color = palette.danger) },
                leadingIcon = {
                    Icon(
                        imageVector = Icons.Filled.Delete,
                        contentDescription = null,
                        tint = palette.danger,
                    )
                },
                onClick = {
                    expanded = false
                    onDelete()
                },
                enabled = enabled,
            )
        }
    }
}

@Composable
private fun TaskStateButtons(
    currentState: TaskState,
    onSetTaskState: (TaskState) -> Unit,
) {
    Row(
        modifier = Modifier.horizontalScroll(rememberScrollState()),
        horizontalArrangement = Arrangement.spacedBy(9.dp),
        verticalAlignment = Alignment.CenterVertically,
    ) {
        listOf(TaskState.None, TaskState.Todo, TaskState.Doing, TaskState.Done).forEach { state ->
            TaskStateButton(
                state = state,
                selected = currentState == state,
                onClick = { onSetTaskState(state) },
            )
        }
    }
}

@Composable
private fun TaskStateButton(
    state: TaskState,
    selected: Boolean,
    onClick: () -> Unit,
) {
    val palette = LocalFocusPalette.current
    val shape = CircleShape
    val label = taskStateActionLabel(state)
    Box(
        modifier = Modifier
            .size(34.dp)
            .clip(shape)
            .background(if (selected) palette.accentSoft else Color.Transparent)
            .border(
                BorderStroke(
                    width = if (selected) 1.dp else 0.dp,
                    color = if (selected) palette.accentBorderStrong else Color.Transparent,
                ),
                shape,
            )
            .semantics {
                contentDescription = label
                stateDescription = if (selected) "Selected" else "Not selected"
            }
            .clickable(
                onClickLabel = label,
                role = Role.Button,
                onClick = onClick,
            ),
        contentAlignment = Alignment.Center,
    ) {
        TaskDot(taskState = state, selected = selected)
    }
}

private fun taskStateActionLabel(state: TaskState): String =
    if (state == TaskState.None) "Clear task state" else "Set ${focusTaskLabel(state)}"

internal fun canQuickMarkTodo(isRoot: Boolean, node: Node): Boolean =
    !isRoot && node.canChangeTaskState && node.taskState == TaskState.None

internal fun shouldShowTaskStateModifiers(isRoot: Boolean, node: Node, isFocusedNode: Boolean): Boolean =
    !isRoot && isFocusedNode && node.canChangeTaskState && node.taskState.isTask

internal fun canToggleStar(isRoot: Boolean, node: Node): Boolean =
    !isRoot && !node.isIdeaTag

internal fun starActionLabel(node: Node): String {
    val action = if (node.starred) "Unstar" else "Star"
    return "$action ${plainInlineDisplayText(node.name)}"
}

@Composable
private fun CreateMapDialog(
    loading: Boolean,
    onDismiss: () -> Unit,
    onCreate: (String) -> Unit,
) {
    var name by remember { mutableStateOf("") }
    FocusDialog(
        title = "Create map",
        onDismiss = onDismiss,
        confirmButton = {
            TextButton(
                enabled = !loading && name.trim().isNotEmpty(),
                onClick = { onCreate(name) },
                colors = focusTextButtonColors(),
            ) {
                Text("Create")
            }
        },
        dismissButton = {
            TextButton(onClick = onDismiss, colors = focusTextButtonColors()) { Text("Cancel") }
        },
    ) {
        FocusTextField(value = name, onValueChange = { name = it }, label = "Map name")
    }
}

internal enum class VoiceRecordingStatus { Idle, Starting, Recording, Saving }

internal data class VoiceRecordingUiState(
    val status: VoiceRecordingStatus = VoiceRecordingStatus.Idle,
    val startedAtMillis: Long = 0L,
    val elapsedMillis: Long = 0L,
    val reachedLimit: Boolean = false,
    val errorMessage: String = "",
) {
    val isActive: Boolean
        get() = status == VoiceRecordingStatus.Starting ||
            status == VoiceRecordingStatus.Recording ||
            status == VoiceRecordingStatus.Saving
}

@Composable
private fun EditNodeDialog(
    filePath: String,
    node: Node,
    attachmentUploadState: AttachmentUploadUiState,
    attachmentDeleteState: AttachmentDeleteUiState,
    onDismiss: () -> Unit,
    onSave: (String) -> Unit,
    onAttachFile: (Uri) -> Unit,
    onAttachVoiceNote: (String, String, String, ByteArray) -> Unit,
    onOpenAttachment: (NodeAttachment) -> Unit,
    onDeleteAttachment: (NodeAttachment) -> Unit,
) {
    val context = LocalContext.current
    val coroutineScope = rememberCoroutineScope()
    var text by remember(node.uniqueIdentifier) { mutableStateOf(node.name) }
    var voiceState by remember(node.uniqueIdentifier) { mutableStateOf(VoiceRecordingUiState()) }
    val voiceRecorder = remember(node.uniqueIdentifier) {
        AndroidVoiceNoteRecorder(context.applicationContext)
    }
    val attachFileLauncher = rememberLauncherForActivityResult(ActivityResultContracts.OpenDocument()) { uri ->
        if (uri != null) {
            onAttachFile(uri)
        }
    }
    fun startVoiceRecording() {
        voiceState = VoiceRecordingUiState(status = VoiceRecordingStatus.Starting)
        voiceRecorder.start()
            .onSuccess {
                voiceState = VoiceRecordingUiState(
                    status = VoiceRecordingStatus.Recording,
                    startedAtMillis = System.currentTimeMillis(),
                )
            }
            .onFailure { error ->
                voiceState = VoiceRecordingUiState(
                    errorMessage = error.message ?: "Could not start voice recording. Check microphone permission.",
                )
            }
    }
    val voicePermissionLauncher = rememberLauncherForActivityResult(ActivityResultContracts.RequestPermission()) { granted ->
        if (granted) {
            startVoiceRecording()
        } else {
            voiceState = VoiceRecordingUiState(errorMessage = "Microphone permission is required to record voice notes.")
        }
    }
    fun saveVoiceRecording(reachedLimit: Boolean = false) {
        if (voiceState.status != VoiceRecordingStatus.Recording) return
        val now = Instant.now()
        voiceState = voiceState.copy(status = VoiceRecordingStatus.Saving, reachedLimit = reachedLimit)
        coroutineScope.launch {
            val result = withContext(Dispatchers.IO) {
                voiceRecorder.stopAndSave().mapCatching { file ->
                    try {
                        val bytes = file.readBytes()
                        if (bytes.isEmpty()) {
                            error("Voice recording was empty. Try recording again.")
                        }
                        AttachmentUploads.validationError(bytes.size.toLong(), VoiceNotes.MediaType, VoiceNotes.fileName(now))
                            ?.let { error(it) }
                        bytes
                    } finally {
                        file.delete()
                    }
                }
            }
            result
                .onSuccess { bytes ->
                    onAttachVoiceNote(
                        VoiceNotes.displayName(now),
                        VoiceNotes.fileName(now),
                        VoiceNotes.MediaType,
                        bytes,
                    )
                    voiceState = VoiceRecordingUiState()
                }
                .onFailure { error ->
                    voiceState = VoiceRecordingUiState(
                        errorMessage = error.message ?: "Could not save voice recording.",
                    )
                }
        }
    }
    fun cancelVoiceRecording() {
        voiceRecorder.cancel()
        voiceState = VoiceRecordingUiState()
    }
    LaunchedEffect(voiceState.status, voiceState.startedAtMillis) {
        if (voiceState.status == VoiceRecordingStatus.Recording) {
            while (voiceState.status == VoiceRecordingStatus.Recording) {
                val elapsed = (System.currentTimeMillis() - voiceState.startedAtMillis)
                    .coerceIn(0L, VoiceNotes.MaxDurationMillis)
                voiceState = voiceState.copy(elapsedMillis = elapsed)
                if (elapsed >= VoiceNotes.MaxDurationMillis) {
                    saveVoiceRecording(reachedLimit = true)
                    break
                }
                delay(500)
            }
        }
    }
    DisposableEffect(node.uniqueIdentifier) {
        onDispose {
            voiceRecorder.cleanup()
        }
    }
    FocusDialog(
        title = "Edit node",
        onDismiss = onDismiss,
        confirmButton = {
            TextButton(enabled = text.trim().isNotEmpty(), onClick = { onSave(text) }, colors = focusTextButtonColors()) {
                Text("Save")
            }
        },
        dismissButton = {
            TextButton(onClick = onDismiss, colors = focusTextButtonColors()) { Text("Cancel") }
        },
    ) {
        Column(verticalArrangement = Arrangement.spacedBy(12.dp)) {
            FocusTextField(
                value = text,
                onValueChange = { text = it },
                label = "Text",
                minLines = 3,
            )
            AttachmentListSection(
                attachments = nodeAttachments(node),
                onOpenAttachment = onOpenAttachment,
                onDeleteAttachment = if (canDeleteAttachmentsFromNode(node)) onDeleteAttachment else null,
                isDeletingAttachment = { attachment ->
                    attachmentDeleteState.forTarget(filePath, node.uniqueIdentifier, attachment.id).deleting
                },
                deleteErrorForAttachment = { attachment ->
                    attachmentDeleteState.forTarget(filePath, node.uniqueIdentifier, attachment.id).errorMessage
                },
            )
            AttachmentUploadControls(
                uploadState = attachmentUploadState,
                voiceState = voiceState,
                attachEnabled = canStartAttachmentUpload(node, attachmentUploadState, voiceState),
                voiceStartEnabled = canStartVoiceRecording(node, attachmentUploadState, voiceState),
                onAttachClick = { attachFileLauncher.launch(AttachmentUploads.PickerMimeTypes) },
                onStartVoiceClick = {
                    if (ContextCompat.checkSelfPermission(context, Manifest.permission.RECORD_AUDIO) == PackageManager.PERMISSION_GRANTED) {
                        startVoiceRecording()
                    } else {
                        voicePermissionLauncher.launch(Manifest.permission.RECORD_AUDIO)
                    }
                },
                onSaveVoiceClick = { saveVoiceRecording() },
                onCancelVoiceClick = { cancelVoiceRecording() },
            )
        }
    }
}

@Composable
private fun AttachmentUploadControls(
    uploadState: AttachmentUploadUiState,
    voiceState: VoiceRecordingUiState,
    attachEnabled: Boolean,
    voiceStartEnabled: Boolean,
    onAttachClick: () -> Unit,
    onStartVoiceClick: () -> Unit,
    onSaveVoiceClick: () -> Unit,
    onCancelVoiceClick: () -> Unit,
) {
    val palette = LocalFocusPalette.current
    Column(verticalArrangement = Arrangement.spacedBy(6.dp)) {
        Row(
            horizontalArrangement = Arrangement.spacedBy(8.dp),
            verticalAlignment = Alignment.CenterVertically,
        ) {
            OutlinedButton(
                enabled = attachEnabled,
                onClick = onAttachClick,
                colors = ButtonDefaults.outlinedButtonColors(
                    contentColor = if (attachEnabled) palette.accent else palette.muted,
                ),
                border = BorderStroke(1.dp, palette.border),
            ) {
                Icon(
                    imageVector = Icons.Filled.AttachFile,
                    contentDescription = null,
                    modifier = Modifier.size(18.dp),
                )
                Spacer(modifier = Modifier.width(6.dp))
                Text(if (uploadState.uploading) "Uploading..." else "Attach file")
            }
            if (!voiceState.isActive) {
                OutlinedButton(
                    enabled = voiceStartEnabled,
                    onClick = onStartVoiceClick,
                    colors = ButtonDefaults.outlinedButtonColors(contentColor = palette.accent),
                    border = BorderStroke(1.dp, palette.border),
                ) {
                    Text("Voice note")
                }
            }
        }
        if (voiceState.isActive) {
            VoiceRecordingControls(
                voiceState = voiceState,
                onSaveVoiceClick = onSaveVoiceClick,
                onCancelVoiceClick = onCancelVoiceClick,
            )
        }
        Text(
            text = "Images, audio, PDF, text - max 10 MB",
            style = MaterialTheme.typography.labelMedium,
            color = palette.muted,
        )
        if (uploadState.errorMessage.isNotBlank()) {
            Text(
                text = uploadState.errorMessage,
                style = MaterialTheme.typography.bodySmall,
                color = palette.danger,
            )
        }
        if (voiceState.errorMessage.isNotBlank()) {
            Text(
                text = voiceState.errorMessage,
                style = MaterialTheme.typography.bodySmall,
                color = palette.danger,
            )
        }
    }
}

@Composable
private fun VoiceRecordingControls(
    voiceState: VoiceRecordingUiState,
    onSaveVoiceClick: () -> Unit,
    onCancelVoiceClick: () -> Unit,
) {
    val palette = LocalFocusPalette.current
    Row(
        horizontalArrangement = Arrangement.spacedBy(8.dp),
        verticalAlignment = Alignment.CenterVertically,
    ) {
        Box(
            modifier = Modifier
                .size(10.dp)
                .clip(CircleShape)
                .background(palette.danger),
        )
        Text(
            text = when (voiceState.status) {
                VoiceRecordingStatus.Starting -> "Starting..."
                else -> VoiceNotes.formatElapsed(voiceState.elapsedMillis)
            },
            style = MaterialTheme.typography.bodyMedium,
            color = palette.text,
        )
        if (voiceState.reachedLimit) {
            Text(
                text = "5 minute limit reached",
                style = MaterialTheme.typography.labelMedium,
                color = palette.muted,
            )
        }
        TextButton(
            enabled = voiceState.status == VoiceRecordingStatus.Recording,
            onClick = onSaveVoiceClick,
            colors = focusTextButtonColors(),
        ) {
            Text(if (voiceState.status == VoiceRecordingStatus.Saving) "Saving..." else "Save")
        }
        TextButton(
            enabled = voiceState.status != VoiceRecordingStatus.Saving,
            onClick = onCancelVoiceClick,
            colors = focusTextButtonColors(),
        ) {
            Text("Cancel")
        }
    }
}

@Composable
private fun AddChildDialog(
    target: AddChildTarget,
    onDismiss: () -> Unit,
    onSave: (String) -> Unit,
) {
    var text by remember(target.parent.uniqueIdentifier, target.asTask) { mutableStateOf("") }
    var focusRequestVersion by remember(target.parent.uniqueIdentifier, target.asTask) { mutableStateOf(0) }
    val focusRequester = remember { FocusRequester() }
    LaunchedEffect(target.parent.uniqueIdentifier, target.asTask, focusRequestVersion) {
        if (!target.asTask) {
            focusRequester.requestFocus()
        }
    }
    FocusDialog(
        title = addChildDialogTitle(target.asTask),
        onDismiss = onDismiss,
        confirmButton = {
            TextButton(
                enabled = text.trim().isNotEmpty(),
                onClick = {
                    onSave(text.trim())
                    if (!shouldDismissAfterAddChild(target.asTask)) {
                        text = ""
                        focusRequestVersion += 1
                    }
                },
                colors = focusTextButtonColors(),
            ) {
                Text("Add")
            }
        },
        dismissButton = {
            TextButton(onClick = onDismiss, colors = focusTextButtonColors()) { Text("Cancel") }
        },
    ) {
        FocusTextField(
            value = text,
            onValueChange = { text = it },
            label = addChildTextFieldLabel(target.asTask),
            placeholder = addChildTextFieldPlaceholder(target.asTask),
            focusRequester = focusRequester,
            minLines = 2,
        )
    }
}

@Composable
private fun DeleteNodeDialog(
    node: Node,
    onDismiss: () -> Unit,
    onConfirm: () -> Unit,
) {
    FocusDialog(
        title = "Delete node",
        onDismiss = onDismiss,
        confirmButton = {
            DestructiveTextButton(onClick = onConfirm, label = "Delete")
        },
        dismissButton = {
            TextButton(onClick = onDismiss, colors = focusTextButtonColors()) { Text("Cancel") }
        },
    ) {
        Text("Delete \"${plainInlineDisplayText(node.name)}\" and its children?")
    }
}

@Composable
private fun DeleteAttachmentDialog(
    target: AttachmentDeleteTarget,
    deleting: Boolean,
    onDismiss: () -> Unit,
    onConfirm: () -> Unit,
) {
    FocusDialog(
        title = "Delete attachment",
        onDismiss = onDismiss,
        confirmButton = {
            DestructiveTextButton(
                enabled = !deleting,
                onClick = onConfirm,
                label = if (deleting) "Deleting..." else "Delete",
            )
        },
        dismissButton = {
            TextButton(onClick = onDismiss, colors = focusTextButtonColors()) { Text("Cancel") }
        },
    ) {
        Text(deleteAttachmentConfirmationText(target.displayName))
    }
}

@Composable
private fun DeleteMapDialog(
    snapshot: MapSnapshot,
    onDismiss: () -> Unit,
    onConfirm: () -> Unit,
) {
    FocusDialog(
        title = "Delete map",
        onDismiss = onDismiss,
        confirmButton = {
            DestructiveTextButton(onClick = onConfirm, label = "Delete")
        },
        dismissButton = {
            TextButton(onClick = onDismiss, colors = focusTextButtonColors()) { Text("Cancel") }
        },
    ) {
        Text("Delete \"${snapshot.mapName}\" from GitHub and this device?")
    }
}

@Composable
private fun FocusDialog(
    title: String,
    onDismiss: () -> Unit,
    confirmButton: @Composable () -> Unit,
    dismissButton: @Composable () -> Unit,
    text: @Composable () -> Unit,
) {
    val palette = LocalFocusPalette.current
    AlertDialog(
        onDismissRequest = onDismiss,
        title = { Text(title) },
        text = text,
        confirmButton = confirmButton,
        dismissButton = dismissButton,
        containerColor = palette.panelBackground,
        titleContentColor = palette.text,
        textContentColor = palette.text,
        shape = RoundedCornerShape(16.dp),
    )
}

@Composable
private fun FocusTextField(
    value: String,
    onValueChange: (String) -> Unit,
    label: String,
    modifier: Modifier = Modifier.fillMaxWidth(),
    placeholder: String = "",
    focusRequester: FocusRequester? = null,
    minLines: Int = 1,
) {
    val fieldModifier = focusRequester?.let { modifier.focusRequester(it) } ?: modifier
    OutlinedTextField(
        value = value,
        onValueChange = onValueChange,
        label = { Text(label) },
        placeholder = {
            if (placeholder.isNotBlank()) {
                Text(placeholder)
            }
        },
        minLines = minLines,
        modifier = fieldModifier,
        shape = RoundedCornerShape(10.dp),
        colors = focusTextFieldColors(),
    )
}

@Composable
private fun focusTextFieldColors() = OutlinedTextFieldDefaults.colors(
    focusedTextColor = LocalFocusPalette.current.text,
    unfocusedTextColor = LocalFocusPalette.current.text,
    focusedContainerColor = LocalFocusPalette.current.inputBackground,
    unfocusedContainerColor = LocalFocusPalette.current.inputBackground,
    focusedBorderColor = LocalFocusPalette.current.accent,
    unfocusedBorderColor = LocalFocusPalette.current.border,
    focusedLabelColor = LocalFocusPalette.current.accentStrong,
    unfocusedLabelColor = LocalFocusPalette.current.muted,
    cursorColor = LocalFocusPalette.current.accent,
)

@Composable
private fun PrimaryActionButton(
    label: String,
    imageVector: ImageVector,
    enabled: Boolean,
    onClick: () -> Unit,
    modifier: Modifier = Modifier,
) {
    val palette = LocalFocusPalette.current
    Button(
        enabled = enabled,
        onClick = onClick,
        modifier = modifier.heightIn(min = 46.dp),
        shape = RoundedCornerShape(12.dp),
        colors = ButtonDefaults.buttonColors(
            containerColor = palette.accent,
            contentColor = Color.White,
            disabledContainerColor = palette.panelMuted,
            disabledContentColor = palette.muted,
        ),
    ) {
        Icon(imageVector = imageVector, contentDescription = null, modifier = Modifier.size(18.dp))
        Spacer(Modifier.width(8.dp))
        Text(label)
    }
}

@Composable
private fun SecondaryActionButton(
    label: String,
    imageVector: ImageVector,
    enabled: Boolean,
    onClick: () -> Unit,
    modifier: Modifier = Modifier,
) {
    val palette = LocalFocusPalette.current
    OutlinedButton(
        enabled = enabled,
        onClick = onClick,
        modifier = modifier.heightIn(min = 46.dp),
        shape = RoundedCornerShape(12.dp),
        border = BorderStroke(1.dp, palette.accentBorder),
        colors = ButtonDefaults.outlinedButtonColors(
            containerColor = Color.Transparent,
            contentColor = palette.accentStrong,
            disabledContentColor = palette.muted.copy(alpha = 0.45f),
        ),
    ) {
        Icon(imageVector = imageVector, contentDescription = null, modifier = Modifier.size(18.dp))
        Spacer(Modifier.width(8.dp))
        Text(label)
    }
}

@Composable
private fun DangerActionButton(
    label: String,
    imageVector: ImageVector,
    enabled: Boolean,
    onClick: () -> Unit,
    modifier: Modifier = Modifier,
) {
    val palette = LocalFocusPalette.current
    OutlinedButton(
        enabled = enabled,
        onClick = onClick,
        modifier = modifier.heightIn(min = 46.dp),
        shape = RoundedCornerShape(12.dp),
        border = BorderStroke(1.dp, palette.danger.copy(alpha = 0.45f)),
        colors = ButtonDefaults.outlinedButtonColors(
            containerColor = Color.Transparent,
            contentColor = palette.danger,
            disabledContentColor = palette.muted.copy(alpha = 0.45f),
        ),
    ) {
        Icon(imageVector = imageVector, contentDescription = null, modifier = Modifier.size(18.dp))
        Spacer(Modifier.width(8.dp))
        Text(label)
    }
}

@Composable
private fun DestructiveTextButton(
    onClick: () -> Unit,
    label: String,
    enabled: Boolean = true,
) {
    TextButton(
        enabled = enabled,
        onClick = onClick,
        colors = ButtonDefaults.textButtonColors(
            contentColor = LocalFocusPalette.current.danger,
            disabledContentColor = LocalFocusPalette.current.muted.copy(alpha = 0.45f),
        ),
    ) {
        Text(label)
    }
}

@Composable
private fun focusTextButtonColors() = ButtonDefaults.textButtonColors(
    contentColor = LocalFocusPalette.current.accentStrong,
    disabledContentColor = LocalFocusPalette.current.muted.copy(alpha = 0.45f),
)

internal data class VisibleNode(
    val node: Node,
    val depth: Int,
    val hidesDone: Boolean,
    val hasVisibleChildren: Boolean,
    val collapsed: Boolean,
)

internal data class RelatedNodeGroup(
    val relationLabel: String,
    val entries: List<RelatedNodeEntry>,
)

internal enum class FocusedNodeClickAction {
    FocusNode,
    EditText,
    OpenPrimaryAttachment,
}

internal data class AttachmentDeleteTarget(
    val filePath: String,
    val nodeId: String,
    val attachmentId: String,
    val displayName: String,
    val expectedRevision: String? = null,
)

private data class AddChildTarget(val parent: Node, val asTask: Boolean)

internal fun focusedVisibleNodes(
    document: MindMapDocument,
    focusedNodeId: String,
    collapsedOverrides: Map<String, Boolean> = emptyMap(),
): List<VisibleNode> {
    val focusedRecord = resolveFocusedNodeRecord(document, focusedNodeId)
    val focusedHideDone = MapQueries.getNodeHideDoneState(document, focusedRecord.node.uniqueIdentifier)
    return flattenVisibleNodes(focusedRecord.node, depth = 0, ancestorHidesDone = focusedHideDone, collapsedOverrides = collapsedOverrides)
}

internal fun resolveFocusedNodeRecord(document: MindMapDocument, focusedNodeId: String): NodeRecord {
    val rootId = document.rootNode.uniqueIdentifier
    return MapQueries.findNode(document, focusedNodeId.ifBlank { rootId })
        ?: MapQueries.findNode(document, rootId)
        ?: NodeRecord(
            node = document.rootNode,
            parent = null,
            pathSegments = listOf(MapQueries.normalizeNodeDisplayText(document.rootNode.name)),
            depth = 0,
        )
}

internal fun shouldShowFocusedHideDoneAction(document: MindMapDocument, focusedNodeId: String): Boolean {
    val focusedRecord = MapQueries.findNode(document, focusedNodeId) ?: return false
    if (focusedRecord.node.isIdeaTag) return false
    return MapQueries.getNodeHideDoneState(document, focusedNodeId) ||
        MapQueries.hasDoneDescendants(document, focusedNodeId)
}

internal fun focusedAddTargetNode(document: MindMapDocument, focusedNodeId: String): Node? {
    val focusedNode = resolveFocusedNodeRecord(document, focusedNodeId).node
    return focusedNode.takeIf { it.canEditText }
}

internal fun addChildActionLabel(parent: Node, asTask: Boolean): String {
    val kind = if (asTask) "task" else "note"
    return "Add $kind under ${plainInlineDisplayText(parent.name)}"
}

internal fun shouldDismissAfterAddChild(asTask: Boolean): Boolean = asTask

internal fun addChildDialogTitle(asTask: Boolean): String =
    if (asTask) "Add child task" else "Add child notes"

internal fun addChildTextFieldLabel(asTask: Boolean): String =
    if (asTask) "Child task text" else "Child note text"

internal fun addChildTextFieldPlaceholder(asTask: Boolean): String =
    if (asTask) "Describe the new task" else "Add a child note"

internal fun effectiveCollapsed(node: Node, collapsedOverrides: Map<String, Boolean>): Boolean =
    collapsedOverrides[node.uniqueIdentifier] ?: node.collapsed

internal fun toggleCollapsed(node: Node, collapsedOverrides: Map<String, Boolean>): Map<String, Boolean> =
    collapsedOverrides + (node.uniqueIdentifier to !effectiveCollapsed(node, collapsedOverrides))

internal fun collapseActionLabel(node: Node, collapsed: Boolean): String {
    val action = if (collapsed) "Expand" else "Collapse"
    return "$action ${plainInlineDisplayText(node.name)}"
}

internal fun focusAfterDeletingNode(
    document: MindMapDocument,
    focusedNodeId: String,
    deletedNodeId: String,
): String {
    if (focusedNodeId != deletedNodeId) return focusedNodeId
    val deletedRecord = MapQueries.findNode(document, deletedNodeId)
    return deletedRecord?.parent?.uniqueIdentifier ?: document.rootNode.uniqueIdentifier
}

internal fun shouldEditFocusedNode(
    clickedNodeId: String,
    focusedNodeId: String,
    canEditNode: Boolean,
): Boolean =
    canEditNode && clickedNodeId == focusedNodeId

internal fun focusedNodeClickAction(
    clickedNodeId: String,
    focusedNodeId: String,
    node: Node,
): FocusedNodeClickAction =
    when {
        clickedNodeId != focusedNodeId -> FocusedNodeClickAction.FocusNode
        openablePrimaryAttachment(node) != null -> FocusedNodeClickAction.OpenPrimaryAttachment
        node.canEditText -> FocusedNodeClickAction.EditText
        else -> FocusedNodeClickAction.FocusNode
    }

internal fun relatedNodeGroups(
    outgoing: List<RelatedNodeEntry>,
    backlinks: List<RelatedNodeEntry>,
): List<RelatedNodeGroup> {
    val groupsByLabel = linkedMapOf<String, MutableList<RelatedNodeEntry>>()
    (outgoing + backlinks).forEach { entry ->
        groupsByLabel.getOrPut(entry.relationLabel.ifBlank { "link" }) { mutableListOf() } += entry
    }
    return groupsByLabel
        .map { (label, entries) -> RelatedNodeGroup(label, entries) }
        .sortedWith(
            compareBy<RelatedNodeGroup> { it.relationLabel.startsWith("backlink") }
                .thenBy { it.relationLabel },
        )
}

internal fun relatedNodeGroupKey(mapPath: String, nodeId: String, relationLabel: String): String =
    listOf(mapPath, nodeId, relationLabel).joinToString(separator = "::")

internal fun relatedNodeToggleLabel(group: RelatedNodeGroup, expanded: Boolean): String {
    val action = if (expanded) "Hide" else "Show"
    return "$action ${group.relationLabel}"
}

internal fun relatedNodeEntryActionLabel(entry: RelatedNodeEntry): String =
    "Open ${plainInlineDisplayText(entry.nodeName)} in ${entry.mapName} via ${entry.relationLabel}"

internal fun resolveFocusRequestNodeId(document: MindMapDocument, requestedNodeId: String): String =
    MapQueries.findNode(document, requestedNodeId)?.node?.uniqueIdentifier
        ?: document.rootNode.uniqueIdentifier

internal fun nodeAttachments(node: Node): List<NodeAttachment> =
    node.metadata?.attachments.orEmpty()

internal fun isClipboardAttachmentNode(node: Node): Boolean =
    node.metadata?.source == NodeMetadataSources.ClipboardImage ||
        node.metadata?.source == NodeMetadataSources.ClipboardText

internal fun primaryNodeAttachment(node: Node): NodeAttachment? {
    val attachments = nodeAttachments(node)
    return attachments.firstOrNull { it.relativePath.isNotBlank() } ?: attachments.firstOrNull()
}

internal fun openablePrimaryAttachment(node: Node): NodeAttachment? =
    if (isClipboardAttachmentNode(node)) {
        primaryNodeAttachment(node)?.takeIf { AttachmentViewers.canView(it) }
    } else {
        null
    }

internal fun attachmentDisplayName(attachment: NodeAttachment): String =
    attachment.displayName.trim()
        .ifBlank { attachment.relativePath.trim() }
        .ifBlank { "Attachment" }

internal fun attachmentTypeLabel(attachment: NodeAttachment): String {
    val mediaType = attachment.mediaType.trim().lowercase()
    return when {
        mediaType.startsWith("audio/") -> "AUD"
        mediaType.startsWith("text/") -> "TXT"
        else -> "IMG"
    }
}

internal fun attachmentAccessibilityLabel(attachment: NodeAttachment): String =
    "Attachment ${attachmentDisplayName(attachment)}, ${attachmentTypeLabel(attachment)}"

internal fun canViewAttachment(attachment: NodeAttachment): Boolean =
    AttachmentViewers.canView(attachment)

internal fun canDeleteAttachment(attachment: NodeAttachment): Boolean =
    attachment.id.isNotBlank() && attachment.relativePath.isNotBlank()

internal fun canDeleteAttachmentsFromNode(node: Node): Boolean =
    node.canEditText

internal fun attachmentDeleteActionLabel(attachment: NodeAttachment): String =
    "Delete ${attachmentDisplayName(attachment)}"

internal fun deleteAttachmentConfirmationText(displayName: String): String =
    "Delete \"$displayName\"? This removes the file from the node and cannot be undone."

internal fun attachmentDeleteTarget(
    filePath: String,
    nodeId: String,
    attachment: NodeAttachment,
    expectedRevision: String? = null,
): AttachmentDeleteTarget =
    AttachmentDeleteTarget(
        filePath = filePath,
        nodeId = nodeId,
        attachmentId = attachment.id,
        displayName = attachmentDisplayName(attachment),
        expectedRevision = expectedRevision?.takeIf { it.isNotBlank() },
    )

internal fun viewerAttachmentDeleteTarget(
    snapshots: List<MapSnapshot>,
    viewerState: AttachmentViewerUiState,
): AttachmentDeleteTarget? {
    val request = viewerState.request ?: return null
    if (viewerState.loading || viewerState.errorMessage.isNotBlank() || viewerState.bytes == null) return null
    val snapshot = snapshots.firstOrNull { it.filePath == request.filePath } ?: return null
    val record = MapQueries.findNode(snapshot.document, request.nodeId) ?: return null
    if (!canDeleteAttachmentsFromNode(record.node)) return null
    val attachment = nodeAttachments(record.node).firstOrNull { candidate ->
        candidate.id == request.attachment.id ||
            (candidate.relativePath == request.attachment.relativePath && candidate.relativePath.isNotBlank())
    } ?: return null
    if (!canDeleteAttachment(attachment)) return null
    return attachmentDeleteTarget(
        filePath = request.filePath,
        nodeId = request.nodeId,
        attachment = attachment,
        expectedRevision = viewerState.versionToken,
    )
}

internal fun canAttachFiles(node: Node): Boolean =
    node.canEditText

internal fun canStartAttachmentUpload(
    node: Node,
    uploadState: AttachmentUploadUiState,
    voiceState: VoiceRecordingUiState = VoiceRecordingUiState(),
): Boolean =
    canAttachFiles(node) && !uploadState.uploading && !voiceState.isActive

internal fun canStartVoiceRecording(
    node: Node,
    uploadState: AttachmentUploadUiState,
    voiceState: VoiceRecordingUiState = VoiceRecordingUiState(),
): Boolean =
    canAttachFiles(node) && !uploadState.uploading && voiceState.status == VoiceRecordingStatus.Idle

internal fun flattenVisibleNodes(
    node: Node,
    depth: Int,
    ancestorHidesDone: Boolean,
    collapsedOverrides: Map<String, Boolean> = emptyMap(),
): List<VisibleNode> {
    val hideForChildren = MapQueries.getTreeHideDoneState(node, ancestorHidesDone)
    val children = MapQueries.getVisibleChildren(node, ancestorHidesDone)
    val collapsed = effectiveCollapsed(node, collapsedOverrides)
    val current = VisibleNode(
        node = node,
        depth = depth,
        hidesDone = hideForChildren,
        hasVisibleChildren = children.isNotEmpty(),
        collapsed = collapsed,
    )
    if (collapsed) {
        return listOf(current)
    }
    return listOf(current) + children.flatMap { child ->
        flattenVisibleNodes(child, depth + 1, hideForChildren, collapsedOverrides)
    }
}
