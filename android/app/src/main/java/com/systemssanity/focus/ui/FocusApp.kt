package com.systemssanity.focus.ui

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
import androidx.compose.material.icons.automirrored.filled.NoteAdd
import androidx.compose.material.icons.filled.Add
import androidx.compose.material.icons.filled.AddTask
import androidx.compose.material.icons.filled.DarkMode
import androidx.compose.material.icons.filled.Delete
import androidx.compose.material.icons.filled.Edit
import androidx.compose.material.icons.filled.LightMode
import androidx.compose.material.icons.filled.Map
import androidx.compose.material.icons.filled.Refresh
import androidx.compose.material.icons.filled.Settings
import androidx.compose.material.icons.filled.Star
import androidx.compose.material.icons.filled.Sync
import androidx.compose.material.icons.filled.Visibility
import androidx.compose.material.icons.filled.VisibilityOff
import androidx.compose.material3.AlertDialog
import androidx.compose.material3.Button
import androidx.compose.material3.ButtonDefaults
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
import androidx.compose.runtime.getValue
import androidx.compose.runtime.mutableStateOf
import androidx.compose.runtime.remember
import androidx.compose.runtime.setValue
import androidx.compose.ui.Alignment
import androidx.compose.ui.Modifier
import androidx.compose.ui.draw.clip
import androidx.compose.ui.graphics.Color
import androidx.compose.ui.graphics.vector.ImageVector
import androidx.compose.ui.text.font.FontWeight
import androidx.compose.ui.text.style.TextOverflow
import androidx.compose.ui.unit.dp
import androidx.lifecycle.viewmodel.compose.viewModel
import com.systemssanity.focus.data.local.RepoSettings
import com.systemssanity.focus.data.local.ThemePreference
import com.systemssanity.focus.domain.maps.MapQueries
import com.systemssanity.focus.domain.maps.TaskEntry
import com.systemssanity.focus.domain.maps.TaskFilter
import com.systemssanity.focus.domain.model.MapSnapshot
import com.systemssanity.focus.domain.model.Node
import com.systemssanity.focus.domain.model.TaskState

@Composable
fun FocusApp(viewModel: FocusViewModel = viewModel()) {
    val uiState = viewModel.uiState
    val selectedSnapshot = uiState.selectedMapFilePath
        ?.let { filePath -> uiState.snapshots.firstOrNull { it.filePath == filePath } }
        ?: uiState.snapshots.firstOrNull()

    FocusTheme(themePreference = uiState.uiPreferences.theme) {
        val palette = LocalFocusPalette.current
        Surface(
            modifier = Modifier.fillMaxSize(),
            color = palette.pageBackground,
            contentColor = palette.text,
        ) {
            var screen by remember { mutableStateOf(AppScreen.Connection) }
            Scaffold(
                containerColor = palette.pageBackground,
                topBar = {
                    FocusTopBar(
                        screen = screen,
                        themePreference = uiState.uiPreferences.theme,
                        statusMessage = uiState.statusMessage,
                        pendingCount = uiState.pendingCount,
                        onSyncRequested = viewModel::syncPendingNow,
                        onThemeToggle = {
                            viewModel.setThemePreference(
                                if (uiState.uiPreferences.theme == ThemePreference.Dark) {
                                    ThemePreference.Light
                                } else {
                                    ThemePreference.Dark
                                },
                            )
                        },
                        onScreenChanged = { screen = it },
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
                            onSave = viewModel::saveConnection,
                            onLoad = { viewModel.loadWorkspace(forceRefresh = true) },
                        )
                        AppScreen.Maps -> MapsScreen(
                            snapshots = uiState.snapshots,
                            loading = uiState.loading,
                            onOpenMap = { snapshot ->
                                viewModel.openMap(snapshot)
                                screen = AppScreen.Map
                            },
                            onCreateMap = viewModel::createMap,
                            onDeleteMap = viewModel::deleteMap,
                        )
                        AppScreen.Tasks -> TasksScreen(
                            snapshots = uiState.snapshots,
                            filter = uiState.taskFilter,
                            onFilterChanged = viewModel::setTaskFilter,
                            onOpenTask = { entry ->
                                uiState.snapshots.firstOrNull { it.filePath == entry.filePath }?.let { snapshot ->
                                    viewModel.openMap(snapshot)
                                    screen = AppScreen.Map
                                }
                            },
                            onSetTaskState = { entry, taskState ->
                                viewModel.setTaskState(entry.filePath, entry.nodeId, taskState)
                            },
                        )
                        AppScreen.Map -> selectedSnapshot?.let { snapshot ->
                            MapEditorScreen(
                                snapshot = snapshot,
                                onSetTaskState = viewModel::setTaskState,
                                onEditNode = viewModel::editNodeText,
                                onAddChild = viewModel::addChild,
                                onDeleteNode = viewModel::deleteNode,
                                onToggleHideDone = viewModel::toggleHideDone,
                            )
                        } ?: EmptyPanel("No map is loaded.")
                    }
                }
            }
        }
    }
}

