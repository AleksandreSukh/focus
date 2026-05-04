package com.systemssanity.focus.domain.maps

import com.systemssanity.focus.domain.model.NodeAttachment
import kotlin.test.Test
import kotlin.test.assertEquals
import kotlin.test.assertFalse
import kotlin.test.assertTrue

class AttachmentViewersTest {
    @Test
    fun routesAttachmentsToViewerKinds() {
        assertEquals(AttachmentViewerKind.Text, AttachmentViewers.viewerKind(attachment(mediaType = "text/plain")))
        assertEquals(AttachmentViewerKind.Audio, AttachmentViewers.viewerKind(attachment(mediaType = "audio/mp4")))
        assertEquals(AttachmentViewerKind.Pdf, AttachmentViewers.viewerKind(attachment(mediaType = "application/pdf")))
        assertEquals(AttachmentViewerKind.Pdf, AttachmentViewers.viewerKind(attachment(relativePath = "scan.pdf")))
        assertEquals(AttachmentViewerKind.Image, AttachmentViewers.viewerKind(attachment(mediaType = "image/png")))
        assertEquals(AttachmentViewerKind.Image, AttachmentViewers.viewerKind(attachment(mediaType = "application/octet-stream")))
    }

    @Test
    fun buildsStableTitlesAndActionLabels() {
        val named = attachment(displayName = "Camera photo", relativePath = "camera.jpg")
        val pathOnly = attachment(displayName = " ", relativePath = "notes.txt")

        assertEquals("Camera photo", AttachmentViewers.title(named))
        assertEquals("notes.txt", AttachmentViewers.title(pathOnly))
        assertEquals("View Camera photo", AttachmentViewers.openActionLabel(named))
        assertEquals("Close Camera photo", AttachmentViewers.closeActionLabel(named))
    }

    @Test
    fun formatsByteSizesAndViewability() {
        assertEquals("0 B", AttachmentViewers.formatByteSize(-1))
        assertEquals("512 B", AttachmentViewers.formatByteSize(512))
        assertEquals("2.0 KB", AttachmentViewers.formatByteSize(2048))
        assertEquals("2.0 MB", AttachmentViewers.formatByteSize(2 * 1024 * 1024))
        assertTrue(AttachmentViewers.canView(attachment(relativePath = "file.txt")))
        assertFalse(AttachmentViewers.canView(attachment(relativePath = " ")))
    }

    @Test
    fun unsupportedPreviewMessagesAreKindSpecific() {
        assertEquals(
            "This audio attachment was loaded, but Android could not prepare playback.",
            AttachmentViewers.unsupportedPreviewMessage(AttachmentViewerKind.Audio),
        )
        assertEquals(
            "This PDF was loaded, but Android could not preview it inline.",
            AttachmentViewers.unsupportedPreviewMessage(AttachmentViewerKind.Pdf),
        )
    }

    private fun attachment(
        mediaType: String = "",
        relativePath: String = "attachment.bin",
        displayName: String = "",
    ): NodeAttachment =
        NodeAttachment(
            id = "attachment-id",
            relativePath = relativePath,
            mediaType = mediaType,
            displayName = displayName,
        )
}
