package com.systemssanity.focus.domain.maps

import com.systemssanity.focus.domain.model.NodeAttachment
import java.util.Locale

object AttachmentUploads {
    const val MaxBytes: Long = 10L * 1024L * 1024L

    val PickerMimeTypes: Array<String> = arrayOf(
        "image/*",
        "audio/*",
        "application/pdf",
        "text/plain",
        "text/markdown",
    )

    fun validationError(sizeBytes: Long?, mediaType: String?, displayName: String?): String? {
        if (sizeBytes != null && sizeBytes > MaxBytes) {
            return tooLargeMessage(sizeBytes)
        }

        val normalizedMediaType = normalizedMediaType(mediaType, displayName)
        if (!isAcceptedMediaType(normalizedMediaType)) {
            return unsupportedTypeMessage(normalizedMediaType)
        }

        return null
    }

    fun normalizedMediaType(mediaType: String?, displayName: String?): String {
        val normalized = mediaType.orEmpty().trim().lowercase(Locale.ROOT)
        if ((normalized.isBlank() || normalized == "application/octet-stream") && isMarkdownFile(displayName)) {
            return "text/markdown"
        }
        return normalized.ifBlank { "application/octet-stream" }
    }

    fun isAcceptedMediaType(mediaType: String): Boolean =
        mediaType.startsWith("image/") ||
            mediaType.startsWith("audio/") ||
            mediaType.startsWith("text/") ||
            mediaType == "application/pdf"

    fun displayName(rawName: String?): String =
        rawName.orEmpty()
            .substringAfterLast('/')
            .substringAfterLast('\\')
            .substringAfterLast(':')
            .trim()
            .ifBlank { "Attachment" }

    fun extension(displayName: String): String {
        val lastDot = displayName.lastIndexOf('.')
        return if (lastDot >= 0) displayName.substring(lastDot).lowercase(Locale.ROOT) else ""
    }

    fun relativePath(attachmentId: String, displayName: String): String =
        attachmentId + extension(displayName)

    fun attachment(
        attachmentId: String,
        displayName: String,
        mediaType: String,
        createdAtUtc: String,
        fileName: String = displayName,
    ): NodeAttachment =
        NodeAttachment(
            id = attachmentId,
            relativePath = relativePath(attachmentId, fileName),
            mediaType = mediaType,
            displayName = displayName,
            createdAtUtc = createdAtUtc,
        )

    fun tooLargeMessage(sizeBytes: Long): String =
        "File is too large (${String.format(Locale.US, "%.1f", sizeBytes.toDouble() / 1024.0 / 1024.0)} MB). Maximum size is 10 MB."

    fun unsupportedTypeMessage(mediaType: String): String =
        "Unsupported file type \"${mediaType.ifBlank { "unknown" }}\". Attach an image, audio, PDF, or text file."

    private fun isMarkdownFile(displayName: String?): Boolean {
        val lower = displayName.orEmpty().trim().lowercase(Locale.ROOT)
        return lower.endsWith(".md") || lower.endsWith(".markdown")
    }
}
