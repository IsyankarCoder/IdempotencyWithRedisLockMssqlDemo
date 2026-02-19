using System.Text.Json;
using System.Xml.Linq;

namespace IdempotencyWithRedisLockMssqlDemo.Helper
{
    public static class JsonHelper
    {
        public static string NormalizeJson<T>(T obj)
        {
            var options = new JsonSerializerOptions()
            {
                WriteIndented = false,
                DefaultIgnoreCondition = System.Text.Json.Serialization.JsonIgnoreCondition.WhenWritingNull
            };

            var json = JsonSerializer.Serialize(obj, options);
            using var doc =JsonDocument.Parse(json);
            return NormalizeElement(doc.RootElement);

        }

        public static string NormalizeElement(JsonElement jsonElement)
        {
            return jsonElement.ValueKind switch
            {
                JsonValueKind.Object => "{" + string.Join(",",
                    jsonElement.EnumerateObject()
                           .OrderBy(p => p.Name)
                           .Select(p => $"\"{p.Name}\":{NormalizeElement(p.Value)}")) + "}",

                JsonValueKind.Array => "[" + string.Join(",",
                    jsonElement.EnumerateArray().Select(NormalizeElement)) + "]",

                JsonValueKind.String => $"\"{jsonElement.GetString()}\"",
                JsonValueKind.Number => jsonElement.GetRawText(),
                JsonValueKind.True => "true",
                JsonValueKind.False => "false",
                JsonValueKind.Null => "null",
                _ => throw new NotSupportedException()
            };
        }
             
    }
}
