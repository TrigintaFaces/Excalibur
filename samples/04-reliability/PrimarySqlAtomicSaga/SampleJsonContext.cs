using System.Text.Json.Serialization;

namespace PrimarySqlAtomicSaga;

[JsonSourceGenerationOptions(PropertyNamingPolicy = JsonKnownNamingPolicy.CamelCase,
    UseStringEnumConverter = true, DefaultIgnoreCondition = JsonIgnoreCondition.WhenWritingNull)]
[JsonSerializable(typeof(OrderState))]
[JsonSerializable(typeof(OrderCommand))]
[JsonSerializable(typeof(OrderDeadline))]
[JsonSerializable(typeof(Dictionary<string, object>))]
[JsonSerializable(typeof(string))]
internal partial class SampleJsonContext : JsonSerializerContext;
