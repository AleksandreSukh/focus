package com.systemssanity.focus.ui

import com.systemssanity.focus.domain.maps.UnreadableMapEntry
import com.systemssanity.focus.domain.maps.UnreadableMapReason
import com.systemssanity.focus.domain.model.MapSnapshot
import com.systemssanity.focus.domain.model.MindMapDocument
import com.systemssanity.focus.domain.model.Node
import com.systemssanity.focus.domain.model.TaskState
import kotlin.test.Test
import kotlin.test.assertEquals
import kotlin.test.assertFalse
import kotlin.test.assertNull
import kotlin.test.assertTrue

class FocusRoutesTest {
    @Test
    fun hashRoutesMatchPwaShape() {
        assertEquals("#maps", FocusRoutes.buildHashRoute(FocusRoute.Maps))
        assertEquals("#tasks", FocusRoutes.buildHashRoute(FocusRoute.Tasks))
        assertEquals(
            "#map/maps%2Falpha.json",
            FocusRoutes.buildHashRoute(FocusRoute.Map("maps/alpha.json", "root-node"), rootNodeId = "root-node"),
        )
        assertEquals(
            "#map/maps%2Falpha.json?node=branch-node",
            FocusRoutes.buildHashRoute(FocusRoute.Map("maps/alpha.json", "branch-node"), rootNodeId = "root-node"),
        )
        assertEquals(
            ParsedFocusRoute(FocusRoute.Map("maps/alpha.json", "branch-node")),
            FocusRoutes.parseHashRoute("#map/maps%2Falpha.json?node=branch-node"),
        )
        assertEquals(ParsedFocusRoute(FocusRoute.Maps), FocusRoutes.parseHashRoute(""))
        assertTrue(FocusRoutes.parseHashRoute("#map/").isInvalid)
    }

    @Test
    fun deepLinksUsePwaHashPayloadsInsideFocusScheme() {
        val route = FocusRoute.Map("Focus Maps/alpha+beta.json", "node one")

        assertEquals(
            "focus://open#map/Focus%20Maps%2Falpha%2Bbeta.json?node=node%20one",
            FocusRoutes.buildDeepLinkUri(route, rootNodeId = "root"),
        )
        assertEquals(
            ParsedFocusRoute(route),
            FocusRoutes.parseDeepLinkUri("focus://open#map/Focus%20Maps%2Falpha%2Bbeta.json?node=node%20one"),
        )
        assertEquals(ParsedFocusRoute(FocusRoute.Tasks), FocusRoutes.parseDeepLinkUri("https://example.test/app#tasks"))
        assertNull(FocusRoutes.parseDeepLinkUri(null))
        assertNull(FocusRoutes.parseDeepLinkUri("focus://open"))
    }

    @Test
    fun viewportResolutionFallsBackToVisibleAncestorOrRoot() {
        val done = Node(uniqueIdentifier = "done", name = "Done", taskState = TaskState.Done)
        val todo = Node(uniqueIdentifier = "todo", name = "Todo", taskState = TaskState.Todo)
        val branch = Node(
            uniqueIdentifier = "branch",
            name = "Branch",
            hideDoneTasks = true,
            children = listOf(done, todo),
        )
        val document = MindMapDocument(rootNode = Node(uniqueIdentifier = "root", name = "Root", children = listOf(branch)))

        assertEquals("root", resolveViewportNodeId(document, ""))
        assertEquals("branch", resolveViewportNodeId(document, "branch"))
        assertEquals("branch", resolveViewportNodeId(document, "done"))
        assertEquals("todo", resolveViewportNodeId(document, "todo"))
        assertEquals("root", resolveViewportNodeId(document, "missing"))
    }