private enum class AppScreen { Connection, Maps, Tasks, Map }

@Composable
private fun FocusTopBar(
    screen: AppScreen,
    themePreference: ThemePreference,
    statusMessage: String,
    pendingCount: Int,
    onSyncRequested: () -> Unit,
    onThemeToggle: () -> Unit,
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
                        text = if (pendingCount > 0) "$pendingCount pending change${if (pendingCount == 1) "" else "s"}" else statusMessage.ifBlank { "Ready" },
                        style = MaterialTheme.typography.bodySmall,
                        color = if (pendingCount > 0) palette.accentStrong else palette.muted,
                        maxLines = 1,
                        overflow = TextOverflow.Ellipsis,
                    )
                }
                FocusIconButton(
                    imageVector = if (pendingCount > 0) Icons.Filled.Sync else Icons.Filled.Refresh,
                    contentDescription = if (pendingCount > 0) "Sync pending changes" else "Sync status",
                    onClick = { if (pendingCount > 0) onSyncRequested() },
                    enabled = pendingCount > 0,
                    selected = pendingCount > 0,
                )
                FocusIconButton(
                    imageVector = if (themePreference == ThemePreference.Dark) Icons.Filled.LightMode else Icons.Filled.DarkMode,
                    contentDescription = if (themePreference == ThemePreference.Dark) "Switch to light theme" else "Switch to dark theme",
                    onClick = onThemeToggle,
                )
            }

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
                FocusNavTab(
                    label = "Connection",
                    imageVector = Icons.Filled.Settings,
                    selected = screen == AppScreen.Connection,
                    onClick = { onScreenChanged(AppScreen.Connection) },
                )
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
    onSave: (RepoSettings, String) -> Unit,
    onLoad: () -> Unit,
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
                PrimaryActionButton(
                    label = "Save connection and load",
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
            }
        }
    }
}

