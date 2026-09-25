namespace SystemOneSharp.Testing;

/// <summary>Canonical answers shaped like the ones a System One server returns.</summary>
public static class SystemOneAnswers
{
    public static NoulAnswer Noul(double probability) => new() { Noul = probability };

    /// <summary>A Choice answer selecting the most probable label.</summary>
    public static ChoiceAnswer Choice(IReadOnlyDictionary<string, double> probabilities, double confidence = 0.8) => new()
    {
        Choice = probabilities.MaxBy(pair => pair.Value).Key,
        Probabilities = probabilities,
        Confidence = confidence
    };

    /// <summary>A Score answer with a numbered legend and a probability for each level.</summary>
    public static ScoreAnswer Score(double score, IReadOnlyList<string> levels, IReadOnlyList<double> probabilities, double confidence = 0.7) => new()
    {
        Score = score,
        Legend = levels.Select((level, index) => (level, index)).ToDictionary(item => item.index.ToString(), item => item.level),
        Probabilities = probabilities.Select((probability, index) => (probability, index)).ToDictionary(item => item.index.ToString(), item => item.probability),
        Confidence = confidence
    };
}

/// <summary>Deterministic response factories for <see cref="FakeSystemOneClient"/>.</summary>
public static class SystemOneResponses
{
    public const string Model = "jev-test";

    /// <summary>A response with the given answers and fixed usage.</summary>
    public static SystemOneResponse Create(IReadOnlyDictionary<string, SystemOneAnswer> answers) => new()
    {
        Model = Model,
        Answers = answers,
        Usage = new TokenUsage { InputTokens = 10, OutputTokens = 2 }
    };

    /// <summary>A factory answering every Noul question with <paramref name="probability"/>.</summary>
    public static Func<SystemOneRequest, SystemOneResponse> AllNoul(double probability) => request =>
        Create(request.Questions.ToDictionary(pair => pair.Key, _ => (SystemOneAnswer)SystemOneAnswers.Noul(probability)));

    /// <summary>A factory answering each question ID from <paramref name="answers"/>; missing IDs throw.</summary>
    public static Func<SystemOneRequest, SystemOneResponse> ById(IReadOnlyDictionary<string, SystemOneAnswer> answers) => request =>
        Create(request.Questions.Keys.ToDictionary(id => id, id => answers.TryGetValue(id, out var answer)
            ? answer
            : throw new KeyNotFoundException($"No canned answer for question '{id}'.")));
}
