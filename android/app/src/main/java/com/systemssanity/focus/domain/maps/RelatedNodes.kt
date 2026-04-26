package com.systemssanity.focus.domain.maps

import com.systemssanity.focus.domain.model.MapSnapshot
import com.systemssanity.focus.domain.model.Node

data class RelatedNodeEntry(
    val direction: Direction,
    val mapPath: String,
    val mapName: String,
    val nodeId: String,
    val nodeName: String,
    val nodePathSegments: List<String>,
    val relationLabel: String,
) {
    enum class Direction { Outgoing, Backlink }
}

object RelatedNodes {
    fun collectOutgoing(node: Node, snapshots: List<MapSnapshot>): List<RelatedNodeEntry> =
        node.links.values.mapNotNull { link ->
            val target = findNodeAcrossSnapshots(snapshots, link.id) ?: return@mapNotNull null
            RelatedNodeEntry(
                direction = RelatedNodeEntry.Direction.Outgoing,
                mapPath = target.snapshot.filePath,
                mapName = target.snapshot.mapName,
                nodeId = target.record.node.uniqueIdentifier,
                nodeName = MapQueries.normalizeNodeDisplayText(target.record.node.name),
                nodePathSegments = target.record.pathSegments,
                relationLabel = relationLabel(link.relationType),
            )
        }.sorted()

    fun collectBacklinks(targetNodeId: String, snapshots: List<MapSnapshot>): List<RelatedNodeEntry> {
        val entries = mutableListOf<RelatedNodeEntry>()
        snapshots.forEach { snapshot ->
            traverse(snapshot.document.rootNode, emptyList()) { node, path ->
                node.links.values
                    .filter { it.id.equals(targetNodeId, ignoreCase = true) }
                    .forEach { link ->
                        entries += RelatedNodeEntry(
                            direction = RelatedNodeEntry.Direction.Backlink,
                            mapPath = snapshot.filePath,
                            mapName = snapshot.mapName,
                            nodeId = node.uniqueIdentifier,
                            nodeName = MapQueries.normalizeNodeDisplayText(node.name),
                            nodePathSegments = path,
                            relationLabel = "backlink: ${relationLabel(link.relationType)}",
                        )
                    }
            }
        }
        return entries.sorted()
    }

    fun relationLabel(relationType: Int): String = when (relationType) {
        0 -> "relates"
        1 -> "prerequisite"
        2 -> "todo-with"
        3 -> "causes"
        else -> "link"
    }

    private data class LocatedNode(val snapshot: MapSnapshot, val record: NodeRecord)

    private fun findNodeAcrossSnapshots(snapshots: List<MapSnapshot>, nodeId: String): LocatedNode? =
        snapshots.firstNotNullOfOrNull { snapshot ->
            MapQueries.findNode(snapshot.document, nodeId)?.let { LocatedNode(snapshot, it) }
        }

    private fun traverse(node: Node, path: List<String>, visitor: (Node, List<String>) -> Unit) {
        val nextPath = path + MapQueries.normalizeNodeDisplayText(node.name)
        visitor(node, nextPath)
        node.children.forEach { traverse(it, nextPath, visitor) }
    }

    private fun List<RelatedNodeEntry>.sorted(): List<RelatedNodeEntry> =
        sortedWith(compareBy<RelatedNodeEntry> { it.mapName }.thenBy { it.nodePathSegments.joinToString(" > ") })
}
