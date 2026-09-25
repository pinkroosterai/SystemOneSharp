using SystemOneSharp.Integrations.Verification;

await ExtensionsAIChecks.RunAsync();
await EvaluationChecks.RunAsync();
await AgentFrameworkChecks.RunAsync();

Console.WriteLine("All SystemOneSharp integration checks passed.");
