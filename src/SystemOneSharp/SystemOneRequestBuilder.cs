using System.Text.Json;
using System.Text.Json.Nodes;

namespace SystemOneSharp;

public sealed class SystemOneRequestBuilder
{
    private JsonNode? _state;
    private readonly Dictionary<string, SystemOneQuestion> _questions = new();

    public SystemOneRequestBuilder WithState(string state)
    {
        ArgumentNullException.ThrowIfNull(state);
        _state = JsonValue.Create(state);
        return this;
    }

    public SystemOneRequestBuilder WithState(JsonNode state)
    {
        ArgumentNullException.ThrowIfNull(state);
        _state = state.DeepClone();
        return this;
    }

    public SystemOneRequestBuilder WithState<T>(T state)
    {
        ArgumentNullException.ThrowIfNull(state);
        _state = JsonSerializer.SerializeToNode(state);
        return this;
    }

    public SystemOneRequestBuilder AddChoice(string id, string instructions, Action<ChoiceQuestionBuilder> configure)
    {
        ArgumentNullException.ThrowIfNull(instructions);
        return AddChoice(id, JsonValue.Create(instructions)!, configure);
    }

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

    public SystemOneRequestBuilder AddScore(string id, string instructions, Action<ScoreQuestionBuilder> configure)
    {
        ArgumentNullException.ThrowIfNull(instructions);
        return AddScore(id, JsonValue.Create(instructions)!, configure);
    }

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

    public SystemOneRequestBuilder AddNoul(string id, string instructions, Action<NoulQuestionBuilder> configure)
    {
        ArgumentNullException.ThrowIfNull(instructions);
        return AddNoul(id, JsonValue.Create(instructions)!, configure);
    }

    public SystemOneRequestBuilder AddNoul(string id, string instructions)
        => AddNoul(id, instructions, _ => { });

    public SystemOneRequestBuilder AddNoul(string id, JsonNode instructions)
        => AddNoul(id, instructions, _ => { });

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

    public SystemOneRequest Build()
    {
        var request = new SystemOneRequest
        {
            State = _state?.DeepClone()!,
            Questions = _questions.ToDictionary(pair => pair.Key, pair => CloneQuestion(pair.Value))
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
