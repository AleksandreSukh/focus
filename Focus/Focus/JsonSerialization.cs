#nullable enable

using Newtonsoft.Json;
using Newtonsoft.Json.Serialization;

namespace Systems.Sanity.Focus;

internal static class JsonSerialization
{
    public static JsonSerializerSettings CreateDefaultSettings() =>
        new()
        {
            Formatting = Formatting.Indented,
            ContractResolver = new CamelCasePropertyNamesContractResolver()
        };
}
