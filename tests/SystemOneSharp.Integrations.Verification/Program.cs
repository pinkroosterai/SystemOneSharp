using SystemOneSharp.Integrations.Verification;

await ExtensionsAIChecks.RunAsync();
await EvaluationChecks.RunAsync();

Console.WriteLine("All SystemOneSharp integration checks passed.");
