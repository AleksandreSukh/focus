package com.systemssanity.focus.domain.maps

import com.systemssanity.focus.domain.model.NodeAttachment
import java.io.File
import java.util.Locale

data class AttachmentExportFile(
    val fileName: String,
    val mimeType: String,
)

object AttachmentExports {
    private const val DefaultMimeType = "application/octet-stream"
    private const val ExportDirectoryName = "focus-attachment-exports"

    fun exportFile(
        attachment: NodeAttachment,
        kind: AttachmentViewerKind,
        loadedMediaType: String,
    ): AttachmentExportFile =
        AttachmentExportFile(
            fileName = fileName(attachment, kind),
            mimeType = mimeType(loadedMediaType, attachment, kind),
        )

    fun fileName(attachment: NodeAttachment, kind: AttachmentViewerKind): String {
        val rawName = when {
            attachment.displayName.isNotBlank() -> attachment.displayName.trim()
            attachment.relativePath.isNotBlank() -> AttachmentUploads.displayName(attachment.relativePath)
            else -> fallbackFileName(kind, attachment.mediaType)
        }
        val safeName = sanitizeFileName(rawName)
            .ifBlank { fallbackFileName(kind, attachment.mediaType) }

        if (AttachmentUploads.extension(safeName).isNotBlank()) {
            return safeName
        }

        val relativeExtension = AttachmentUploads.extension(AttachmentUploads.displayName(attachment.relativePath))
        val mediaTypeExtension = extensionForMediaType(attachment.mediaType)
        val extension = when {
            relativeExtension.isNotBlank() && relativeExtension != ".bin" -> relativeExtension
            mediaTypeExtension.isNotBlank() -> mediaTypeExtension
            relativeExtension.isNotBlank() -> relativeExtension
            else -> AttachmentUploads.extension(fallbackFileName(kind, attachment.mediaType))
        }

        return if (extension.isBlank()) safeName else safeName + extension
    }

    fun mimeType(
        loadedMediaType: String,
        attachment: NodeAttachment,
        kind: AttachmentViewerKind,
    ): String {
        val loaded = loadedMediaType.trim().lowercase(Locale.ROOT)
        if (loaded.isNotBlank()) {
            return loaded
        }

        val metadata = attachment.mediaType.trim().lowercase(Locale.ROOT)
        if (metadata.isNotBlank()) {
            return metadata
        }

        return when (kind) {
            AttachmentViewerKind.Text -> "text/plain"
            AttachmentViewerKind.Audio -> "audio/mp4"
            AttachmentViewerKind.Pdf -> "application/pdf"
            AttachmentViewerKind.Image -> DefaultMimeType
        }
    }

    fun saveActionLabel(attachment: NodeAttachment, kind: AttachmentViewerKind): String =
        "Save ${fileName(attachment, kind)}"

    fun shareActionLabel(attachment: NodeAttachment, kind: AttachmentViewerKind): String =
        "Share ${fileName(attachment, kind)}"

    fun canExport(bytes: ByteArray?, loading: Boolean, errorMessage: String): Boolean =
        bytes != null && !loading && errorMessage.isBlank()

    fun writeAttachmentExportFile(cacheDir: File, fileName: String, bytes: ByteArray): File {
        val exportDir = File(cacheDir, ExportDirectoryName)
        if (!exportDir.exists() && !exportDir.mkdirs()) {
            error("Could not prepare attachment export cache.")
        }

        val safeFileName = sanitizeFileName(fileName).ifBlank { "attachment.bin" }
        val file = File(exportDir, safeFileName)
        file.writeBytes(bytes)
        return file
    }

    internal fun sanitizeFileName(rawName: String): String =
        rawName
            .replace(Regex("""[\\/:*?"<>|\p{Cntrl}]"""), "_")
            .trim { it <= ' ' || it == '.' }
            .ifBlank { "" }

    private fun fallbackFileName(kind: AttachmentViewerKind, mediaType: String): String =
        "attachment" + (
            extensionForMediaType(mediaType).ifBlank {
                when (kind) {
                    AttachmentViewerKind.Text -> ".txt"
                    AttachmentViewerKind.Audio -> ".m4a"
                    AttachmentViewerKind.Pdf -> ".pdf"
                    AttachmentViewerKind.Image -> ".bin"
                }
            }
            )

    private fun extensionForMediaType(mediaType: String): String =
        when (mediaType.trim().lowercase(Locale.ROOT)) {
            "application/pdf" -> ".pdf"
            "text/markdown" -> ".md"
            "text/plain" -> ".txt"
            "audio/mp4", "audio/aac" -> ".m4a"
            "audio/mpeg" -> ".mp3"
            "audio/webm" -> ".webm"
            "audio/wav", "audio/x-wav" -> ".wav"
            "image/jpeg", "image/jpg" -> ".jpg"
            "image/png" -> ".png"
            "image/gif" -> ".gif"
            "image/webp" -> ".webp"
            else -> ""
        }
}
