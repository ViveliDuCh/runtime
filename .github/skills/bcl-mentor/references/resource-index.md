# Resource Index — dotnet/runtime Documentation

Master index of all documentation, processes, and tools relevant to BCL development.

> **MCP Preference:** When the user needs the *content* of a document below (not
> just the link), prefer querying the appropriate MCP server over summarizing from
> memory. GitHub-hosted docs → `github:get_file_contents`. Microsoft Learn docs →
> `microsoftdocs:search`. CI pipelines → `azure-devops:*`. Packages → `nuget:*`.
> Build logs → `baronfel-binlog:*`. Only fall back to static links for external
> tools with no MCP coverage (Helix, apisof.net, grep.app).

## Coding Guidelines

| Document | URL | When to Use |
|---|---|---|
| Framework Design Guidelines Digest | https://github.com/dotnet/runtime/blob/main/docs/coding-guidelines/framework-design-guidelines-digest.md | API naming and design |
| Breaking Changes | https://github.com/dotnet/runtime/blob/main/docs/coding-guidelines/breaking-changes.md | Evaluating behavior changes |
| Breaking Change Rules | https://github.com/dotnet/runtime/blob/main/docs/coding-guidelines/breaking-change-rules.md | Source/binary compat rules |
| Breaking Change Definitions | https://github.com/dotnet/runtime/blob/main/docs/coding-guidelines/breaking-change-definitions.md | What counts as breaking |
| Breaking Change Process | https://github.com/dotnet/runtime/blob/main/docs/project/breaking-change-process.md | Filing a breaking change |
| Updating Ref Source | https://github.com/dotnet/runtime/blob/main/docs/coding-guidelines/updating-ref-source.md | After modifying public API |
| EditorConfig | https://github.com/dotnet/runtime/blob/main/.editorconfig | Code style enforcement |
| Copilot Instructions | https://github.com/dotnet/runtime/blob/main/.github/copilot-instructions.md | Build/test workflow |

## Project Processes

| Document | URL | When to Use |
|---|---|---|
| API Review Process | https://github.com/dotnet/runtime/blob/main/docs/project/api-review-process.md | Before filing API proposal |
| Issue Guide | https://github.com/dotnet/runtime/blob/main/docs/project/issue-guide.md | Finding area owners, labels |
| Library Servicing | https://github.com/dotnet/runtime/blob/main/docs/project/library-servicing.md | Backporting fixes |
| Branching Guide | https://github.com/dotnet/runtime/blob/main/docs/project/branching-guide.md | Release branch management |
| Versioning | https://github.com/dotnet/runtime/blob/main/docs/project/versioning.md | Version number conventions |
| Writing Tests | https://github.com/dotnet/runtime/blob/main/docs/project/writing-tests.md | Test conventions |
| Contributing | https://github.com/dotnet/runtime/blob/main/CONTRIBUTING.md | General contribution guide |
| Repo Organization | https://github.com/dotnet/runtime/blob/main/docs/project/repo-organization.md | Understanding repo structure |

## Build & Test Workflow

| Document | URL | When to Use |
|---|---|---|
| Build Libraries | https://github.com/dotnet/runtime/blob/main/docs/workflow/building/libraries/README.md | Building library code |
| Test Libraries | https://github.com/dotnet/runtime/blob/main/docs/workflow/testing/libraries/testing.md | Running library tests |
| Build CoreCLR | https://github.com/dotnet/runtime/blob/main/docs/workflow/building/coreclr/README.md | Building CLR |
| Test CoreCLR | https://github.com/dotnet/runtime/blob/main/docs/workflow/testing/coreclr/testing.md | Running CLR tests |
| Build Mono | https://github.com/dotnet/runtime/blob/main/docs/workflow/building/mono/README.md | Building Mono |
| WASM Build | https://github.com/dotnet/runtime/blob/main/docs/workflow/building/libraries/webassembly-instructions.md | WASM targets |
| CI Failure Analysis | https://github.com/dotnet/runtime/blob/main/docs/workflow/ci/failure-analysis.md | When CI is red |
| Editing & Debugging | https://github.com/dotnet/runtime/blob/main/docs/workflow/editing-and-debugging.md | IDE setup |

## CI & Infrastructure

| Resource | URL | When to Use |
|---|---|---|
| Known Issues Documentation | https://github.com/dotnet/arcade/blob/main/Documentation/Projects/Build%20Analysis/KnownIssues.md | Filing CI issues |
| Known Issues Board | https://github.com/orgs/dotnet/projects/111 | Checking for existing issues |
| Build Analysis Helper | https://helix.dot.net/BuildAnalysis/CreateKnownIssues | Creating known issue JSON |
| AzDO Runtime Pipeline | https://dev.azure.com/dnceng-public/public/_build?definitionId=130 | Checking CI history |
| Helix Portal | https://helix.dot.net/ | Investigating test results |
| dnceng Issues | https://github.com/dotnet/dnceng/issues | Infrastructure problems |

## API Review

| Resource | URL | When to Use |
|---|---|---|
| API Review Schedule | https://apireview.net/schedule | Checking when reviews happen |
| Ready for Review Backlog | https://aka.ms/ready-for-api-review | Tracking proposal status |
| API Review Notes | https://github.com/dotnet/apireviews | Past review decisions |
| API Proposal Template | https://github.com/dotnet/runtime/issues/new?template=02_api_proposal.yml | Filing new proposals |
| FXDC Live Streams | https://www.youtube.com/@NETFoundation/streams | Watching reviews |

## Tools

| Tool | URL / Command | Purpose |
|---|---|---|
| ILSpy | https://github.com/icsharpcode/ILSpy | Decompile .NET assemblies |
| apisof.net | https://apisof.net | Check API availability across TFMs |
| grep.app | https://grep.app | Search public .NET repos for usage patterns |
| Regex101 | https://regex101.com/ (.NET flavor) | Test Known Issue regex patterns |
| JSON Escape | https://www.freeformatter.com/json-escape.html | Escape JSON for Known Issues |
| DevBox | https://devbox.microsoft.com/ | Cloud dev VM |
| BenchmarkDotNet | Installed via NuGet | Performance benchmarking |

## People & Teams

| Team | Handle | When to Engage |
|---|---|---|
| Compat Team | `@dotnet/compat` | Breaking change evaluation |
| FXDC | Framework Design Core | API review meeting |
| dnceng | `@dotnet/dnceng` | CI infrastructure issues |
| Area Owners | See [issue-guide.md](https://github.com/dotnet/runtime/blob/main/docs/project/issue-guide.md) | Area-specific questions |

## Communication Channels

| Channel | URL | Purpose |
|---|---|---|
| Infrastructure Team (Teams) | [Teams link](https://teams.microsoft.com/l/channel/19%3ab27b36ecd10a46398da76b02f0411de7%40thread.skype/Infrastructure) | CI help (corpnet) |
| Discord #runtime | https://aka.ms/dotnet-discord | Community + team chat |

## PR Commands

| Command | Purpose |
|---|---|
| `/azp run runtime` | Re-run all CI pipelines |
| `/azp run runtime-extra-platforms` | Run extra platform tests |
| `/ba-g opened issue #XXXXX` | Bypass Build Analysis for known issue |
| `@dotnet-policy-service rerun` | Re-run CLA/license check |
