package com.systemssanity.focus.domain.maps

import com.systemssanity.focus.domain.model.MapSnapshot
import com.systemssanity.focus.domain.model.MindMapDocument
import com.systemssanity.focus.domain.model.Node
import com.systemssanity.focus.domain.model.NodeType
import com.systemssanity.focus.domain.model.TaskState

data class NodeRecord(
    val node: Node,
    val parent: Node?,
    val pathSegments: List<String>,
    val depth: Int,
)

data class TaskEntry(
    val filePath: String,
    val fileName: String,
    val mapName: String,
    val nodeId: String,
    val nodeName: String,
    val nodePathSegments: List<String>,
    val taskState: TaskState,
    val depth: Int,
)

data class TaskCounts(
    val total: Int,
    val open: Int,
    val todo: Int,
    val doing: Int,
    val done: Int,
)

data class MapSummary(
    val filePath: String,
    val fileName: String,
    val mapName: String,
    val rootTitle: String,
    val updatedAt: String,
    val taskCounts: TaskCounts,
)

object MapQueries {
    fun findNode(document: MindMapDocument, nodeId: String): NodeRecord? {
        fun visit(node: Node, parent: Node?, path: List<String>, depth: Int): NodeRecord? {
            val nextPath = path + normalizeNodeDisplayText(node.name)
            if (node.uniqueIdentifier == nodeId) return NodeRecord(node, parent, nextPath, depth)
            return node.children.firstNotNullOfOrNull { child -> visit(child, node, nextPath, depth + 1) }
        }
        return visit(document.rootNode, null, emptyList(), 0)
    }

    fun buildMapSummary(snapshot: MapSnapshot): MapSummary =
        MapSummary(
            filePath = snapshot.filePath,
            fileName = snapshot.fileName,
            mapName = snapshot.mapName,
            rootTitle = normalizeNodeDisplayText(snapshot.document.rootNode.name),
            updatedAt = snapshot.document.updatedAt,
            taskCounts = getTaskCounts(snapshot.document),
        )

    fun getTaskCounts(document: MindMapDocument): TaskCounts {
        val entries = collectTaskEntries(MapSnapshot("", "", "", document, "", 0), TaskFilter.All)
        return TaskCounts(
            total = entries.size,
            open = entries.count { it.taskState.isOpen },
            todo = entries.count { it.taskState == TaskState.Todo },
            doing = entries.count { it.taskState == TaskState.Doing },
            done = entries.count { it.taskState == TaskState.Done },
        )
    }

    fun collectTaskEntries(snapshot: MapSnapshot, filter: TaskFilter = TaskFilter.Open): List<TaskEntry> {
        val entries = mutableListOf<TaskEntry>()
        fun visit(node: Node, parent: Node?, path: List<String>, depth: Int) {
            val nextPath = path + normalizeNodeDisplayText(node.name)
            if (isTaskNode(node, parent) && filter.matches(node.taskState)) {
                entries += TaskEntry(
                    filePath = snapshot.filePath,
                    fileName = snapshot.fileName,
                    mapName = snapshot.mapName,
                    nodeId = node.uniqueIdentifier,
                    nodeName = normalizeNodeDisplayText(node.name),
                    nodePathSegments = nextPath,
                    taskState = node.taskState,
                    depth = depth,
                )
            }
            node.children.forEach { child -> visit(child, node, nextPath, depth + 1) }
        }
        visit(snapshot.document.rootNode, null, emptyList(), 0)
        return entries.sortedWith(compareBy<TaskEntry> { it.taskState.sortPriority() }
            .thenBy { it.mapName }
            .thenBy { it.nodePathSegments.joinToString(" > ") })
    }

    fun getVisibleChildren(node: Node, ancestorHidesDone: Boolean): List<Node> {
        val hideForChildren = getTreeHideDoneState(node, ancestorHidesDone)
        return node.children.filterNot { child -> hideForChildren && child.taskState == TaskState.Done }
    }

    fun getNodeHideDoneState(document: MindMapDocument, nodeId: String): Boolean {
        var result = false
        fun visit(node: Node, ancestorHidesDone: Boolean): Boolean {
            val current = getTreeHideDoneState(node, ancestorHidesDone)
            if (node.uniqueIdentifier == nodeId) {
                result = current
                return true
            }
            return node.children.any { visit(it, current) }
        }
        visit(document.rootNode, false)
        return result
    }

    fun getTreeHideDoneState(node: Node, ancestorHidesDone: Boolean): Boolean {
        val local = when {
            node.hideDoneTasksExplicit == true -> node.hideDoneTasks
            node.hideDoneTasks -> true
            else -> null
        }
        return local ?: ancestorHidesDone
    }

    fun normalizeNodeDisplayText(value: String): String =
        value.replace(Regex("\\r\\n|\\r|\\n"), " ").trim().ifBlank { "Untitled" }

    private fun isTaskNode(node: Node, parent: Node?): Boolean =
        parent != null && node.nodeType != NodeType.IdeaBagItem && node.taskState.isTask
}

enum class TaskFilter {
    Open,
    Todo,
    Doing,
    Done,
    All;

    fun matches(taskState: TaskState): Boolean = when (this) {
        Open -> taskState == TaskState.Todo || taskState == TaskState.Doing
        Todo -> taskState == TaskState.Todo
        Doing -> taskState == TaskState.Doing
        Done -> taskState == TaskState.Done
        All -> taskState != TaskState.None
    }
}
