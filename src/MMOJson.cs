using Newtonsoft.Json;

namespace ValheimMMOMOD
{
    internal static class MMOJson
    {
        private static readonly JsonSerializerSettings Settings = new JsonSerializerSettings
        {
            TypeNameHandling = TypeNameHandling.None,
            MetadataPropertyHandling = MetadataPropertyHandling.Ignore,
            ObjectCreationHandling = ObjectCreationHandling.Replace,
            MaxDepth = 32
        };
        public static string ToJson(object value, bool pretty = false) { return JsonConvert.SerializeObject(value, pretty ? Formatting.Indented : Formatting.None, Settings); }
        public static T FromJson<T>(string text) { return JsonConvert.DeserializeObject<T>(text, Settings); }
    }
}
