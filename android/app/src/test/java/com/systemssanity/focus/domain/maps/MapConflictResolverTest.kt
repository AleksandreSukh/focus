package com.systemssanity.focus.domain.maps

import kotlin.test.Test
import kotlin.test.assertEquals
import kotlin.test.assertTrue

class MapConflictResolverTest {
    @Test
    fun detectsConflictMarkers() {
        assertTrue(MapConflictResolver.hasConflictMarkers("<<<<<<< HEAD\n{}\n=======\n{}\n>>>>>>> main"))
    }

    @Test
    fun fallsBackToNewerMapTimestampWhenStructuralMergeCannotResolve() {
        val conflicted = """
            <<<<<<< HEAD
            {"updatedAt":"2026-04-25T10:00:00Z","rootNode":{"uniqueIdentifier":"11111111-1111-4111-8111-111111111111","nodeType":0,"name":"Older","children":[],"links":{},"number":1,"taskState":0}}
            =======
            {"updatedAt":"2026-04-25T10:05:00Z","rootNode":{"uniqueIdentifier":"22222222-2222-4222-8222-222222222222","nodeType":0,"name":"Newer","children":[],"links":{},"number":1,"taskState":0}}
            >>>>>>> main
        """.trimIndent()

        val resolved = MapConflictResolver.tryResolve(conflicted)

        assertTrue(resolved.ok)
        assertTrue(resolved.resolvedContent.orEmpty().contains("Newer"))
    }

    @Test
    fun structurallyMergesChildrenByStableIdentifier() {
        val conflicted = """
            <<<<<<< HEAD
            {"updatedAt":"2026-04-25T10:00:00Z","rootNode":{"uniqueIdentifier":"11111111-1111-4111-8111-111111111111","nodeType":0,"name":"Root","children":[{"uniqueIdentifier":"22222222-2222-4222-8222-222222222222","nodeType":0,"name":"Ours","children":[],"links":{},"number":1,"taskState":1}],"links":{},"number":1,"taskState":0}}
            =======
            {"updatedAt":"2026-04-25T10:01:00Z","rootNode":{"uniqueIdentifier":"11111111-1111-4111-8111-111111111111","nodeType":0,"name":"Root","children":[{"uniqueIdentifier":"33333333-3333-4333-8333-333333333333","nodeType":0,"name":"Theirs","children":[],"links":{},"number":1,"taskState":2}],"links":{},"number":1,"taskState":0}}
            >>>>>>> main
        """.trimIndent()

        val resolved = MapConflictResolver.tryResolve(conflicted)

        assertTrue(resolved.ok)
        assertEquals(true, resolved.resolvedContent.orEmpty().contains("Ours"))
        assertEquals(true, resolved.resolvedContent.orEmpty().contains("Theirs"))
    }
}
