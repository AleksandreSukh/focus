package com.systemssanity.focus.ui

import com.systemssanity.focus.domain.maps.MapQueries
import com.systemssanity.focus.domain.maps.UnreadableMapEntry
import com.systemssanity.focus.domain.model.MapSnapshot
import com.systemssanity.focus.domain.model.MindMapDocument
import com.systemssanity.focus.domain.model.Node
import com.systemssanity.focus.domain.model.TaskState
import java.io.ByteArrayOutputStream
import java.net.URI
import java.nio.charset.StandardCharsets

internal sealed interface FocusRoute {
    data object Maps : FocusRoute
    data object Tasks : FocusRoute
    data class Map(val filePath: String, val nodeId: String = "") : FocusRoute
}

internal data class ParsedFocusRoute(
    val route: FocusRoute,
    val isInvalid: Boolean = false,
)

internal data class NativeRouteRequest(
    val route: FocusRoute,
    val version: Long,
)

internal data class FocusRouteResolution(
    val route: FocusRoute,
    val canonicalized: Boolean = false,
    val statusMessage: String = "",
)

internal data class NativeRouteHistory(
    val current: FocusRoute = FocusRoute.Maps,
    val backStack: List<FocusRoute> = emptyList(),
    val forwardStack: List<FocusRoute> = emptyList(),
) {
    val canGoBack: Boolean get() = backStack.isNotEmpty()
    val canGoForward: Boolean get() = forwardStack.isNotEmpty()

    fun push(route: FocusRoute): NativeRouteHistory =
        if (route == current) {
            this
        } else {
            copy(
                current = route,
                backStack = backStack + current,
                forwardStack = emptyList(),
            )
        }

    fun replace(route: FocusRoute): NativeRouteHistory =
        if (route == current) this else copy(current = route)

    fun goBack(): NativeRouteHistory =
        if (!canGoBack) {
            this
        } else {
            copy(
                current = backStack.last(),
                backStack = backStack.dropLast(1),
                forwardStack = listOf(current) + forwardStack,
            )
        }

    fun goForward(): NativeRouteHistory =
        if (!canGoForward) {
            this
        } else {
            copy(
                current = forwardStack.first(),
                backStack = backStack + current,
                forwardStack = forwardStack.drop(1),
            )
        }
}

internal object FocusRoutes {
    const val MapsHash = "#maps"
    const val TasksHash = "#tasks"
    const val DeepLinkBase = "focus://open"

    fun buildHashRoute(route: FocusRoute, rootNodeId: String = ""): String =
        when (route) {
            FocusRoute.Maps -> MapsHash
            FocusRoute.Tasks -> TasksHash
            is FocusRoute.Map -> {
                val base = "#map/${encodeUriComponent(route.filePath)}"
                val nodeId = route.nodeId.trim()
                val rootId = rootNodeId.trim()
                if (nodeId.isNotBlank() && nodeId != rootId) {
                    "$base?node=${encodeUriComponent(nodeId)}"
                } else {
                    base
                }
            }
        }

    fun buildDeepLinkUri(route: FocusRoute, rootNodeId: String = ""): String =
        "$DeepLinkBase${buildHashRoute(route, rootNodeId)}"

    fun parseHashRoute(hashValue: String?): ParsedFocusRoute {
        val routeText = hashValue.orEmpty().let { value ->
            if (value.startsWith("#")) value.drop(1) else value
        }

        if (routeText.isBlank() || routeText == "maps") {
            return ParsedFocusRoute(FocusRoute.Maps)
        }
        if (routeText == "tasks") {
            return ParsedFocusRoute(FocusRoute.Tasks)
        }
        if (routeText.startsWith("map/")) {
            val rawMapRoute = routeText.drop(4)
            val queryIndex = rawMapRoute.indexOf('?')
            val encodedPath = if (queryIndex >= 0) rawMapRoute.substring(0, queryIndex) else rawMapRoute
            val queryText = if (queryIndex >= 0) rawMapRoute.substring(queryIndex + 1) else ""
            if (encodedPath.isBlank()) {
                return ParsedFocusRoute(FocusRoute.Maps, isInvalid = true)
            }
            return runCatching {
                val nodeId = queryText
                    .split('&')
                    .firstNotNullOfOrNull { item ->
                        val keyValue = item.split('=', limit = 2)
                        if (keyValue.firstOrNull() == "node") keyValue.getOrNull(1).orEmpty() else null
                    }
                    .orEmpty()
                ParsedFocusRoute(
                    FocusRoute.Map(
                        filePath = decodeUriComponent(encodedPath),
                        nodeId = decodeUriComponent(nodeId).trim(),
                    ),
                )
            }.getOrElse {
                ParsedFocusRoute(FocusRoute.Maps, isInvalid = true)
            }
        }

        return ParsedFocusRoute(FocusRoute.Maps, isInvalid = true)
    }

