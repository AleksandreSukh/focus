using Newtonsoft.Json;
using Systems.Sanity.Focus.Infrastructure.Input;

namespace Systems.Sanity.Focus.Domain
{
    public class UserConfig
    {
        public string DataFolder { get; set; }

        public string GitRepository { get; set; }

        [JsonProperty(NullValueHandling = NullValueHandling.Ignore)]
        public TranslationDto[] Translations { get; set; }
    }
}
