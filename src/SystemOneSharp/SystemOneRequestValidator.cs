using System.Text.Json;
using System.Text.Json.Nodes;

namespace SystemOneSharp;

internal static class SystemOneRequestValidator
{
    internal static void Validate(SystemOneRequest request)
    {
        ArgumentNullException.ThrowIfNull(request);
        if (request.State is null || !IsStructuredOrString(request.State))
            throw new ArgumentException("State must be a JSON string, object, or array.", nameof(request));
        if (request.Questions is null || request.Questions.Count == 0)
            throw new ArgumentException("At least one question is required.", nameof(request));

        foreach (var (id, question) in request.Questions)
        {
            if (string.IsNullOrWhiteSpace(id) || question is null ||
                question.Instructions is null || !IsStructuredOrString(question.Instructions))
                throw new ArgumentException($"Question '{id}' needs a valid ID and instructions.", nameof(request));

            switch (question)
            {
                case ChoiceQuestion choice when choice.Criteria is { Count: >= 2 and <= 255 } &&
                    choice.Criteria.All(pair => !string.IsNullOrWhiteSpace(pair.Key) &&
                        (pair.Value is null || IsStructuredOrString(pair.Value))):
                    break;
                case ScoreQuestion score when score.Criteria is { Count: >= 2 and <= 10 } &&
                    score.Criteria.All(item => item is not null && IsStructuredOrString(item)):
                    break;
                case NoulQuestion noul when noul.Criteria is null ||
                    noul.Criteria.Count == 2 && noul.Criteria.ContainsKey("true") &&
                    noul.Criteria.ContainsKey("false") && noul.Criteria.Values.All(IsStructuredOrString):
                    break;
                default:
                    throw new ArgumentException($"Question '{id}' has invalid criteria.", nameof(request));
            }
        }
    }

    private static bool IsStructuredOrString(JsonNode node) =>
        node.GetValueKind() is JsonValueKind.String or JsonValueKind.Object or JsonValueKind.Array;
}
