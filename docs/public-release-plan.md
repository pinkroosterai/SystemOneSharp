# Public release plan: GitHub and NuGet

Builds on `docs/release-readiness.md`. Checked 2026-09-24.

## Findings

- Repo `pinkroosterai/SystemOneSharp` is **private**, MIT, owned by a user account. Latest CI run on `master` is green (Windows and Linux).
- Package ID `SystemOneSharp` is free on NuGet (HTTP 404). `NUGET_API_KEY` is set in the environment. I did not test whether the key is valid or scoped to this ID.
- No secrets found in tracked files or in a search of history. No Actions artifacts exist. Run logs are not yet reviewed.
- Community files are present: README, LICENSE, CONTRIBUTING, CODE_OF_CONDUCT, SECURITY, issue and PR templates, Dependabot.
- README still says the package is unpublished (line 7). Version is `0.1.0-preview.1`.
- Every commit uses the author email `jan@ditdomeinisvanmij.nl`. It becomes public with the history.
- No git tags, no GitHub releases, no release workflow, no CHANGELOG.
- The library has no runtime dependencies, so no dependency licence issues.

## Needed from you

1. **Version:** ship `0.1.0-preview.1` (recommended: the API is new and unproven, and NuGet shows "prerelease") or `0.1.0` stable?
2. **Author email in history:** keep it public, or rewrite history to a GitHub noreply address? Rewriting means a force-push and changes every commit hash. Recommended: keep, unless that address is private to you.
3. **Upstream naming:** the README names TypeSafe Jev and Laya. Confirm you are fine using those names publicly. The package description says "used by TypeSafe Jev", which could read as an endorsement. Recommended: add one line saying this is an unofficial, community client.
4. **NuGet key:** confirm it is scoped to push new packages (glob `SystemOneSharp*`). If unsure, I will find out when the push succeeds or fails.

## Steps

### A. Prepare (one commit, no visibility change)

1. Read the Actions run logs for leaked values.
2. Add the "unofficial client" line to the README and the package description (if you agree in item 3).
3. Replace the README's "not published" install text with `dotnet add package SystemOneSharp --prerelease` (drop `--prerelease` for a stable version). Add NuGet and CI badges.
4. Add `CHANGELOG.md` with the first entry.
5. Add `.github/workflows/release.yml`, triggered by a `v*` tag: build, run the harness, pack, push `.nupkg` and `.snupkg` to NuGet, create the GitHub release with the packages attached. Recommended: use the secret `NUGET_API_KEY` and a protected `release` environment. NuGet trusted publishing is the keyless option, but it needs a first manual publish and web setup, so I would defer it.
6. Run build, the harness, and `dotnet pack` locally. Unzip the packages and check metadata, README, and the source-link/symbols.

### B. Go public on GitHub

7. Push step A. Confirm CI is green.
8. Set repo topics and description. Turn on private vulnerability reporting, secret scanning with push protection, and Dependabot alerts. Protect `master` (require CI, block force-push).
9. Change visibility to public. **This is outward-facing and hard to undo (forks and caches persist), so I will confirm with you right before this step.**

### C. Release

10. Add the repository secret `NUGET_API_KEY` from your environment value (via `gh secret set`, the value is never printed).
11. Tag `v<version>` and push it. The workflow publishes to NuGet and creates the GitHub release.
12. Verify: package page on nuget.org (README renders, symbols, source link), `dotnet add package` in a scratch project, then a run of the example against it.
13. If a push to NuGet goes wrong, NuGet cannot delete a version, only unlist it. That is why step 6 checks the packages first.

## Next step

Answer the four questions above. Then I do step A and stop before step 9 for your go-ahead.
