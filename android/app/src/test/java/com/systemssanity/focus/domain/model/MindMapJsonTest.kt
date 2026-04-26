package com.systemssanity.focus.domain.model

import kotlin.test.Test
import kotlin.test.assertEquals
import kotlin.test.assertTrue

class MindMapJsonTest {
    @Test
    fun readsLegacyPascalCaseMapFields() {
        val document = MindMapJson.parse(
            """
            {
              "UpdatedAt": "2026-04-25T10:00:00Z",
              "RootNode": {
                "NodeType": 0,
                "UniqueIdentifier": "11111111-1111-4111-8111-111111111111",
                "Name": "Root",
                "Children": [
                  {
                    "NodeType": 0,
                    "UniqueIdentifier": "22222222-2222-4222-8222-222222222222",
                    "Name": "Task",
                    "Children": [],
                    "Links": {},
                    "Number": 1,
                    "TaskState": 1
                  }
                ],
                "Links": {},
                "Number": 1,
                "TaskState": 0
              }
            }
            """.trimIndent(),
        )

        assertEquals("Root", document.rootNode.name)
        assertEquals(TaskState.Todo, document.rootNode.children.single().taskState)
    }

    @Test
    fun serializesCanonicalCamelCaseJson() {
        val json = MindMapJson.serialize(MindMapDocument(rootNode = Node(name = "Root")))

        assertTrue(json.contains("\"rootNode\""))
        assertTrue(json.contains("\"uniqueIdentifier\""))
    }
}
