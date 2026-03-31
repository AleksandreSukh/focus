#nullable enable

using Newtonsoft.Json;
using Newtonsoft.Json.Converters;
using Newtonsoft.Json.Serialization;
using Systems.Sanity.Focus.Domain;

namespace Systems.Sanity.Focus;

internal static class JsonSerialization
{
    public static JsonSerializerSettings CreateDefaultSettings() =>
        new()
        {
            Formatting = Formatting.Indented,
            ContractResolver = new CamelCasePropertyNamesContractResolver(),
            Converters =
            {
                new IsoDateTimeConverter { DateTimeFormat = NodeMetadata.TimestampFormat },
            },
        };
}
