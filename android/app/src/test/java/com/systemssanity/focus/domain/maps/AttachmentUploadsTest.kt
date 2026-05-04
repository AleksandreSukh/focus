package com.systemssanity.focus.domain.maps

import kotlin.test.Test
import kotlin.test.assertEquals
import kotlin.test.assertNull

class AttachmentUploadsTest {
    @Test
    fun validatesPwaCompatibleAttachmentTypesAndSize() {
        assertNull(AttachmentUploads.validationError(AttachmentUploads.MaxBytes, "image/png", "photo.png"))
        assertNull(AttachmentUploads.validationError(12, "audio/webm", "voice.webm"))
        assertNull(AttachmentUploads.validationError(12, "application/pdf", "scan.pdf"))
        assertNull(AttachmentUploads.validationError(12, "text/plain", "notes.txt"))
        assertNull(AttachmentUploads.validationError(12, "text/markdown", "notes.md"))

        assertEquals(
            "File is too large (10.0 MB). Maximum size is 10 MB.",
            AttachmentUploads.validationError(AttachmentUploads.MaxBytes + 1, "image/png", "photo.png"),
        )
        assertEquals(
            "Unsupported file type \"application/zip\". Attach an image, audio, PDF, or text file.",
            AttachmentUploads.validationError(12, "application/zip", "archive.zip"),
        )
    }

    @Test
    fun markdownFallbackAcceptsMissingOrOctetStreamMediaType() {
        assertEquals("text/markdown", AttachmentUploads.normalizedMediaType("", "plan.md"))
        assertEquals("text/markdown", AttachmentUploads.normalizedMediaType("application/octet-stream", "plan.markdown"))
        assertNull(AttachmentUploads.validationError(12, "", "plan.md"))
        assertNull(AttachmentUploads.validationError(12, "application/octet-stream", "plan.markdown"))
    }

    @Test
    fun displayNameExtensionAndRelativePathMatchPwaRules() {
        assertEquals("photo.JPG", AttachmentUploads.displayName("content://downloads/photo.JPG"))
        assertEquals("Attachment", AttachmentUploads.displayName(" "))
        assertEquals(".jpg", AttachmentUploads.extension("photo.JPG"))
        assertEquals("", AttachmentUploads.extension("README"))
        assertEquals("11111111-1111-4111-8111-111111111111.jpg", AttachmentUploads.relativePath("11111111-1111-4111-8111-111111111111", "photo.JPG"))
    }

    @Test
    fun buildsAttachmentMetadataFromPreparedFileInfo() {
        val attachment = AttachmentUploads.attachment(
            attachmentId = "11111111-1111-4111-8111-111111111111",
            displayName = "Plan.md",
            mediaType = "text/markdown",
            createdAtUtc = "2026-05-04T10:00:00Z",
        )

        assertEquals("11111111-1111-4111-8111-111111111111", attachment.id)
        assertEquals("11111111-1111-4111-8111-111111111111.md", attachment.relativePath)
        assertEquals("text/markdown", attachment.mediaType)
        assertEquals("Plan.md", attachment.displayName)
        assertEquals("2026-05-04T10:00:00Z", attachment.createdAtUtc)
    }
}
