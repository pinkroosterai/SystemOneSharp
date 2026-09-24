# Public release preparation

## Research and decisions

- GitHub's [community profile guidance](https://docs.github.com/en/communities/setting-up-your-project-for-healthy-contributions/about-community-profiles-for-public-repositories) calls out README, license, contributing, conduct, security, and issue templates. This repository now includes those files.
- Microsoft recommends [SDK-style NuGet packages with README, license metadata, tags, and repository information](https://learn.microsoft.com/en-us/nuget/create-packages/package-authoring-best-practices). The library project sets those fields and produces a `.snupkg` with [source-linked symbols](https://learn.microsoft.com/en-us/nuget/create-packages/symbol-packages-snupkg).
- Microsoft's [cross-platform library guidance](https://learn.microsoft.com/en-us/dotnet/standard/library-guidance/cross-platform-targeting) recommends considering broader targets. The first release remains .NET 10 only, matching the existing implementation and verification harness.
- GitHub recommends [minimum workflow permissions and commit-pinned actions](https://docs.github.com/en/code-security/tutorials/secure-your-organization/protect-against-threats). CI builds, runs the harness, and packs on Windows and Linux. Dependabot checks action and NuGet updates weekly.
- [Changing a private repository to public](https://docs.github.com/en/repositories/managing-your-repositorys-settings-and-features/managing-repository-settings/setting-repository-visibility) also exposes Actions history and logs. Review those as well as Git history before changing visibility.

## Release boundary

The repository is intended to remain private after preparation. `0.1.0-preview.1` is a packaging version, not a published NuGet release. The `SystemOneSharp` package ID returned HTTP 404 from NuGet's flat-container index on 2026-09-24; check it again before publishing. The MIT copyright holder is Jan Versteeg, based on the existing commit author, and should be reviewed before the public switch.

## Before making the repository public

1. Review all tracked files, Git history, Actions runs, and build artifacts for secrets and private data.
2. Confirm the README, license holder, package description, and upstream service references reflect the intended public positioning.
3. Confirm CI passes on Windows and Linux. Enable GitHub private vulnerability reporting when the repository is public, then confirm the path in `SECURITY.md` works.
4. Review repository settings, visibility, and default branch. Change visibility only after the preceding checks.

## Before publishing to NuGet

1. Confirm package ID ownership and the final version; replace the preview version if needed.
2. Check the `.nupkg` and `.snupkg` contents, metadata, README rendering, and source links.
3. Update the README's unpublished-package note and prepare release notes. Publish the package and symbols only as a separate release action.
