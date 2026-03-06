---
name: navigating-bcl-protocols
description: >
  Routes dotnet/runtime contributors to the correct documented workflow based on
  work type: API design, bug fixes, CI failures, breaking changes, community PR
  review, or adding new BCL types. Provides step-by-step procedures with links
  to upstream documentation. Use when unsure what process to follow, onboarding
  to a new BCL area, evaluating a community contribution, or triaging CI failures.
  Do not use for code implementation, performance benchmarking, or build analysis.
---

# BCL Protocol Navigator

Routes a dotnet/runtime contributor to the correct documented process based on
the type of work. Identifies the work type, loads the appropriate protocol
reference, and provides step-by-step guidance with links.

## When to Use This Skill

- "What's the process for adding a new API?"
- "I got a community PR to review — what should I check?"
- "CI is failing and I don't know if it's my fault"
- "Is this change a breaking change?"
- "How do I add a new type to System.Numerics.Vectors?"
- "What should I do next on my PR?"
- "Where's the documentation for X?"

## Step 1: Identify the Work Type

Ask the user (or infer from context) which scenario applies:

| Work Type | Trigger Phrases | Protocol Reference |
|---|---|---|
| **New API Design** | "API proposal", "new API", "design review" | [new-api-workflow.md](references/new-api-workflow.md) |
| **CI Failure Triage** | "CI red", "build failed", "test failures" | [ci-failure-triage.md](references/ci-failure-triage.md) |
| **Breaking Change** | "breaking change", "compat", "behavioral change" | [breaking-change-eval.md](references/breaking-change-eval.md) |
| **Community PR** | "PR review", "community contribution", "external PR" | [community-pr-review.md](references/community-pr-review.md) |
| **New BCL Type** | "add type", "new struct/class", "where does this go" | [new-bcl-type.md](references/new-bcl-type.md) |
| **General / Unsure** | "where do I find", "what's the process" | [resource-index.md](references/resource-index.md) |

## Step 2: Check Assembly Context

Before routing, identify the target assembly:

```powershell
# From the user's current directory or PR files
git diff --name-only HEAD~1 | Select-String "src/libraries/([^/]+)/" -AllMatches | ForEach-Object { $_.Matches[0].Groups[1].Value } | Sort-Object -Unique
```

Then check the assembly type (this changes the protocol):

```powershell
# Check if it's a facade, partial facade, or normal library
Select-String -Path "src/libraries/<LibName>/src/*.csproj" -Pattern "IsPartialFacadeAssembly|ContractTypesPartiallyMoved"
```

## Step 3: Route to Protocol

Load the appropriate reference document and walk through it step by step.

### Quick-Route Table

| If you're doing... | And the assembly is... | Then... |
|---|---|---|
| Adding a new type | Pure facade | Convert to partial facade first → [new-bcl-type.md](references/new-bcl-type.md) |
| Adding a new type | CoreLib | Use `Shared.projitems`, centralized ThrowHelper → [new-bcl-type.md](references/new-bcl-type.md) |
| Adding a new type | Normal library | Straightforward: `src/` + `ref/` + tests → [new-bcl-type.md](references/new-bcl-type.md) |
| API proposal | Any | Full pipeline: research → prototype → review → draft → publish → [new-api-workflow.md](references/new-api-workflow.md) |
| CI is red | Any | Script first, then triage → [ci-failure-triage.md](references/ci-failure-triage.md) |
| Breaking change? | Any | 4-bucket classification → [breaking-change-eval.md](references/breaking-change-eval.md) |
| Community PR | Any | Checklist: API surface, tests, breaking changes, conventions → [community-pr-review.md](references/community-pr-review.md) |

## Step 4: Enrich with MCP Tools (Prefer Over Static Summaries)

Before summarizing a document yourself, check if an MCP server can provide
the authoritative content. This keeps responses current and avoids stale
baked-in summaries.

| Need | MCP Server | Tool Call |
|------|-----------|-----------|
| .NET API docs, FDG, compat rules | `microsoftdocs` | `microsoftdocs:search` with query |
| Upstream repo docs (breaking-changes.md, etc.) | `github` | `github:get_file_contents` with owner/repo/path |
| CI pipeline status or history | `azure-devops` | `azure-devops:*` for dnceng-public pipelines |
| NuGet package metadata | `nuget` | `nuget:*` for package versions/dependencies |
| Build log analysis | `baronfel-binlog` | `baronfel-binlog:*` for MSBuild binlog parsing |

**When to use MCP vs static references:**
- Process steps, decision trees, checklists → use the reference files in this skill
- Documentation content the user needs to read → fetch live via MCP
- External tools with no MCP (Helix, apisof.net, grep.app) → provide direct links from [resource-index.md](references/resource-index.md)

## Step 5: Provide Links and Next Actions

Always end with:
1. **Specific link(s)** to the relevant upstream documentation
2. **Concrete next command** to run or action to take
3. **Who to ask** if the protocol doesn't cover their case

## Important: Skill Boundaries

This skill is a **router and advisor**. It does NOT:
- Write or modify code (use other skills for that)
- Run CI analysis scripts (use `ci-analysis` skill)
- Create API proposals (use `api-proposal` skill)
- Review code (use `code-review` skill)
- Summarize docs that an MCP server can provide live

It DOES:
- Tell you WHICH skill to use
- Tell you WHAT documentation to read (or which MCP to query)
- Tell you WHAT steps to follow
- Tell you WHO to ask
- Provide the actual links to everything above
