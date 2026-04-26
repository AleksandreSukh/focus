package com.systemssanity.focus.domain.model

import kotlinx.serialization.KSerializer
import kotlinx.serialization.descriptors.PrimitiveKind
import kotlinx.serialization.descriptors.PrimitiveSerialDescriptor
import kotlinx.serialization.descriptors.SerialDescriptor
import kotlinx.serialization.encoding.Decoder
import kotlinx.serialization.encoding.Encoder

@kotlinx.serialization.Serializable(with = NodeTypeSerializer::class)
enum class NodeType(val wireValue: Int) {
    TextItem(0),
    IdeaBagItem(1),
    TextBlockItem(2);

    val isEditableInMobile: Boolean get() = this != IdeaBagItem

    companion object {
        fun fromWireValue(value: Int): NodeType =
            entries.firstOrNull { it.wireValue == value } ?: TextItem
    }
}

object NodeTypeSerializer : KSerializer<NodeType> {
    override val descriptor: SerialDescriptor =
        PrimitiveSerialDescriptor("NodeType", PrimitiveKind.INT)

    override fun serialize(encoder: Encoder, value: NodeType) {
        encoder.encodeInt(value.wireValue)
    }

    override fun deserialize(decoder: Decoder): NodeType =
        NodeType.fromWireValue(decoder.decodeInt())
}
