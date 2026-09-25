using System.Text.Json;
using System.Text.Json.Nodes;
using System.Text.Json.Serialization.Metadata;

namespace SystemOneSharp;

/// <summary>Builds a validated decision request with Choice, Score, and Noul questions.</summary>
public sealed class SystemOneRequestBuilder
{
    private JsonNode? _state;
    private string? _model;
    private readonly Dictionary<string, SystemOneQuestion> _questions = new();

    /// <summary>Sets a plain-text state.</summary>
    public SystemOneRequestBuilder WithState(string state)
    {
        ArgumentNullException.ThrowIfNull(state);
        _state = JsonValue.Create(state);
        return this;
    }

    /// <summary>Sets a structured JSON state using a copy of the supplied node.</summary>
    public SystemOneRequestBuilder WithState(JsonNode state)
    {
        ArgumentNullException.ThrowIfNull(state);
        _state = state.DeepClone();
        return this;
    }

    /// <summary>Sets a structured JSON state using a copy of the supplied element.</summary>
    public SystemOneRequestBuilder WithState(JsonElement state)
    {
        if (state.ValueKind == JsonValueKind.Undefined)
            throw new ArgumentException("State element has no value.", nameof(state));
        _state = JsonNode.Parse(state.GetRawText());
        return this;
    }

    /// <summary>Serializes an object as the state.</summary>
    public SystemOneRequestBuilder WithState<T>(T state)
    {
        ArgumentNullException.ThrowIfNull(state);
        _state = JsonSerializer.SerializeToNode(state);
        return this;
    }

    /// <summary>Serializes an object as the state using source-generated metadata, without reflection.</summary>
    public SystemOneRequestBuilder WithState<T>(T state, JsonTypeInfo<T> typeInfo)
    {
        ArgumentNullException.ThrowIfNull(state);
        ArgumentNullException.ThrowIfNull(typeInfo);
        _state = JsonSerializer.SerializeToNode(state, typeInfo);
        return this;
    }

    /// <summary>Overrides <see cref="SystemOneOptions.Model"/> for this request.</summary>
    public SystemOneRequestBuilder WithModel(string model)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(model);
        _model = model;
        return this;
    }

    /// <summary>Adds a Choice question with plain-text instructions.</summary>
    public SystemOneRequestBuilder AddChoice(string id, string instructions, Action<ChoiceQuestionBuilder> configure)
    {
        ArgumentNullException.ThrowIfNull(instructions);
        return AddChoice(id, JsonValue.Create(instructions)!, configure);
    }

    /// <summary>Adds a Choice question with structured instructions.</summary>
    public SystemOneRequestBuilder AddChoice(string id, JsonNode instructions, Action<ChoiceQuestionBuilder> configure)
    {
        ArgumentNullException.ThrowIfNull(instructions);
        ArgumentNullException.ThrowIfNull(configure);
        var builder = new ChoiceQuestionBuilder();
        configure(builder);
        return AddQuestion(id, new ChoiceQuestion
        {
            Instructions = instructions.DeepClone(),
            Criteria = builder.Build()
        });
    }

    /// <summary>Adds a Score question with plain-text instructions.</summary>
    public SystemOneRequestBuilder AddScore(string id, string instructions, Action<ScoreQuestionBuilder> configure)
    {
        ArgumentNullException.ThrowIfNull(instructions);
        return AddScore(id, JsonValue.Create(instructions)!, configure);
    }

    /// <summary>Adds a Score question with structured instructions.</summary>
    public SystemOneRequestBuilder AddScore(string id, JsonNode instructions, Action<ScoreQuestionBuilder> configure)
    {
        ArgumentNullException.ThrowIfNull(instructions);
        ArgumentNullException.ThrowIfNull(configure);
        var builder = new ScoreQuestionBuilder();
        configure(builder);
        return AddQuestion(id, new ScoreQuestion
        {
            Instructions = instructions.DeepClone(),
            Criteria = builder.Build()
        });
    }

    /// <summary>Adds a Noul question with plain-text instructions and optional criteria.</summary>
    public SystemOneRequestBuilder AddNoul(string id, string instructions, Action<NoulQuestionBuilder> configure)
    {
        ArgumentNullException.ThrowIfNull(instructions);
        return AddNoul(id, JsonValue.Create(instructions)!, configure);
    }

    /// <summary>Adds a Noul question without criteria.</summary>
    public SystemOneRequestBuilder AddNoul(string id, string instructions)
        => AddNoul(id, instructions, _ => { });

    /// <summary>Adds a Noul question with structured instructions and no criteria.</summary>
    public SystemOneRequestBuilder AddNoul(string id, JsonNode instructions)
        => AddNoul(id, instructions, _ => { });

    /// <summary>Adds a Noul question with structured instructions and optional criteria.</summary>
    public SystemOneRequestBuilder AddNoul(string id, JsonNode instructions, Action<NoulQuestionBuilder> configure)
    {
        ArgumentNullException.ThrowIfNull(instructions);
        ArgumentNullException.ThrowIfNull(configure);
        var builder = new NoulQuestionBuilder();
        configure(builder);
        return AddQuestion(id, new NoulQuestion
        {
            Instructions = instructions.DeepClone(),
            Criteria = builder.Build()
        });
    }

    /// <summary>Validates and returns a separate snapshot of the request.</summary>
    public SystemOneRequest Build()
    {
        var request = new SystemOneRequest
        {
            State = _state?.DeepClone()!,
            Questions = _questions.ToDictionary(pair => pair.Key, pair => CloneQuestion(pair.Value)),
            Model = _model
        };
        SystemOneRequestValidator.Validate(request);
        return request;
    }

    private SystemOneRequestBuilder AddQuestion(string id, SystemOneQuestion question)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(id);
        if (!_questions.TryAdd(id, question))
            throw new ArgumentException($"Question ID '{id}' is already present.", nameof(id));
        return this;
    }

    private static SystemOneQuestion CloneQuestion(SystemOneQuestion question) => question switch
    {
        ChoiceQuestion choice => new ChoiceQuestion
        {
            Instructions = choice.Instructions.DeepClone(),
            Criteria = choice.Criteria.ToDictionary(pair => pair.Key, pair => pair.Value?.DeepClone())
        },
        ScoreQuestion score => new ScoreQuestion
        {
            Instructions = score.Instructions.DeepClone(),
            Criteria = score.Criteria.Select(item => item.DeepClone()).ToArray()
        },
        NoulQuestion noul => new NoulQuestion
        {
            Instructions = noul.Instructions.DeepClone(),
            Criteria = noul.Criteria?.ToDictionary(pair => pair.Key, pair => pair.Value.DeepClone())
        },
        _ => throw new InvalidOperationException("Unsupported question type.")
    };
}

