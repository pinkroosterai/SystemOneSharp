# SystemOneSharp execution plan

1. **Scaffold.** Create `SystemOneSharp.slnx`, a `net10.0` library, and a `net10.0` console verification project without external packages. Complete when both projects restore and build.
2. **Model the contract.** Add request/question and response/answer DTOs, typed JSON discriminator handling, configuration, interface, and exception types. Complete when the projects compile and a mixed question request serializes to the documented field names.
3. **Implement HTTP calls.** Add request validation, endpoint resolution, per-request authentication, response parsing and validation, cancellation, and typed failures. Complete when the harness confirms one mixed request and all three answer types.
4. **Add retry behavior.** Retry 429/529 with exponential delay or `Retry-After`; leave other failures final. Complete when the harness confirms retry success and exhausted retry status.
5. **Validate and hand off.** Run the full console harness and `dotnet build SystemOneSharp.slnx` in Release; read results, address failures, and document the use pattern in `README.md`. Complete when both commands exit successfully and the working tree contains no secrets.
