package com.systemssanity.focus.domain.maps

import com.systemssanity.focus.domain.model.NodeAttachment
import java.io.File
import java.nio.file.Files
import kotlin.test.Test
import kotlin.test.assertContentEquals
import kotlin.test.assertEquals
import kotlin.test.assertFalse
import kotlin.test.assertTrue

class AttachmentExportsTest {
    @Test
    fun derivesSafeFileNamesFromDisplayNameThenRelativePathThenFallback() {
        assertEquals(
            "Trip_photo.jpg",
            AttachmentExports.fileName(
                attachment(displayName = "Trip/photo", relativePath = "camera.JPG"),
                AttachmentViewerKind.Image,
            ),
        )
        assertEquals(
            "notes.md",
            AttachmentExports.fileName(
                attachment(displayName = " ", relativePath = "notes.md", mediaType = "text/markdown"),
                AttachmentViewerKind.Text,
            ),
        )
        assertEquals(
            "attachment.pdf",
            AttachmentExports.fileName(
                attachment(displayName = " ", relativePath = " ", mediaType = "application/pdf"),
                AttachmentViewerKind.Pdf,
            ),
        )
    }

    @Test
    fun preservesUsefulExtensionsForCommonViewerKinds() {
        assertEquals(
            "Camera.jpg",
            AttachmentExports.fileName(
                attachment(displayName = "Camera", mediaType = "image/jpeg"),
                AttachmentViewerKind.Image,
            ),
        )
        assertEquals(
            "Voice.m4a",
            AttachmentExports.fileName(
                attachment(displayName = "Voice", mediaType = "audio/mp4"),
                AttachmentViewerKind.Audio,
            ),
        )
        assertEquals(
            "Plan.txt",
            AttachmentExports.fileName(
                attachment(displayName = "Plan", mediaType = "text/plain"),
                AttachmentViewerKind.Text,
            ),
        )
        assertEquals(
            "Scan.pdf",
            AttachmentExports.fileName(
                attachment(displayName = "Scan", mediaType = "application/pdf"),
                AttachmentViewerKind.Pdf,
            ),
        )
    }

    @Test
    fun derivesMimeTypeFromLoadedBytesThenMetadataThenFallback() {
        assertEquals(
            "image/png",
            AttachmentExports.mimeType("image/png", attachment(mediaType = "image/jpeg"), AttachmentViewerKind.Image),
        )
        assertEquals(
            "audio/mp4",
            AttachmentExports.mimeType(" ", attachment(mediaType = "audio/mp4"), AttachmentViewerKind.Audio),
        )
        assertEquals(
            "text/plain",
            AttachmentExports.mimeType(" ", attachment(mediaType = " "), AttachmentViewerKind.Text),
        )
        assertEquals(
            "application/octet-stream",
            AttachmentExports.mimeType(" ", attachment(mediaType = " "), AttachmentViewerKind.Image),
        )
    }

    @Test
    fun buildsSaveAndShareLabels() {
        val attachment = attachment(displayName = "Plan", mediaType = "text/plain")

        assertEquals("Save Plan.txt", AttachmentExports.saveActionLabel(attachment, AttachmentViewerKind.Text))
        assertEquals("Share Plan.txt", AttachmentExports.shareActionLabel(attachment, AttachmentViewerKind.Text))
    }

    @Test
    fun exportAvailabilityRequiresLoadedBytesAndNoError() {
        assertTrue(AttachmentExports.canExport(byteArrayOf(1, 2), loading = false, errorMessage = ""))
        assertFalse(AttachmentExports.canExport(null, loading = false, errorMessage = ""))
        assertFalse(AttachmentExports.canExport(byteArrayOf(1), loading = true, errorMessage = ""))
        assertFalse(AttachmentExports.canExport(byteArrayOf(1), loading = false, errorMessage = "Failed"))
    }

    @Test
    fun writesExportFileToCacheSubdirectory() {
        val cacheDir = Files.createTempDirectory("focus-export-test-").toFile()
        try {
            val file = AttachmentExports.writeAttachmentExportFile(
                cacheDir = cacheDir,
                fileName = "bad/name.txt",
                bytes = byteArrayOf(3, 4, 5),
            )

            assertEquals("bad_name.txt", file.name)
            assertEquals(File(cacheDir, "focus-attachment-exports").canonicalPath, file.parentFile?.canonicalPath)
            assertContentEquals(byteArrayOf(3, 4, 5), file.readBytes())
        } finally {
            cacheDir.deleteRecursively()
        }
    }

    private fun attachment(
        displayName: String = "",
        relativePath: String = "attachment.bin",
        mediaType: String = "",
    ): NodeAttachment =
        NodeAttachment(
            id = "attachment-id",
            relativePath = relativePath,
            mediaType = mediaType,
            displayName = displayName,
        )
}
