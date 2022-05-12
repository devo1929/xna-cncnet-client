using Newtonsoft.Json;

namespace DTAClient.Domain.Multiplayer.CnCNet.QuickMatch
{
    public class QmRequestResponse
    {
        [JsonProperty("type")]
        public string Type { get; set; }

        [JsonProperty("description")]
        public string Description { get; set; }

        [JsonProperty("message")]
        public string Message { get; set; }

        [JsonProperty("checkback")]
        public int CheckBack { get; set; }

        [JsonProperty("no_sooner_than")]
        public int NoSoonerThan { get; set; }

        public bool IsError => IsType(QmResponseTypes.Error);

        public bool IsFatal => IsType(QmResponseTypes.Fatal);

        public bool IsSpawn => IsType(QmResponseTypes.Spawn);

        public bool IsUpdate => IsType(QmResponseTypes.Update);

        public bool IsQuit => IsType(QmResponseTypes.Quit);

        public bool IsWait => IsType(QmResponseTypes.Wait);

        public bool IsSuccessful => !IsError && !IsFatal;

        private bool IsType(string type) => Type == type;
    }
}