    fun parseDeepLinkUri(uriText: String?): ParsedFocusRoute? {
        val text = uriText?.trim().orEmpty()
        if (text.isBlank()) return null
        if (text.startsWith("#")) return parseHashRoute(text)
        return runCatching {
            val fragment = URI(text).rawFragment ?: return null
            parseHashRoute("#$fragment")
        }.getOrNull() ?: ParsedFocusRoute(FocusRoute.Maps, isInvalid = true)
    }

    private fun encodeUriComponent(value: String): String {
        val unreserved = "ABCDEFGHIJKLMNOPQRSTUVWXYZabcdefghijklmnopqrstuvwxyz0123456789-_.!~*'()"
        return buildString {
            value.toByteArray(StandardCharsets.UTF_8).forEach { byte ->
                val char = byte.toInt().toChar()
                if (char in unreserved) {
                    append(char)
                } else {
                    append('%')
                    append(byte.toInt().and(0xff).toString(16).uppercase().padStart(2, '0'))
                }
            }
        }
    }

    private fun decodeUriComponent(value: String): String {
        val output = ByteArrayOutputStream()
        var index = 0
        while (index < value.length) {
            val char = value[index]
            if (char == '%' && index + 2 < value.length) {
                val hex = value.substring(index + 1, index + 3)
                val byte = hex.toIntOrNull(16) ?: error("Invalid URI escape")
                output.write(byte)
                index += 3
            } else {
                output.write(char.toString().toByteArray(StandardCharsets.UTF_8))
                index += 1
            }
        }
        return output.toString(StandardCharsets.UTF_8.name())
    }
}

internal fun nativeRouteRequestFromUri(uriText: String?, version: Long): NativeRouteRequest? =
    FocusRoutes.parseDeepLinkUri(uriText)?.let { parsed ->
        NativeRouteRequest(parsed.route, version)
    }

internal fun resolveFocusRoute(
    route: FocusRoute,
    snapshots: List<MapSnapshot>,
    unreadableMaps: List<UnreadableMapEntry> = emptyList(),
): FocusRouteResolution =
    when (route) {
        FocusRoute.Maps,
        FocusRoute.Tasks -> FocusRouteResolution(route)
        is FocusRoute.Map -> {
            val snapshot = snapshots.firstOrNull { it.filePath == route.filePath }
            if (snapshot == null) {
                val message = if (unreadableMaps.any { it.filePath == route.filePath }) {
                    "The requested map needs repair before it can be opened."
                } else {
                    "The requested map is no longer available."
                }
                FocusRouteResolution(
                    route = FocusRoute.Maps,
                    canonicalized = true,
                    statusMessage = message,
                )
            } else {
                val rootId = getDocumentRootNodeId(snapshot.document)
                val viewportNodeId = resolveViewportNodeId(snapshot.document, route.nodeId.ifBlank { rootId })
                val canonicalRoute = FocusRoute.Map(snapshot.filePath, viewportNodeId)
                FocusRouteResolution(
                    route = canonicalRoute,
                    canonicalized = canonicalRoute != route,
                )
            }
        }
    }

internal fun getDocumentRootNodeId(document: MindMapDocument): String =
    document.rootNode.uniqueIdentifier

internal fun resolveViewportNodeId(document: MindMapDocument, nodeId: String = ""): String {
    val rootNode = document.rootNode
    val rootId = rootNode.uniqueIdentifier
    val requestedNodeId = nodeId.trim().ifBlank { rootId }
    var resolvedNodeId = ""

    fun visit(node: Node, ancestorHidesDone: Boolean, nearestVisibleAncestorId: String, isRoot: Boolean): Boolean {
        val nodeIsHidden = !isRoot && ancestorHidesDone && node.taskState == TaskState.Done
        val visibleNodeId = if (nodeIsHidden) {
            nearestVisibleAncestorId
        } else {
            node.uniqueIdentifier.ifBlank { nearestVisibleAncestorId }
        }
        if (node.uniqueIdentifier == requestedNodeId) {
            resolvedNodeId = visibleNodeId
            return true
        }
        val hideDoneStateForChildren = MapQueries.getTreeHideDoneState(node, ancestorHidesDone)
        return node.children.any { child ->
            visit(child, hideDoneStateForChildren, visibleNodeId, isRoot = false)
        }
    }

    visit(rootNode, ancestorHidesDone = false, nearestVisibleAncestorId = rootId, isRoot = true)
    return resolvedNodeId.ifBlank { rootId }
}

internal fun isSubtreeRoute(route: FocusRoute, document: MindMapDocument?): Boolean {
    if (route !is FocusRoute.Map || document == null) return false
    val rootId = getDocumentRootNodeId(document)
    val viewportNodeId = resolveViewportNodeId(document, route.nodeId)
    return rootId.isNotBlank() && viewportNodeId.isNotBlank() && viewportNodeId != rootId
}

internal fun shouldShowWorkspaceTabs(route: FocusRoute, document: MindMapDocument?): Boolean =
    !isSubtreeRoute(route, document)

internal fun nativeRouteBackLabel(canGoBack: Boolean): String =
    if (canGoBack) "Go back" else "Go back unavailable"

internal fun nativeRouteForwardLabel(canGoForward: Boolean): String =
    if (canGoForward) "Go forward" else "Go forward unavailable"
