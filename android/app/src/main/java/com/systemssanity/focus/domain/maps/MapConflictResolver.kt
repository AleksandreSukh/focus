package com.systemssanity.focus.domain.maps

import com.systemssanity.focus.domain.model.MindMapDocument
import com.systemssanity.focus.domain.model.MindMapJson
import com.systemssanity.focus.domain.model.Node
import com.systemssanity.focus.domain.model.NodeAttachment
import java.time.Instant

data class ConflictResolveResult(
    val ok: Boolean,
    val resolvedContent: String? = null,
)

object MapConflictResolver {
    fun hasConflictMarkers(content: String): Boolean =
        content.contains("<<<<<<< ")

    fun tryResolve(conflictedContent: String): ConflictResolveResult {
        if (!hasConflictMarkers(conflictedContent)) return ConflictResolveResult(ok = false)

        val ours = buildResolvedContent(conflictedContent, takeOurs = true)
        val theirs = buildResolvedContent(conflictedContent, takeOurs = false)
        val merged = tryMergeResolve(ours, theirs)
        if (merged.ok) return merged

        val oursTimestamp = tryParseMapTimestamp(ours)
        val theirsTimestamp = tryParseMapTimestamp(theirs)
        if (oursTimestamp == null && theirsTimestamp == null) {
            return ConflictResolveResult(ok = false)
        }

        val selected = when {
            oursTimestamp != null && theirsTimestamp != null && oursTimestamp > theirsTimestamp -> ours
            oursTimestamp != null && theirsTimestamp == null -> ours
            else -> theirs
        }
        return ConflictResolveResult(ok = true, resolvedContent = selected)
    }

    private fun tryMergeResolve(oursJson: String, theirsJson: String): ConflictResolveResult =
        runCatching {
            val ours = MindMapJson.parse(oursJson)
            val theirs = MindMapJson.parse(theirsJson)
            val merged = mergeDocuments(ours, theirs) ?: return ConflictResolveResult(ok = false)
            ConflictResolveResult(ok = true, resolvedContent = MindMapJson.serialize(merged))
        }.getOrElse {
            ConflictResolveResult(ok = false)
        }

    private fun mergeDocuments(ours: MindMapDocument, theirs: MindMapDocument): MindMapDocument? {
        val root = mergeNode(ours.rootNode, theirs.rootNode) ?: return null
        return MindMapDocument(
            rootNode = root,
            updatedAt = maxTimestamp(ours.updatedAt, theirs.updatedAt) ?: ours.updatedAt,
        )
    }

    private fun mergeNode(ours: Node, theirs: Node): Node? {
        if (!ours.uniqueIdentifier.equals(theirs.uniqueIdentifier, ignoreCase = true)) return null
        if (ours.nodeType != theirs.nodeType) return null

        val oursUpdated = parseInstant(ours.metadata?.updatedAtUtc)
        val theirsUpdated = parseInstant(theirs.metadata?.updatedAtUtc)
        val takeTheirs = compareNullableInstants(theirsUpdated, oursUpdated) >= 0
        val mergedMetadata = mergeMetadata(ours, theirs, takeTheirs)
        val mergedChildren = mergeChildren(ours.children, theirs.children) ?: return null

        return ours.copy(
            name = if (takeTheirs) theirs.name else ours.name,
            collapsed = if (takeTheirs) theirs.collapsed else ours.collapsed,
            hideDoneTasks = if (takeTheirs) theirs.hideDoneTasks else ours.hideDoneTasks,
            hideDoneTasksExplicit = if (takeTheirs) theirs.hideDoneTasksExplicit else ours.hideDoneTasksExplicit,
            starred = if (takeTheirs) theirs.starred else ours.starred,
            taskState = if (takeTheirs) theirs.taskState else ours.taskState,
            metadata = mergedMetadata,
            links = ours.links + theirs.links.filterKeys { it !in ours.links },
            children = mergedChildren,
        )
    }

