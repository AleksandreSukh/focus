package com.systemssanity.focus.domain.maps

import com.systemssanity.focus.domain.model.Node

fun collectDeletedAttachmentRefs(node: Node): List<DeletedAttachmentRef> {
    val refs = mutableListOf<DeletedAttachmentRef>()

    fun visit(current: Node) {
        val nodeId = current.uniqueIdentifier
        current.metadata?.attachments.orEmpty().forEach { attachment ->
            val relativePath = attachment.relativePath
            if (nodeId.isNotBlank() && relativePath.isNotBlank()) {
                refs += DeletedAttachmentRef(
                    nodeId = nodeId,
                    attachmentId = attachment.id,
                    relativePath = relativePath,
                    displayName = attachment.displayName.ifBlank { relativePath },
                )
            }
        }
        current.children.forEach(::visit)
    }

    visit(node)
    return normalizeDeletedAttachmentRefs(refs)
}

fun normalizeDeletedAttachmentRefs(refs: List<DeletedAttachmentRef>): List<DeletedAttachmentRef> {
    val seen = LinkedHashSet<String>()
    return refs.filter { ref ->
        if (ref.nodeId.isBlank() || ref.relativePath.isBlank()) {
            false
        } else {
            seen.add(deletedAttachmentKey(ref))
        }
    }
}

fun deletedAttachmentKey(ref: DeletedAttachmentRef): String =
    "${ref.nodeId}\u0000${ref.relativePath}"