@Composable
private fun MapsScreen(
    snapshots: List<MapSnapshot>,
    loading: Boolean,
    onOpenMap: (MapSnapshot) -> Unit,
    onCreateMap: (String) -> Unit,
    onDeleteMap: (String) -> Unit,
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
            if (snapshots.isEmpty()) {
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
                .align(Alignment.BottomEnd)
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
private fun MapSummaryCard(
    snapshot: MapSnapshot,
    loading: Boolean,
    onOpenMap: () -> Unit,
    onDeleteMap: () -> Unit,
) {
    val palette = LocalFocusPalette.current
    val summary = MapQueries.buildMapSummary(snapshot)
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
                    Text(
                        summary.rootTitle,
                        style = MaterialTheme.typography.titleMedium,
                        fontWeight = FontWeight.SemiBold,
                        maxLines = 2,
                        overflow = TextOverflow.Ellipsis,
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
                    contentDescription = "Open ${summary.rootTitle}",
                    onClick = onOpenMap,
                    enabled = !loading,
                )
                FocusIconButton(
                    imageVector = Icons.Filled.Delete,
                    contentDescription = "Delete ${summary.rootTitle}",
                    onClick = onDeleteMap,
                    enabled = !loading,
                    destructive = true,
                )
            }
            TaskCountPills(summary.taskCounts.open, summary.taskCounts.todo, summary.taskCounts.doing, summary.taskCounts.done)
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
private fun TaskCountPills(open: Int, todo: Int, doing: Int, done: Int) {
    val palette = LocalFocusPalette.current
    Row(
        modifier = Modifier.horizontalScroll(rememberScrollState()),
        horizontalArrangement = Arrangement.spacedBy(8.dp),
    ) {
        FocusPill(label = "Open $open", toneColor = palette.taskTodo, leadingDot = true)
        FocusPill(label = "Todo $todo", toneColor = palette.taskTodo, leadingDot = true)
        FocusPill(label = "Doing $doing", toneColor = palette.taskDoing, leadingDot = true)
        FocusPill(label = "Done $done", toneColor = palette.taskDone, leadingDot = true)
    }
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
                    Text(
                        entry.nodeName,
                        style = MaterialTheme.typography.titleMedium,
                        fontWeight = FontWeight.SemiBold,
                        maxLines = 2,
                        overflow = TextOverflow.Ellipsis,
                    )
                    Text(entry.mapName, style = MaterialTheme.typography.bodyMedium, color = palette.accentStrong)
                    Text(
                        entry.nodePathSegments.joinToString(" > "),
                        style = MaterialTheme.typography.bodySmall,
                        color = palette.muted,
                        maxLines = 2,
                        overflow = TextOverflow.Ellipsis,
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
    onSetTaskState: (String, String, TaskState) -> Unit,
    onEditNode: (String, String, String) -> Unit,
    onAddChild: (String, String, String, Boolean) -> Unit,
    onDeleteNode: (String, String) -> Unit,
    onToggleHideDone: (String, String) -> Unit,
) {
    var editingNode by remember(snapshot.filePath) { mutableStateOf<Node?>(null) }
    var addingChild by remember(snapshot.filePath) { mutableStateOf<AddChildTarget?>(null) }
    var deleteCandidate by remember(snapshot.filePath) { mutableStateOf<Node?>(null) }
    val visibleNodes = remember(snapshot.document) {
        flattenVisibleNodes(snapshot.document.rootNode, depth = 0, ancestorHidesDone = false)
    }

    Box(modifier = Modifier.fillMaxSize()) {
        LazyColumn(
            modifier = Modifier.fillMaxSize(),
            contentPadding = PaddingValues(start = 16.dp, top = 16.dp, end = 16.dp, bottom = 96.dp),
            verticalArrangement = Arrangement.spacedBy(8.dp),
        ) {
            item {
                MapHeader(snapshot = snapshot)
            }
            items(visibleNodes, key = { it.node.uniqueIdentifier }) { item ->
                NodeRow(
                    item = item,
                    onSetTaskState = { taskState ->
                        onSetTaskState(snapshot.filePath, item.node.uniqueIdentifier, taskState)
                    },
                    onEdit = { editingNode = item.node },
                    onAddNote = { addingChild = AddChildTarget(item.node, asTask = false) },
                    onAddTask = { addingChild = AddChildTarget(item.node, asTask = true) },
                    onDelete = { deleteCandidate = item.node },
                    onToggleHideDone = { onToggleHideDone(snapshot.filePath, item.node.uniqueIdentifier) },
                )
            }
        }
        FocusFab(
            imageVector = Icons.AutoMirrored.Filled.NoteAdd,
            contentDescription = "Add note to map root",
            onClick = { addingChild = AddChildTarget(snapshot.document.rootNode, asTask = false) },
            modifier = Modifier
                .align(Alignment.BottomEnd)
                .padding(20.dp),
        )
    }

    editingNode?.let { node ->
        EditNodeDialog(
            node = node,
            onDismiss = { editingNode = null },
            onSave = { text ->
                editingNode = null
                onEditNode(snapshot.filePath, node.uniqueIdentifier, text)
            },
        )
    }
    addingChild?.let { target ->
        AddChildDialog(
            target = target,
            onDismiss = { addingChild = null },
            onSave = { text ->
                addingChild = null
                onAddChild(snapshot.filePath, target.parent.uniqueIdentifier, text, target.asTask)
            },
        )
    }
    deleteCandidate?.let { node ->
        DeleteNodeDialog(
            node = node,
            onDismiss = { deleteCandidate = null },
            onConfirm = {
                deleteCandidate = null
                onDeleteNode(snapshot.filePath, node.uniqueIdentifier)
            },
        )
    }
}

@Composable
private fun MapHeader(snapshot: MapSnapshot) {
    val palette = LocalFocusPalette.current
    Column(
        modifier = Modifier.padding(bottom = 6.dp),
        verticalArrangement = Arrangement.spacedBy(2.dp),
    ) {
        Text(snapshot.mapName, style = MaterialTheme.typography.headlineSmall, fontWeight = FontWeight.SemiBold)
        Text(
            snapshot.filePath,
            style = MaterialTheme.typography.bodySmall,
            color = palette.muted,
            maxLines = 1,
            overflow = TextOverflow.Ellipsis,
        )
    }
}

@Composable
private fun NodeRow(
    item: VisibleNode,
    onSetTaskState: (TaskState) -> Unit,
    onEdit: () -> Unit,
    onAddNote: () -> Unit,
    onAddTask: () -> Unit,
    onDelete: () -> Unit,
    onToggleHideDone: () -> Unit,
) {
    val node = item.node
    val isRoot = item.depth == 0
    val palette = LocalFocusPalette.current
    FocusCard(
        modifier = Modifier
            .fillMaxWidth()
            .padding(start = (item.depth * 14).coerceAtMost(72).dp),
    ) {
        Column(
            modifier = Modifier.padding(12.dp),
            verticalArrangement = Arrangement.spacedBy(9.dp),
        ) {
            Row(horizontalArrangement = Arrangement.spacedBy(10.dp), verticalAlignment = Alignment.Top) {
                TaskDot(taskState = node.taskState, modifier = Modifier.padding(top = 6.dp))
                Column(modifier = Modifier.weight(1f)) {
                    Row(
                        horizontalArrangement = Arrangement.spacedBy(6.dp),
                        verticalAlignment = Alignment.Top,
                    ) {
                        Text(
                            MapQueries.normalizeNodeDisplayText(node.name),
                            style = if (isRoot) MaterialTheme.typography.titleLarge else MaterialTheme.typography.titleMedium,
                            fontWeight = if (isRoot) FontWeight.Bold else FontWeight.SemiBold,
                            maxLines = 3,
                            overflow = TextOverflow.Ellipsis,
                            modifier = Modifier.weight(1f, fill = false),
                        )
                        if (node.starred) {
                            Icon(
                                imageVector = Icons.Filled.Star,
                                contentDescription = "Starred",
                                tint = palette.accentStrong,
                                modifier = Modifier.size(19.dp),
                            )
                        }
                    }
                    if (node.isIdeaTag) {
                        Text("Idea", style = MaterialTheme.typography.bodySmall, color = palette.muted)
                    }
                }
            }
            if (!isRoot && node.canChangeTaskState) {
                TaskStateButtons(currentState = node.taskState, onSetTaskState = onSetTaskState)
            }
            NodeActionButtons(
                node = node,
                isRoot = isRoot,
                hidesDone = item.hidesDone,
                onEdit = onEdit,
                onAddNote = onAddNote,
                onAddTask = onAddTask,
                onDelete = onDelete,
                onToggleHideDone = onToggleHideDone,
            )
        }
    }
}

@Composable
private fun NodeActionButtons(
    node: Node,
    isRoot: Boolean,
    hidesDone: Boolean,
    onEdit: () -> Unit,
    onAddNote: () -> Unit,
    onAddTask: () -> Unit,
    onDelete: () -> Unit,
    onToggleHideDone: () -> Unit,
) {
    val nodeName = MapQueries.normalizeNodeDisplayText(node.name)
    Row(
        modifier = Modifier.horizontalScroll(rememberScrollState()),
        horizontalArrangement = Arrangement.spacedBy(7.dp),
    ) {
        if (node.canEditText) {
            FocusIconButton(Icons.Filled.Edit, "Edit $nodeName", onEdit)
        }
        if (!node.isIdeaTag) {
            FocusIconButton(Icons.AutoMirrored.Filled.NoteAdd, "Add note under $nodeName", onAddNote)
            FocusIconButton(Icons.Filled.AddTask, "Add task under $nodeName", onAddTask)
            if (node.children.isNotEmpty()) {
                FocusIconButton(
                    imageVector = if (hidesDone) Icons.Filled.Visibility else Icons.Filled.VisibilityOff,
                    contentDescription = if (hidesDone) "Show done descendants" else "Hide done descendants",
                    onClick = onToggleHideDone,
                    selected = hidesDone,
                )
            }
        }
        if (!isRoot && !node.isIdeaTag) {
            FocusIconButton(Icons.Filled.Delete, "Delete $nodeName", onDelete, destructive = true)
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
        horizontalArrangement = Arrangement.spacedBy(7.dp),
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
    val color = focusTaskColor(state, palette)
    val shape = CircleShape
    Row(
        modifier = Modifier
            .clip(shape)
            .background(if (selected) color.copy(alpha = if (palette.isDark) 0.18f else 0.10f) else Color.Transparent)
            .border(BorderStroke(1.dp, if (selected) color else palette.border), shape)
            .clickable(onClick = onClick)
            .heightIn(min = 34.dp)
            .padding(horizontal = 11.dp, vertical = 7.dp),
        verticalAlignment = Alignment.CenterVertically,
        horizontalArrangement = Arrangement.spacedBy(7.dp),
    ) {
        TaskDot(taskState = state, selected = selected)
        Text(
            text = focusTaskLabel(state),
            color = if (selected) color else palette.text,
            style = MaterialTheme.typography.labelMedium,
            fontWeight = FontWeight.SemiBold,
        )
    }
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

@Composable
private fun EditNodeDialog(
    node: Node,
    onDismiss: () -> Unit,
    onSave: (String) -> Unit,
) {
    var text by remember(node.uniqueIdentifier) { mutableStateOf(node.name) }
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
        FocusTextField(
            value = text,
            onValueChange = { text = it },
            label = "Text",
            minLines = 3,
        )
    }
}

@Composable
private fun AddChildDialog(
    target: AddChildTarget,
    onDismiss: () -> Unit,
    onSave: (String) -> Unit,
) {
    var text by remember(target.parent.uniqueIdentifier, target.asTask) { mutableStateOf("") }
    FocusDialog(
        title = if (target.asTask) "Add child task" else "Add child note",
        onDismiss = onDismiss,
        confirmButton = {
            TextButton(enabled = text.trim().isNotEmpty(), onClick = { onSave(text) }, colors = focusTextButtonColors()) {
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
            label = "Text",
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
        Text("Delete \"${MapQueries.normalizeNodeDisplayText(node.name)}\" and its children?")
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
    minLines: Int = 1,
) {
    OutlinedTextField(
        value = value,
        onValueChange = onValueChange,
        label = { Text(label) },
        minLines = minLines,
        modifier = modifier,
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
private fun DestructiveTextButton(onClick: () -> Unit, label: String) {
    TextButton(
        onClick = onClick,
        colors = ButtonDefaults.textButtonColors(contentColor = LocalFocusPalette.current.danger),
    ) {
        Text(label)
    }
}

@Composable
private fun focusTextButtonColors() = ButtonDefaults.textButtonColors(
    contentColor = LocalFocusPalette.current.accentStrong,
    disabledContentColor = LocalFocusPalette.current.muted.copy(alpha = 0.45f),
)

private data class VisibleNode(val node: Node, val depth: Int, val hidesDone: Boolean)

private data class AddChildTarget(val parent: Node, val asTask: Boolean)

private fun flattenVisibleNodes(node: Node, depth: Int, ancestorHidesDone: Boolean): List<VisibleNode> {
    val hideForChildren = MapQueries.getTreeHideDoneState(node, ancestorHidesDone)
    val children = MapQueries.getVisibleChildren(node, ancestorHidesDone)
    return listOf(VisibleNode(node, depth, hideForChildren)) + children.flatMap { child ->
        flattenVisibleNodes(child, depth + 1, hideForChildren)
    }
}