    @Test
    fun routeResolutionCanonicalizesNodesAndMissingMaps() {
        val document = MindMapDocument(
            rootNode = Node(
                uniqueIdentifier = "root",
                name = "Root",
                children = listOf(Node(uniqueIdentifier = "child", name = "Child")),
            ),
        )
        val snapshot = snapshot("FocusMaps/Map.json", document)
        val unreadable = UnreadableMapEntry(
            filePath = "FocusMaps/Broken.json",
            fileName = "Broken.json",
            mapName = "Broken",
            revision = "rev",
            reason = UnreadableMapReason.InvalidJson,
            message = "Invalid JSON.",
            rawText = "{}",
        )

        assertEquals(
            FocusRouteResolution(FocusRoute.Map("FocusMaps/Map.json", "child")),
            resolveFocusRoute(FocusRoute.Map("FocusMaps/Map.json", "child"), listOf(snapshot)),
        )
        assertEquals(
            FocusRouteResolution(FocusRoute.Map("FocusMaps/Map.json", "root"), canonicalized = true),
            resolveFocusRoute(FocusRoute.Map("FocusMaps/Map.json", "missing"), listOf(snapshot)),
        )
        assertEquals(
            FocusRouteResolution(
                route = FocusRoute.Maps,
                canonicalized = true,
                statusMessage = "The requested map needs repair before it can be opened.",
            ),
            resolveFocusRoute(FocusRoute.Map("FocusMaps/Broken.json", "root"), listOf(snapshot), listOf(unreadable)),
        )
        assertEquals(
            "The requested map is no longer available.",
            resolveFocusRoute(FocusRoute.Map("FocusMaps/Missing.json"), listOf(snapshot)).statusMessage,
        )
    }

    @Test
    fun nativeRouteHistoryPushesReplacesAndMovesBackForward() {
        val maps = FocusRoute.Maps
        val tasks = FocusRoute.Tasks
        val map = FocusRoute.Map("FocusMaps/Map.json", "root")
        val child = FocusRoute.Map("FocusMaps/Map.json", "child")

        val pushed = NativeRouteHistory(maps).push(tasks).push(map)
        assertEquals(map, pushed.current)
        assertEquals(listOf(maps, tasks), pushed.backStack)
        assertEquals(emptyList(), pushed.forwardStack)
        assertEquals(pushed, pushed.push(map))

        val back = pushed.goBack()
        assertEquals(tasks, back.current)
        assertEquals(listOf(maps), back.backStack)
        assertEquals(listOf(map), back.forwardStack)

        val forward = back.goForward()
        assertEquals(pushed, forward)

        val replaced = pushed.replace(child)
        assertEquals(child, replaced.current)
        assertEquals(listOf(maps, tasks), replaced.backStack)

        val clearedForward = back.push(child)
        assertEquals(child, clearedForward.current)
        assertEquals(emptyList(), clearedForward.forwardStack)
    }

    @Test
    fun workspaceTabsHideForFocusedSubtreeRoutes() {
        val document = MindMapDocument(
            rootNode = Node(
                uniqueIdentifier = "root",
                name = "Root",
                children = listOf(Node(uniqueIdentifier = "child", name = "Child")),
            ),
        )

        assertTrue(shouldShowWorkspaceTabs(FocusRoute.Maps, document))
        assertTrue(shouldShowWorkspaceTabs(FocusRoute.Tasks, document))
        assertTrue(shouldShowWorkspaceTabs(FocusRoute.Map("FocusMaps/Map.json", "root"), document))
        assertFalse(shouldShowWorkspaceTabs(FocusRoute.Map("FocusMaps/Map.json", "child"), document))
        assertEquals("Go back", nativeRouteBackLabel(canGoBack = true))
        assertEquals("Go back unavailable", nativeRouteBackLabel(canGoBack = false))
        assertEquals("Go forward", nativeRouteForwardLabel(canGoForward = true))
        assertEquals("Go forward unavailable", nativeRouteForwardLabel(canGoForward = false))
    }

    private fun snapshot(filePath: String, document: MindMapDocument): MapSnapshot =
        MapSnapshot(
            filePath = filePath,
            fileName = filePath.substringAfterLast('/'),
            mapName = filePath.substringAfterLast('/').removeSuffix(".json"),
            document = document,
            revision = "rev",
            loadedAtMillis = 0,
        )
}
