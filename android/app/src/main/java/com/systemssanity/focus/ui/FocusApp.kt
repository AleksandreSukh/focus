package com.systemssanity.focus.ui

import androidx.compose.foundation.clickable
import androidx.compose.foundation.horizontalScroll
import androidx.compose.foundation.layout.Arrangement
import androidx.compose.foundation.layout.Column
import androidx.compose.foundation.layout.Row
import androidx.compose.foundation.layout.Spacer
import androidx.compose.foundation.layout.fillMaxSize
import androidx.compose.foundation.layout.fillMaxWidth
import androidx.compose.foundation.layout.height
import androidx.compose.foundation.layout.padding
import androidx.compose.foundation.rememberScrollState
import androidx.compose.foundation.lazy.LazyColumn
import androidx.compose.foundation.lazy.items
import androidx.compose.material3.AlertDialog
import androidx.compose.material3.AssistChip
import androidx.compose.material3.Button
import androidx.compose.material3.Card
import androidx.compose.material3.ExperimentalMaterial3Api
import androidx.compose.material3.FilterChip
import androidx.compose.material3.MaterialTheme
import androidx.compose.material3.OutlinedTextField
import androidx.compose.material3.Scaffold
import androidx.compose.material3.Surface
import androidx.compose.material3.Tab
import androidx.compose.material3.TabRow
import androidx.compose.material3.Text
import androidx.compose.material3.TextButton
import androidx.compose.material3.TopAppBar
import androidx.compose.runtime.Composable
import androidx.compose.runtime.getValue
import androidx.compose.runtime.mutableStateOf
import androidx.compose.runtime.remember
import androidx.compose.runtime.setValue
import androidx.compose.ui.Modifier
import androidx.compose.ui.text.font.FontWeight
import androidx.compose.ui.unit.dp
import androidx.lifecycle.viewmodel.compose.viewModel
import com.systemssanity.focus.data.local.RepoSettings
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

    MaterialTheme {
        Surface(modifier = Modifier.fillMaxSize()) {
            var screen by remember { mutableStateOf(AppScreen.Connection) }
            Scaffold(
                topBar = {
                    FocusTopBar(
                        screen = screen,
                        statusMessage = uiState.statusMessage,
                        pendingCount = uiState.pendingCount,
                        onSyncRequested = viewModel::syncPendingNow,
                        onScreenChanged = { screen = it },
                    )
                },
            ) { padding ->
                Column(
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

@OptIn(ExperimentalMaterial3Api::class)
@Composable
private fun FocusTopBar(
    screen: AppScreen,
    statusMessage: String,
    pendingCount: Int,
    onSyncRequested: () -> Unit,
    onScreenChanged: (AppScreen) -> Unit,
) {
    Column {
        TopAppBar(
            title = { Text("Focus") },
            actions = {
                val status = if (pendingCount > 0) "$pendingCount pending" else statusMessage.take(28)
                AssistChip(
                    onClick = { if (pendingCount > 0) onSyncRequested() },
                    label = { Text(status.ifBlank { "Ready" }) },
                )
            },
        )
        TabRow(selectedTabIndex = when (screen) {
            AppScreen.Maps, AppScreen.Map -> 0
            AppScreen.Tasks -> 1
            AppScreen.Connection -> 2
        }) {
            Tab(selected = screen == AppScreen.Maps || screen == AppScreen.Map, onClick = { onScreenChanged(AppScreen.Maps) }, text = { Text("Maps") })
            Tab(selected = screen == AppScreen.Tasks, onClick = { onScreenChanged(AppScreen.Tasks) }, text = { Text("Tasks") })
            Tab(selected = screen == AppScreen.Connection, onClick = { onScreenChanged(AppScreen.Connection) }, text = { Text("Connection") })
        }
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
    Column(
        modifier = Modifier
            .fillMaxSize()
            .padding(16.dp),
        verticalArrangement = Arrangement.spacedBy(12.dp),
    ) {
        Text("GitHub connection", style = MaterialTheme.typography.headlineSmall, fontWeight = FontWeight.SemiBold)
        OutlinedTextField(value = owner, onValueChange = { owner = it }, label = { Text("Repository owner") }, modifier = Modifier.fillMaxWidth())
        OutlinedTextField(value = repo, onValueChange = { repo = it }, label = { Text("Repository name") }, modifier = Modifier.fillMaxWidth())
        OutlinedTextField(value = branch, onValueChange = { branch = it }, label = { Text("Branch") }, modifier = Modifier.fillMaxWidth())
        OutlinedTextField(value = path, onValueChange = { path = it }, label = { Text("FocusMaps folder path") }, modifier = Modifier.fillMaxWidth())
        OutlinedTextField(value = token, onValueChange = { token = it }, label = { Text(if (uiState.tokenPresent) "Personal access token (saved)" else "Personal access token") }, modifier = Modifier.fillMaxWidth())
        Button(
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
        ) {
            Text("Save connection and load")
        }
        Button(
            enabled = !uiState.loading && uiState.tokenPresent,
            onClick = onLoad,
            modifier = Modifier.fillMaxWidth(),
        ) {
            Text("Reload workspace")
        }
        Text(uiState.statusMessage, style = MaterialTheme.typography.bodyMedium)
        Text(
            "The native app stores the PAT in Android Keystore-backed encrypted preferences and caches maps locally for offline reading.",
            style = MaterialTheme.typography.bodyMedium,
        )
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
    var newMapName by remember { mutableStateOf("") }
    var deleteCandidate by remember { mutableStateOf<MapSnapshot?>(null) }
    val summaries = snapshots.map(MapQueries::buildMapSummary).sortedByDescending { it.updatedAt }
    LazyColumn(
        modifier = Modifier
            .fillMaxSize()
            .padding(16.dp),
        verticalArrangement = Arrangement.spacedBy(10.dp),
    ) {
        item {
            Card(modifier = Modifier.fillMaxWidth()) {
                Column(
                    modifier = Modifier.padding(16.dp),
                    verticalArrangement = Arrangement.spacedBy(10.dp),
                ) {
                    Text("Create map", style = MaterialTheme.typography.titleMedium, fontWeight = FontWeight.SemiBold)
                    OutlinedTextField(
                        value = newMapName,
                        onValueChange = { newMapName = it },
                        label = { Text("Map name") },
                        modifier = Modifier.fillMaxWidth(),
                    )
                    Button(
                        enabled = !loading && newMapName.trim().isNotEmpty(),
                        onClick = {
                            onCreateMap(newMapName)
                            newMapName = ""
                        },
                        modifier = Modifier.fillMaxWidth(),
                    ) {
                        Text("Create")
                    }
                }
            }
        }
        if (snapshots.isEmpty()) {
            item {
                Text("No maps are loaded.", style = MaterialTheme.typography.bodyMedium)
            }
        }
        items(summaries, key = { it.filePath }) { summary ->
            Card(
                modifier = Modifier.fillMaxWidth(),
            ) {
                Column(modifier = Modifier.padding(16.dp)) {
                    Text(summary.rootTitle, style = MaterialTheme.typography.titleMedium, fontWeight = FontWeight.SemiBold)
                    Spacer(Modifier.height(6.dp))
                    Row(horizontalArrangement = Arrangement.spacedBy(8.dp)) {
                        Text("Open ${summary.taskCounts.open}")
                        Text("Todo ${summary.taskCounts.todo}")
                        Text("Doing ${summary.taskCounts.doing}")
                        Text("Done ${summary.taskCounts.done}")
                    }
                    Spacer(Modifier.height(8.dp))
                    Row(
                        modifier = Modifier.horizontalScroll(rememberScrollState()),
                        horizontalArrangement = Arrangement.spacedBy(6.dp),
                    ) {
                        Button(
                            enabled = !loading,
                            onClick = { onOpenMap(snapshots.first { it.filePath == summary.filePath }) },
                        ) {
                            Text("Open")
                        }
                        TextButton(
                            enabled = !loading,
                            onClick = { deleteCandidate = snapshots.first { it.filePath == summary.filePath } },
                        ) {
                            Text("Delete")
                        }
                    }
                }
            }
        }
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
private fun TasksScreen(
    snapshots: List<MapSnapshot>,
    filter: TaskFilter,
    onFilterChanged: (TaskFilter) -> Unit,
    onOpenTask: (TaskEntry) -> Unit,
    onSetTaskState: (TaskEntry, TaskState) -> Unit,
) {
    val entries = snapshots.flatMap { MapQueries.collectTaskEntries(it, filter) }
    Column(modifier = Modifier.fillMaxSize().padding(16.dp)) {
        Row(
            modifier = Modifier.horizontalScroll(rememberScrollState()),
            horizontalArrangement = Arrangement.spacedBy(8.dp),
        ) {
            listOf(TaskFilter.Open, TaskFilter.Todo, TaskFilter.Doing, TaskFilter.Done, TaskFilter.All).forEach { candidate ->
                FilterChip(
                    selected = filter == candidate,
                    onClick = { onFilterChanged(candidate) },
                    label = { Text(candidate.name) },
                )
            }
        }
        Spacer(Modifier.height(12.dp))
        if (entries.isEmpty()) {
            Text("No tasks match the selected filter.", style = MaterialTheme.typography.bodyMedium)
            return
        }
        LazyColumn(verticalArrangement = Arrangement.spacedBy(10.dp)) {
            items(entries, key = { "${it.filePath}:${it.nodeId}" }) { entry ->
                Card(
                    modifier = Modifier
                        .fillMaxWidth()
                        .clickable { onOpenTask(entry) },
                ) {
                    Column(modifier = Modifier.padding(16.dp)) {
                        Text(entry.nodeName, style = MaterialTheme.typography.titleMedium)
                        Text(entry.mapName, style = MaterialTheme.typography.bodyMedium)
                        Text(entry.nodePathSegments.joinToString(" > "), style = MaterialTheme.typography.bodySmall)
                        Spacer(Modifier.height(8.dp))
                        TaskStateButtons(
                            currentState = entry.taskState,
                            onSetTaskState = { onSetTaskState(entry, it) },
                        )
                    }
                }
            }
        }
    }
}

@Composable
private fun EmptyPanel(message: String) {
    Column(
        modifier = Modifier
            .fillMaxSize()
            .padding(16.dp),
        verticalArrangement = Arrangement.Center,
    ) {
        Text(message, style = MaterialTheme.typography.titleMedium)
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

    LazyColumn(
        modifier = Modifier.fillMaxSize().padding(16.dp),
        verticalArrangement = Arrangement.spacedBy(8.dp),
    ) {
        item {
            Text(snapshot.mapName, style = MaterialTheme.typography.headlineSmall, fontWeight = FontWeight.SemiBold)
            Text(snapshot.filePath, style = MaterialTheme.typography.bodySmall)
            Spacer(Modifier.height(12.dp))
        }
        items(visibleNodes, key = { it.node.uniqueIdentifier }) { item ->
            NodeCard(
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
private fun NodeCard(
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
    Card(
        modifier = Modifier
            .fillMaxWidth()
            .padding(start = (item.depth * 14).dp),
    ) {
        Column(modifier = Modifier.padding(12.dp)) {
            Row(horizontalArrangement = Arrangement.spacedBy(8.dp)) {
                Text(node.taskState.displayMarker().ifBlank { "-" }, fontWeight = FontWeight.SemiBold)
                Text(MapQueries.normalizeNodeDisplayText(node.name), style = MaterialTheme.typography.titleMedium)
            }
            Spacer(Modifier.height(8.dp))
            if (!isRoot && node.canChangeTaskState) {
                TaskStateButtons(currentState = node.taskState, onSetTaskState = onSetTaskState)
                Spacer(Modifier.height(8.dp))
            }
            Row(
                modifier = Modifier.horizontalScroll(rememberScrollState()),
                horizontalArrangement = Arrangement.spacedBy(6.dp),
            ) {
                if (node.canEditText) {
                    TextButton(onClick = onEdit) { Text("Edit") }
                }
                if (!node.isIdeaTag) {
                    TextButton(onClick = onAddNote) { Text("Add note") }
                    TextButton(onClick = onAddTask) { Text("Add task") }
                    if (node.children.isNotEmpty()) {
                        TextButton(onClick = onToggleHideDone) {
                            Text(if (item.hidesDone) "Show done" else "Hide done")
                        }
                    }
                }
                if (!isRoot && !node.isIdeaTag) {
                    TextButton(onClick = onDelete) { Text("Delete") }
                }
            }
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
        horizontalArrangement = Arrangement.spacedBy(6.dp),
    ) {
        listOf(TaskState.None, TaskState.Todo, TaskState.Doing, TaskState.Done).forEach { state ->
            FilterChip(
                selected = currentState == state,
                onClick = { onSetTaskState(state) },
                label = {
                    Text(
                        when (state) {
                            TaskState.None -> "Clear"
                            TaskState.Todo -> "Todo"
                            TaskState.Doing -> "Doing"
                            TaskState.Done -> "Done"
                        },
                    )
                },
            )
        }
    }
}

@Composable
private fun EditNodeDialog(
    node: Node,
    onDismiss: () -> Unit,
    onSave: (String) -> Unit,
) {
    var text by remember(node.uniqueIdentifier) { mutableStateOf(node.name) }
    AlertDialog(
        onDismissRequest = onDismiss,
        title = { Text("Edit node") },
        text = {
            OutlinedTextField(
                value = text,
                onValueChange = { text = it },
                label = { Text("Text") },
                minLines = 3,
                modifier = Modifier.fillMaxWidth(),
            )
        },
        confirmButton = {
            TextButton(enabled = text.trim().isNotEmpty(), onClick = { onSave(text) }) {
                Text("Save")
            }
        },
        dismissButton = {
            TextButton(onClick = onDismiss) { Text("Cancel") }
        },
    )
}

@Composable
private fun AddChildDialog(
    target: AddChildTarget,
    onDismiss: () -> Unit,
    onSave: (String) -> Unit,
) {
    var text by remember(target.parent.uniqueIdentifier, target.asTask) { mutableStateOf("") }
    AlertDialog(
        onDismissRequest = onDismiss,
        title = { Text(if (target.asTask) "Add child task" else "Add child note") },
        text = {
            OutlinedTextField(
                value = text,
                onValueChange = { text = it },
                label = { Text("Text") },
                minLines = 2,
                modifier = Modifier.fillMaxWidth(),
            )
        },
        confirmButton = {
            TextButton(enabled = text.trim().isNotEmpty(), onClick = { onSave(text) }) {
                Text("Add")
            }
        },
        dismissButton = {
            TextButton(onClick = onDismiss) { Text("Cancel") }
        },
    )
}

@Composable
private fun DeleteNodeDialog(
    node: Node,
    onDismiss: () -> Unit,
    onConfirm: () -> Unit,
) {
    AlertDialog(
        onDismissRequest = onDismiss,
        title = { Text("Delete node") },
        text = {
            Text("Delete \"${MapQueries.normalizeNodeDisplayText(node.name)}\" and its children?")
        },
        confirmButton = {
            TextButton(onClick = onConfirm) { Text("Delete") }
        },
        dismissButton = {
            TextButton(onClick = onDismiss) { Text("Cancel") }
        },
    )
}

@Composable
private fun DeleteMapDialog(
    snapshot: MapSnapshot,
    onDismiss: () -> Unit,
    onConfirm: () -> Unit,
) {
    AlertDialog(
        onDismissRequest = onDismiss,
        title = { Text("Delete map") },
        text = {
            Text("Delete \"${snapshot.mapName}\" from GitHub and this device?")
        },
        confirmButton = {
            TextButton(onClick = onConfirm) { Text("Delete") }
        },
        dismissButton = {
            TextButton(onClick = onDismiss) { Text("Cancel") }
        },
    )
}

private data class VisibleNode(val node: Node, val depth: Int, val hidesDone: Boolean)

private data class AddChildTarget(val parent: Node, val asTask: Boolean)

private fun flattenVisibleNodes(node: Node, depth: Int, ancestorHidesDone: Boolean): List<VisibleNode> {
    val hideForChildren = MapQueries.getTreeHideDoneState(node, ancestorHidesDone)
    val children = MapQueries.getVisibleChildren(node, ancestorHidesDone)
    return listOf(VisibleNode(node, depth, hideForChildren)) + children.flatMap { child ->
        flattenVisibleNodes(child, depth + 1, hideForChildren)
    }
}
