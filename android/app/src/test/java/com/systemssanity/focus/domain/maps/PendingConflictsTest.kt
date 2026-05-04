package com.systemssanity.focus.domain.maps

import com.systemssanity.focus.domain.model.TaskState
import kotlin.test.Test
import kotlin.test.assertEquals
import kotlin.test.assertIs

class PendingConflictsTest {
    @Test
    fun describesQueuedOperationsWithPwaLabels() {
        assertEquals(
            "text change in Map",
            PendingConflicts.describeOperation(
                MapMutation.EditNodeText(
                    filePath = "FocusMaps/Map.json",
                    nodeId = "node",
                    text = "Text",
                    timestamp = "2026-05-04T10:00:00Z",
                    commitMessage = "edit",
                ),
                "Map",
            ),
        )
        assertEquals(
            "task state update in Map",
            PendingConflicts.describeOperation(
                MapMutation.SetTaskState(
                    filePath = "FocusMaps/Map.json",
                    nodeId = "node",
                    taskState = TaskState.Done,
                    timestamp = "2026-05-04T10:00:00Z",
                    commitMessage = "task",
                ),
                "Map",
            ),
        )
        assertEquals(
            "new child task in Map",
            PendingConflicts.describeOperation(
                MapMutation.AddChildTask(
                    filePath = "FocusMaps/Map.json",
                    parentNodeId = "parent",
                    newNodeId = "child",
                    text = "Task",
                    timestamp = "2026-05-04T10:00:00Z",
                    commitMessage = "add",
                ),
                "Map",
            ),
        )
        assertEquals(
            "map rename in Map",
            PendingConflicts.describeOperation(
                MapMutation.RenameMap(
                    filePath = "FocusMaps/Old.json",
                    newFilePath = "FocusMaps/New.json",
                    nodeId = "root",
                    text = "New",
                    timestamp = "2026-05-04T10:00:00Z",
                    commitMessage = "rename",
                ),
                "Map",
            ),
        )
    }

    @Test
    fun pendingConflictLabelsAreStable() {
        val entry = PendingConflicts.build("FocusMaps/Map.json", "Map")

        assertEquals("1 queued change needs a local or remote choice.", PendingConflicts.pendingText(1))
        assertEquals("Resolve conflict for Map", PendingConflicts.resolveLabel(entry))
        assertEquals("Retry syncing Map", PendingConflicts.retryLabel(entry))
        assertEquals("Keep my change", PendingConflicts.choiceLabel(PendingConflictResolution.Local))
        assertEquals("Use remote version", PendingConflicts.choiceLabel(PendingConflictResolution.Remote))
    }

    @Test
    fun conflictDiffReportsAddsAndRemoves() {
        val result = ConflictDiffs.build(
            remoteText = "{\n  \"name\": \"Remote\"\n}\n",
            localText = "{\n  \"name\": \"Local\"\n}\n",
        )

        val lines = assertIs<ConflictDiffResult.Lines>(result).lines
        assertEquals(true, lines.any { it is ConflictDiffLine.Text && it.type == ConflictDiffLineType.Remove })
        assertEquals(true, lines.any { it is ConflictDiffLine.Text && it.type == ConflictDiffLineType.Add })
    }

    @Test
    fun conflictDiffCollapsesContextAndHandlesSpecialStates() {
        val diff = ConflictDiffs.computeLineDiff(
            leftLines = (1..20).map { "line $it" },
            rightLines = (1..20).map { if (it == 10) "line changed" else "line $it" },
        ) ?: error("Expected diff")
        val collapsed = ConflictDiffs.collapseContext(diff, contextLines = 1)

        assertEquals(true, collapsed.any { it is ConflictDiffLine.Ellipsis })
        assertIs<ConflictDiffResult.NoChanges>(ConflictDiffs.build("same\n", "same\n"))
        assertIs<ConflictDiffResult.TooLarge>(
            ConflictDiffs.build(
                remoteText = (1..501).joinToString("\n") { "r$it" },
                localText = "local",
            ),
        )
    }
}
