package com.systemssanity.focus.domain.maps

import com.systemssanity.focus.domain.model.Link
import com.systemssanity.focus.domain.model.MapSnapshot
import com.systemssanity.focus.domain.model.MindMapDocument
import com.systemssanity.focus.domain.model.Node
import kotlin.test.Test
import kotlin.test.assertEquals

class RelatedNodesTest {
    @Test
    fun formatsOutgoingAndBacklinkRelationLabels() {
        assertEquals("relates", RelatedNodes.relationLabel(0))
        assertEquals("prerequisite", RelatedNodes.relationLabel(1))
        assertEquals("todo-with", RelatedNodes.relationLabel(2))
        assertEquals("causes", RelatedNodes.relationLabel(3))
        assertEquals("link", RelatedNodes.relationLabel(99))

        assertEquals("backlink: prerequisite", RelatedNodes.backlinkRelationLabel(1))
        assertEquals("backlink", RelatedNodes.backlinkRelationLabel(99))
    }

    @Test
    fun collectOutgoingResolvesAcrossLoadedMapsOmitsMissingTargetsAndSortsByMapThenPath() {
        val alpha = snapshot(
            "Alpha.json",
            Node(
                uniqueIdentifier = Ids.alphaRoot,
                name = "Alpha",
                children = listOf(
                    Node(
                        uniqueIdentifier = Ids.alphaBranch,
                        name = "Alpha Branch",
                        links = mapOf(
                            Ids.gammaBranch to Link(id = Ids.gammaBranch, relationType = 0),
                            Ids.betaTask to Link(id = " ${Ids.betaTask} ", relationType = 1),
                            Ids.missingTarget to Link(id = Ids.missingTarget, relationType = 3),
                        ),
                    ),
                ),
            ),
        )
        val beta = snapshot(
            "Beta.json",
            Node(
                uniqueIdentifier = Ids.betaRoot,
                name = "Beta",
                children = listOf(Node(uniqueIdentifier = Ids.betaTask, name = "Beta Task")),
            ),
        )
        val gamma = snapshot(
            "Gamma.json",
            Node(
                uniqueIdentifier = Ids.gammaRoot,
                name = "Gamma",
                children = listOf(Node(uniqueIdentifier = Ids.gammaBranch, name = "Gamma Branch")),
            ),
        )

        val entries = RelatedNodes.collectOutgoing(alpha.document.rootNode.children.single(), listOf(gamma, alpha, beta))

        assertEquals(
            listOf(
                RelatedNodeEntry(
                    direction = RelatedNodeEntry.Direction.Outgoing,
                    mapPath = "FocusMaps/Beta.json",
                    mapName = "Beta",
                    nodeId = Ids.betaTask,
                    nodeName = "Beta Task",
                    nodePathSegments = listOf("Beta", "Beta Task"),
                    relationLabel = "prerequisite",
                ),
                RelatedNodeEntry(
                    direction = RelatedNodeEntry.Direction.Outgoing,
                    mapPath = "FocusMaps/Gamma.json",
                    mapName = "Gamma",
                    nodeId = Ids.gammaBranch,
                    nodeName = "Gamma Branch",
                    nodePathSegments = listOf("Gamma", "Gamma Branch"),
                    relationLabel = "relates",
                ),
            ),
            entries,
        )
    }

    @Test
    fun collectBacklinksResolvesAcrossLoadedMapsWithBacklinkLabelsAndSorting() {
        val alpha = snapshot(
            "Alpha.json",
            Node(
                uniqueIdentifier = Ids.alphaRoot,
                name = "Alpha",
                children = listOf(
                    Node(
                        uniqueIdentifier = Ids.alphaBranch,
                        name = "Alpha Branch",
                        links = mapOf(Ids.betaTask to Link(id = Ids.betaTask, relationType = 3)),
                    ),
                ),
            ),
        )
        val beta = snapshot(
            "Beta.json",
            Node(
                uniqueIdentifier = Ids.betaRoot,
                name = "Beta",
                children = listOf(Node(uniqueIdentifier = Ids.betaTask, name = "Beta Task")),
            ),
        )
        val gamma = snapshot(
            "Gamma.json",
            Node(
                uniqueIdentifier = Ids.gammaRoot,
                name = "Gamma",
                children = listOf(
                    Node(
                        uniqueIdentifier = Ids.gammaBranch,
                        name = "Gamma Branch",
                        links = mapOf(Ids.betaTask to Link(id = Ids.betaTask.uppercase(), relationType = 99)),
                    ),
                ),
            ),
        )

        val entries = RelatedNodes.collectBacklinks(Ids.betaTask, listOf(beta, gamma, alpha))

        assertEquals(
            listOf(
                RelatedNodeEntry(
                    direction = RelatedNodeEntry.Direction.Backlink,
                    mapPath = "FocusMaps/Alpha.json",
                    mapName = "Alpha",
                    nodeId = Ids.alphaBranch,
                    nodeName = "Alpha Branch",
                    nodePathSegments = listOf("Alpha", "Alpha Branch"),
                    relationLabel = "backlink: causes",
                ),
                RelatedNodeEntry(
                    direction = RelatedNodeEntry.Direction.Backlink,
                    mapPath = "FocusMaps/Gamma.json",
                    mapName = "Gamma",
                    nodeId = Ids.gammaBranch,
                    nodeName = "Gamma Branch",
                    nodePathSegments = listOf("Gamma", "Gamma Branch"),
                    relationLabel = "backlink",
                ),
            ),
            entries,
        )
    }

    private fun snapshot(fileName: String, rootNode: Node): MapSnapshot =
        MapSnapshot(
            filePath = "FocusMaps/$fileName",
            fileName = fileName,
            mapName = fileName.removeSuffix(".json"),
            document = MindMapDocument(rootNode = rootNode),
            revision = "rev-1",
            loadedAtMillis = 0,
        )

    private object Ids {
        const val alphaRoot = "11111111-1111-4111-8111-111111111111"
        const val alphaBranch = "11111111-1111-4111-8111-111111111112"
        const val betaRoot = "22222222-2222-4222-8222-222222222221"
        const val betaTask = "22222222-2222-4222-8222-222222222222"
        const val gammaRoot = "33333333-3333-4333-8333-333333333331"
        const val gammaBranch = "33333333-3333-4333-8333-333333333332"
        const val missingTarget = "44444444-4444-4444-8444-444444444444"
    }
}
