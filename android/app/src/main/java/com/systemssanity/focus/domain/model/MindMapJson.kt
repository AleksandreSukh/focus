package com.systemssanity.focus.domain.model

import kotlinx.serialization.encodeToString
import kotlinx.serialization.json.Json
import kotlinx.serialization.json.JsonArray
import kotlinx.serialization.json.JsonElement
import kotlinx.serialization.json.JsonObject
import kotlinx.serialization.json.jsonObject

object MindMapJson {
    private val json = Json {
        encodeDefaults = true
        ignoreUnknownKeys = true
        prettyPrint = true
    }

    fun parse(content: String, fallbackTimestampIso: String = ClockProvider.nowIsoSeconds()): MindMapDocument {
        val element = json.parseToJsonElement(content.ifBlank { "{}" })
        val canonical = canonicalizeKeys(element)
        val parsed = json.decodeFromJsonElement(MindMapDocument.serializer(), canonical)
        return normalize(parsed, fallbackTimestampIso)
    }

    fun serialize(document: MindMapDocument): String =
        json.encodeToString(normalize(document)).trimEnd() + "\n"

    fun normalize(
        document: MindMapDocument,
        fallbackTimestampIso: String = ClockProvider.nowIsoSeconds(),
    ): MindMapDocument {
        val normalizedRoot = normalizeNode(
            document.rootNode,
            fallbackTimestampIso = fallbackTimestampIso,
            number = 1,
        )
        val updatedAt = document.updatedAt.ifBlank {
            normalizedRoot.metadata?.updatedAtUtc ?: fallbackTimestampIso
        }
        return MindMapDocument(rootNode = normalizedRoot, updatedAt = normalizeTimestamp(updatedAt, fallbackTimestampIso))
    }

    private fun normalizeNode(
        node: Node,
        fallbackTimestampIso: String,
        number: Int,
    ): Node {
        val id = node.uniqueIdentifier.takeIf(::isGuid) ?: java.util.UUID.randomUUID().toString()
        val metadata = normalizeMetadata(node.effectiveMetadata(fallbackTimestampIso), fallbackTimestampIso)
        val normalizedChildren = node.children.mapIndexed { index, child ->
            normalizeNode(child, fallbackTimestampIso, index + 1)
        }
        val hideDoneExplicit = if (node.hideDoneTasksExplicit == true) true else null
        return node.copy(
            uniqueIdentifier = id,
            name = sanitizeText(node.name),
            children = normalizedChildren,
            links = node.links.mapValues { (_, link) -> normalizeLink(link) },
            number = number.coerceAtLeast(1),
            hideDoneTasksExplicit = hideDoneExplicit,
            metadata = metadata,
        )
    }

    private fun normalizeMetadata(metadata: NodeMetadata, fallbackTimestampIso: String): NodeMetadata {
        val created = normalizeTimestamp(metadata.createdAtUtc, fallbackTimestampIso)
        return metadata.copy(
            createdAtUtc = created,
            updatedAtUtc = normalizeTimestamp(metadata.updatedAtUtc, created),
            source = metadata.source?.takeIf { it.isNotBlank() } ?: NodeMetadataSources.Manual,
            attachments = metadata.attachments.map { attachment ->
                attachment.copy(
                    id = attachment.id.takeIf(::isGuid) ?: java.util.UUID.randomUUID().toString(),
                    relativePath = attachment.relativePath.substringAfterLast('/').substringAfterLast('\\'),
                    createdAtUtc = normalizeTimestamp(attachment.createdAtUtc, created),
                )
            },
        )
    }

    private fun normalizeLink(link: Link): Link =
        link.copy(id = link.id.trim())

    private fun canonicalizeKeys(element: JsonElement): JsonElement =
        when (element) {
            is JsonArray -> JsonArray(element.map(::canonicalizeKeys))
            is JsonObject -> JsonObject(
                element.entries.associate { (key, value) ->
                    canonicalKey(key) to canonicalizeKeys(value)
                },
            )
            else -> element
        }

    private fun canonicalKey(key: String): String =
        key.replaceFirstChar { first ->
            if (first.isUpperCase()) first.lowercaseChar() else first
        }

    private fun sanitizeText(input: String): String =
        input.filter { character ->
            !character.isISOControl() || character == '\r' || character == '\n' || character == '\t'
        }

    private fun normalizeTimestamp(value: String, fallback: String): String =
        runCatching {
            java.time.format.DateTimeFormatter.ISO_INSTANT.format(
                java.time.Instant.parse(value).truncatedTo(java.time.temporal.ChronoUnit.SECONDS),
            )
        }.getOrElse { fallback }

    private fun isGuid(value: String): Boolean =
        runCatching { java.util.UUID.fromString(value) }.isSuccess
}
