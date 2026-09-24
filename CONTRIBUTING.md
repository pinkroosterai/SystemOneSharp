# Contributing

Bug reports and focused pull requests are welcome. Search existing issues before opening a new one. For a behavior change, describe the expected System One request or response and how it differs from the current behavior. Do not include API keys, private state, or live responses in issues or fixtures.

The client is a small .NET 10 library. Keep changes close to the existing code and preserve the documented wire contract in `SPEC.md`. Add checks to the console verification harness when changing serialization, HTTP behavior, or typed answers. Run these commands from the repository root before a pull request:

```text
dotnet build SystemOneSharp.slnx -c Release
dotnet run --project tests/SystemOneSharp.Verification -c Release
```

Use a Conventional Commit subject such as `fix(client): handle response field`. In a pull request, explain the behavior change, list the checks run, and link a related issue when there is one. Security issues belong in the private reporting channel described in [SECURITY.md](SECURITY.md).