    private fun mergeMetadata(ours: Node, theirs: Node, takeTheirs: Boolean) =
        (ours.metadata ?: theirs.metadata)?.let { base ->
            val oursMetadata = ours.effectiveMetadata()
            val theirsMetadata = theirs.effectiveMetadata()
            base.copy(
                createdAtUtc = minTimestamp(oursMetadata.createdAtUtc, theirsMetadata.createdAtUtc) ?: base.createdAtUtc,
                updatedAtUtc = maxTimestamp(oursMetadata.updatedAtUtc, theirsMetadata.updatedAtUtc) ?: base.updatedAtUtc,
                source = if (takeTheirs) theirsMetadata.source else oursMetadata.source,
                device = if (takeTheirs) theirsMetadata.device else oursMetadata.device,
                attachments = mergeAttachments(oursMetadata.attachments, theirsMetadata.attachments),
            )
        }

    private fun mergeChildren(oursChildren: List<Node>, theirsChildren: List<Node>): List<Node>? {
        val mergedById = LinkedHashMap<String, Node>()
        oursChildren.forEach { child -> mergedById[child.uniqueIdentifier.lowercase()] = child }
        theirsChildren.forEach { theirs ->
            val key = theirs.uniqueIdentifier.lowercase()
            val existing = mergedById[key]
            mergedById[key] = if (existing == null) {
                theirs
            } else {
                mergeNode(existing, theirs) ?: return null
            }
        }
        return mergedById.values.mapIndexed { index, node -> node.copy(number = index + 1) }
    }

    private fun mergeAttachments(ours: List<NodeAttachment>, theirs: List<NodeAttachment>): List<NodeAttachment> {
        val mergedById = LinkedHashMap<String, NodeAttachment>()
        ours.forEach { attachment -> mergedById[attachment.id.lowercase()] = attachment }
        theirs.forEach { attachment -> mergedById.putIfAbsent(attachment.id.lowercase(), attachment) }
        return mergedById.values.toList()
    }

    private fun buildResolvedContent(content: String, takeOurs: Boolean): String {
        val lines = content.replace("\r\n", "\n").replace("\r", "\n").split('\n')
        val result = mutableListOf<String>()
        var inOurs = false
        var inTheirs = false
        lines.forEach { line ->
            when {
                line.startsWith("<<<<<<< ") -> {
                    inOurs = true
                    inTheirs = false
                }
                line == "=======" -> {
                    inOurs = false
                    inTheirs = true
                }
                line.startsWith(">>>>>>> ") -> {
                    inOurs = false
                    inTheirs = false
                }
                !inOurs && !inTheirs -> result += line
                takeOurs && inOurs -> result += line
                !takeOurs && inTheirs -> result += line
            }
        }
        return result.joinToString("\n")
    }

    private fun tryParseMapTimestamp(jsonContent: String): Instant? =
        runCatching {
            val document = MindMapJson.parse(jsonContent)
            parseInstant(document.updatedAt) ?: parseInstant(document.rootNode.metadata?.updatedAtUtc)
        }.getOrNull()

    private fun maxTimestamp(left: String, right: String): String? =
        when (compareNullableInstants(parseInstant(left), parseInstant(right))) {
            in Int.MIN_VALUE until 0 -> right
            else -> left
        }

    private fun minTimestamp(left: String, right: String): String? =
        when (compareNullableInstants(parseInstant(left), parseInstant(right))) {
            in Int.MIN_VALUE until 0 -> left
            else -> right
        }

    private fun parseInstant(value: String?): Instant? =
        value?.takeIf { it.isNotBlank() }?.let { runCatching { Instant.parse(it) }.getOrNull() }

    private fun compareNullableInstants(left: Instant?, right: Instant?): Int =
        when {
            left == right -> 0
            left == null -> -1
            right == null -> 1
            else -> left.compareTo(right)
        }
}
