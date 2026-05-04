package com.systemssanity.focus.domain.maps

import com.systemssanity.focus.domain.model.NodeAttachment
import java.util.Locale

enum class AttachmentViewerKind {
    Text,
    Audio,
    Pdf,
    Image,
}

object AttachmentViewers {
    fun viewerKind(attachment: NodeAttachment): AttachmentViewerKind {
        val mediaType = attachment.mediaType.trim().lowercase(Locale.ROOT)
        val path = attachment.relativePath.trim().lowercase(Locale.ROOT)
        val displayName = attachment.displayName.trim().lowercase(Locale.ROOT)
        return when {
            mediaType.startsWith("text/") -> AttachmentViewerKind.Text
            mediaType.startsWith("audio/") -> AttachmentViewerKind.Audio
            mediaType == "application/pdf" || path.endsWith(".pdf") || displayName.endsWith(".pdf") -> AttachmentViewerKind.Pdf
            else -> AttachmentViewerKind.Image
        }
    }

    fun title(attachment: NodeAttachment): String =
        AttachmentUploads.displayName(
            attachment.displayName
                .ifBlank { attachment.relativePath }
                .ifBlank { "Attachment" },
        )

    fun canView(attachment: NodeAttachment): Boolean =
        attachment.relativePath.isNotBlank()

    fun openActionLabel(attachment: NodeAttachment): String =
        "View ${title(attachment)}"

    fun closeActionLabel(attachment: NodeAttachment): String =
        "Close ${title(attachment)}"

    fun formatByteSize(byteCount: Int): String {
        val bytes = byteCount.coerceAtLeast(0)
        return when {
            bytes < 1024 -> "$bytes B"
            bytes < 1024 * 1024 -> String.format(Locale.US, "%.1f KB", bytes.toDouble() / 1024.0)
            else -> String.format(Locale.US, "%.1f MB", bytes.toDouble() / 1024.0 / 1024.0)
        }
    }

    fun unsupportedPreviewMessage(kind: AttachmentViewerKind): String =
        when (kind) {
            AttachmentViewerKind.Image -> "This attachment was loaded, but Android could not preview it as an image."
            AttachmentViewerKind.Pdf -> "This PDF was loaded, but Android could not preview it inline."
            AttachmentViewerKind.Audio -> "This audio attachment was loaded, but Android could not prepare playback."
            AttachmentViewerKind.Text -> "This text attachment was loaded, but Android could not preview it inline."
        }
}
