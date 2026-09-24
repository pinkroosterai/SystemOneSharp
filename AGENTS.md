# Repository Guidelines

## Project Structure & Module Organization

`SystemOneSharp.slnx` contains three .NET 10 projects. `src/SystemOneSharp/` holds the reusable HTTP client, options, DTOs, and exceptions. `tests/SystemOneSharp.Verification/` is a console verification harness with an in-memory HTTP handler; it does not need a running service. `examples/SystemOneSharp.Example/` contains the live console demo and its `appsettings.json`. The wire contract and original build plan are in `SPEC.md` and `PLAN.md`; usage instructions are in `README.md`. There are no separate asset files.

## Build, Test, and Development Commands

- `dotnet build SystemOneSharp.slnx -c Release` builds all three projects and reports compiler warnings.
- `dotnet run --project tests/SystemOneSharp.Verification -c Release` checks request JSON, endpoint selection, typed answers, retries, errors, and cancellation without an API key.
- `dotnet run --project examples/SystemOneSharp.Example -c Release` calls the endpoint configured in the example's JSON file. Start local Laya on port 8000 first, or configure hosted Jev and its key.

Run commands from the repository root in PowerShell on Windows.

## Coding Style & Naming Conventions

Use four-space indentation and follow nearby C# code. Name public types and members in `PascalCase`, local variables in `camelCase`, and private fields with a leading underscore. Keep nullable reference types enabled. Use `System.Text.Json` attributes for wire names such as `input_tokens`; do not rename JSON fields merely to match C# conventions. No formatter or linter configuration is checked in, so keep changes small and the build warning-free.

## Testing Guidelines

The project uses a runnable console harness rather than a unit-test framework; no coverage threshold is configured. Add focused `Check` cases in `tests/SystemOneSharp.Verification/Program.cs` when changing serialization, HTTP behavior, or answer handling. Use descriptive check labels and read the full command output. For example UI changes, also run the live console app and inspect its output.

## Commit & Pull Request Guidelines

Follow the existing Conventional Commit pattern, such as `feat(example): add decision timing` or `fix(client): handle response field`. Keep one logical change per commit. In a pull request, describe the behavior change, list the build and verification commands run, link any relevant issue, and include console output when changing the example display.

## Security & Configuration

Keep API keys in environment variables; `appsettings.json` stores only the variable name. Never commit a key or copy a live response containing sensitive state into a test fixture. Review `tasks/lessons.md` before changing established behavior.
