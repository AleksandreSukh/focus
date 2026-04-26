package com.systemssanity.focus.domain.model

import kotlinx.serialization.KSerializer
import kotlinx.serialization.descriptors.PrimitiveKind
import kotlinx.serialization.descriptors.PrimitiveSerialDescriptor
import kotlinx.serialization.descriptors.SerialDescriptor
import kotlinx.serialization.encoding.Decoder
import kotlinx.serialization.encoding.Encoder

@kotlinx.serialization.Serializable(with = TaskStateSerializer::class)
enum class TaskState(val wireValue: Int) {
    None(0),
    Todo(1),
    Doing(2),
    Done(3);

    val isTask: Boolean get() = this != None
    val isOpen: Boolean get() = this == Todo || this == Doing

    fun sortPriority(): Int = when (this) {
        Doing -> 0
        Todo -> 1
        Done -> 2
        None -> 3
    }

    fun displayMarker(): String = when (this) {
        None -> ""
        Todo -> "[ ]"
        Doing -> "[~]"
        Done -> "[x]"
    }

    companion object {
        fun fromWireValue(value: Int): TaskState =
            entries.firstOrNull { it.wireValue == value } ?: None
    }
}

object TaskStateSerializer : KSerializer<TaskState> {
    override val descriptor: SerialDescriptor =
        PrimitiveSerialDescriptor("TaskState", PrimitiveKind.INT)

    override fun serialize(encoder: Encoder, value: TaskState) {
        encoder.encodeInt(value.wireValue)
    }

    override fun deserialize(decoder: Decoder): TaskState =
        TaskState.fromWireValue(decoder.decodeInt())
}
