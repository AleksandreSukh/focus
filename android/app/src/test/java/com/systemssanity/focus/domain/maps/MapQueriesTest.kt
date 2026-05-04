package com.systemssanity.focus.domain.maps

import com.systemssanity.focus.domain.model.MindMapDocument
import com.systemssanity.focus.domain.model.Node
import com.systemssanity.focus.domain.model.TaskState
import kotlin.test.Test
import kotlin.test.assertFalse
import kotlin.test.assertTrue

class MapQueriesTest {
    @Test
    fun detectsDoneDescendantsBelowSelectedNode() {
        val doneGrandchild = Node(uniqueIdentifier = "done", name = "Done", taskState = TaskState.Done)
        val child = Node(uniqueIdentifier = "child", name = "Child", children = listOf(doneGrandchild))
        val sibling = Node(uniqueIdentifier = "sibling", name = "Sibling", taskState = TaskState.Todo)
        val document = MindMapDocument(rootNode = Node(uniqueIdentifier = "root", name = "Root", children = listOf(child, sibling)))

        assertTrue(MapQueries.hasDoneDescendants(document, "child"))
        assertTrue(MapQueries.hasDoneDescendants(document, "root"))
        assertFalse(MapQueries.hasDoneDescendants(document, "sibling"))
        assertFalse(MapQueries.hasDoneDescendants(document, "missing"))
    }
}
