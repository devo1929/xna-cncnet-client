using System.Collections.Generic;
using Newtonsoft.Json;

namespace DTAClient.Domain.Multiplayer
{
    public class TeamStartMappingPreset
    {
        [JsonProperty("n")]
        public string Name { get; set; }

        [JsonProperty("m")]
        public List<TeamStartMapping> TeamStartMappings { get; set; }

        [JsonIgnore]
        public bool IsUserDefined { get; set; }

        public TeamStartMappingPreset()
        {
            TeamStartMappings = new List<TeamStartMapping>();
        }
    }
}
