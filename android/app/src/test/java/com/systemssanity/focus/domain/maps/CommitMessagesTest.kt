package com.systemssanity.focus.domain.maps

import kotlin.test.Test
import kotlin.test.assertEquals

class CommitMessagesTest {
    @Test
    fun nodeStarMessagesUsePwaWireFormat() {
        assertEquals(
            "map:star Project abc-123 -> starred",
            CommitMessages.nodeStar("Project", "abc-123", starred = true),
        )
        assertEquals(
            "map:star Project abc-123 -> unstarred",
            CommitMessages.nodeStar("Project", "abc-123", starred = false),
        )
    }

    @Test
    fun mapRenameMessagesUsePwaWireFormat() {
        assertEquals(
            "map:rename Old Project -> New Project",
            CommitMessages.mapRename("Old Project", "New Project"),
        )
    }

    @Test
    fun attachmentAddMessagesUsePwaWireFormat() {
        assertEquals(
            "map:attach Project camera photo.jpg",
            CommitMessages.attachmentAdd("Project", "camera photo.jpg"),
        )
    }

    @Test
    fun attachmentRemoveMessagesUsePwaWireFormat() {
        assertEquals(
            "map:detach Project camera photo.jpg",
            CommitMessages.attachmentRemove("Project", "camera photo.jpg"),
        )
    }
}
