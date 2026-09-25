using System.Text.Json.Serialization;

namespace SystemOneSharp.Extensions.AI.Evaluation;

[JsonSerializable(typeof(IReadOnlyDictionary<string, double>))]
internal sealed partial class SystemOneEvaluationJsonContext : JsonSerializerContext;