public sealed class ChoiceQuestionBuilder
{
    private readonly Dictionary<string, JsonNode?> _criteria = new();

    public ChoiceQuestionBuilder Option(string label, string? description)
        => OptionJson(label, description is null ? null : JsonValue.Create(description));

    public ChoiceQuestionBuilder OptionJson(string label, JsonNode? description)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(label);
        if (!_criteria.TryAdd(label, description?.DeepClone()))
            throw new ArgumentException($"Choice label '{label}' is already present.", nameof(label));
        return this;
    }

    internal IReadOnlyDictionary<string, JsonNode?> Build() => _criteria;
}

public sealed class ScoreQuestionBuilder
{
    private readonly List<JsonNode> _criteria = new();

    public ScoreQuestionBuilder Level(string description)
    {
        ArgumentNullException.ThrowIfNull(description);
        return LevelJson(JsonValue.Create(description)!);
    }

    public ScoreQuestionBuilder LevelJson(JsonNode description)
    {
        ArgumentNullException.ThrowIfNull(description);
        _criteria.Add(description.DeepClone());
        return this;
    }

    internal IReadOnlyList<JsonNode> Build() => _criteria;
}

public sealed class NoulQuestionBuilder
{
    private IReadOnlyDictionary<string, JsonNode>? _criteria;

    public NoulQuestionBuilder Criteria(string trueDescription, string falseDescription)
    {
        ArgumentNullException.ThrowIfNull(trueDescription);
        ArgumentNullException.ThrowIfNull(falseDescription);
        return CriteriaJson(JsonValue.Create(trueDescription)!, JsonValue.Create(falseDescription)!);
    }

    public NoulQuestionBuilder CriteriaJson(JsonNode trueDescription, JsonNode falseDescription)
    {
        ArgumentNullException.ThrowIfNull(trueDescription);
        ArgumentNullException.ThrowIfNull(falseDescription);
        _criteria = new Dictionary<string, JsonNode>
        {
            ["true"] = trueDescription.DeepClone(),
            ["false"] = falseDescription.DeepClone()
        };
        return this;
    }

    internal IReadOnlyDictionary<string, JsonNode>? Build() => _criteria;
}
