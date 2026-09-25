using System.Text.Json;
using System.Text.Json.Nodes;
using System.Text.Json.Serialization;

namespace SystemOneSharp;

public sealed class SystemOneRequest
{
    public JsonNode State { get; init; } = null!;
    public IReadOnlyDictionary<string, SystemOneQuestion> Questions { get; init; } = null!;
    /// <summary>Gets the model ID for this request, or <see langword="null"/> to use <see cref="SystemOneOptions.Model"/>.</summary>
    public string? Model { get; init; }
}

[JsonPolymorphic(TypeDiscriminatorPropertyName = "type")]
[JsonDerivedType(typeof(ChoiceQuestion), "choice")]
[JsonDerivedType(typeof(ScoreQuestion), "score")]
[JsonDerivedType(typeof(NoulQuestion), "noul")]
public abstract class SystemOneQuestion
{
    [JsonPropertyName("instructions")]
    public JsonNode Instructions { get; init; } = null!;
}

public sealed class ChoiceQuestion : SystemOneQuestion
{
    [JsonPropertyName("criteria")]
    public IReadOnlyDictionary<string, JsonNode?> Criteria { get; init; } = null!;

    public ChoiceQuestion() { }

    public ChoiceQuestion(string instructions, IReadOnlyDictionary<string, string?> criteria)
    {
        Instructions = JsonValue.Create(instructions)!;
        Criteria = criteria.ToDictionary(pair => pair.Key, pair => pair.Value is null ? null : (JsonNode)JsonValue.Create(pair.Value)!);
    }
}

public sealed class ScoreQuestion : SystemOneQuestion
{
    [JsonPropertyName("criteria")]
    public IReadOnlyList<JsonNode> Criteria { get; init; } = null!;

    public ScoreQuestion() { }

    public ScoreQuestion(string instructions, IReadOnlyList<string> criteria)
    {
        Instructions = JsonValue.Create(instructions)!;
        Criteria = criteria.Select(value => (JsonNode)JsonValue.Create(value)!).ToArray();
    }
}

public sealed class NoulQuestion : SystemOneQuestion
{
    [JsonPropertyName("criteria")]
    [JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)]
    public IReadOnlyDictionary<string, JsonNode>? Criteria { get; init; }

    public NoulQuestion() { }

    public NoulQuestion(string instructions) => Instructions = JsonValue.Create(instructions)!;
}

public sealed class SystemOneResponse
{
    [JsonPropertyName("model")]
    public string Model { get; init; } = null!;

    [JsonPropertyName("answers")]
    public IReadOnlyDictionary<string, SystemOneAnswer> Answers { get; init; } = null!;

    [JsonPropertyName("usage")]
    public TokenUsage Usage { get; init; } = null!;

    /// <summary>Gets response fields outside the System One contract, such as Laya's <c>routing</c>. They are preserved but not validated.</summary>
    [JsonExtensionData]
    public Dictionary<string, JsonElement>? AdditionalProperties { get; init; }

    public ChoiceAnswer GetChoice(string id) => GetAnswer<ChoiceAnswer>(id);
    public ScoreAnswer GetScore(string id) => GetAnswer<ScoreAnswer>(id);
    public NoulAnswer GetNoul(string id) => GetAnswer<NoulAnswer>(id);

    private T GetAnswer<T>(string id) where T : SystemOneAnswer
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(id);
        if (!Answers.TryGetValue(id, out var answer))
            throw new KeyNotFoundException($"Answer '{id}' was not returned.");
        return answer as T ?? throw new InvalidOperationException($"Answer '{id}' is not a {typeof(T).Name}.");
    }
}

public sealed class TokenUsage
{
    [JsonPropertyName("input_tokens")]
    public int InputTokens { get; init; }

    [JsonPropertyName("output_tokens")]
    public int OutputTokens { get; init; }
}

[JsonPolymorphic(TypeDiscriminatorPropertyName = "type")]
[JsonDerivedType(typeof(ChoiceAnswer), "choice")]
[JsonDerivedType(typeof(ScoreAnswer), "score")]
[JsonDerivedType(typeof(NoulAnswer), "noul")]
public abstract class SystemOneAnswer { }

public sealed class ChoiceAnswer : SystemOneAnswer
{
    [JsonPropertyName("choice")]
    public string Choice { get; init; } = null!;

    [JsonPropertyName("probabilities")]
    public IReadOnlyDictionary<string, double> Probabilities { get; init; } = null!;

    [JsonPropertyName("confidence")]
    public double Confidence { get; init; }
}

public sealed class ScoreAnswer : SystemOneAnswer
{
    [JsonPropertyName("score")]
    public double Score { get; init; }

    [JsonPropertyName("legend")]
    public IReadOnlyDictionary<string, string> Legend { get; init; } = null!;

    [JsonPropertyName("probabilities")]
    public IReadOnlyDictionary<string, double> Probabilities { get; init; } = null!;

    [JsonPropertyName("confidence")]
    public double Confidence { get; init; }
}

public sealed class NoulAnswer : SystemOneAnswer
{
    [JsonPropertyName("noul")]
    public double Noul { get; init; }
}
