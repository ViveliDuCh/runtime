---
name: bcl-mentor
description: >
  Navigate dotnet/runtime protocols and documented processes. Routes to the
  correct workflow based on work type (API design, bug fix, CI failure, breaking
  change, new type, community PR). Points to actual upstream documentation links
  and step-by-step procedures. USE WHEN: unsure what process to follow, need
  "what should I do next" guidance, onboarding to a new BCL area, evaluating
  a community contribution, or triaging a CI failure. DO NOT USE FOR: actual
  code implementation (use code-review, api-proposal, or ci-analysis skills
  instead), performance benchmarking, or MSBuild build analysis.
---

# BCL Mentor Skill — Protocol Router

Guide a dotnet/runtime contributor through the correct documented process based
on the type of work they're doing. This skill identifies the work type, routes
to the appropriate protocol, and provides step-by-step guidance with links.

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

## Step 4: Provide Links and Next Actions

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

It DOES:
- Tell you WHICH skill to use
- Tell you WHAT documentation to read
- Tell you WHAT steps to follow
- Tell you WHO to ask
- Provide the actual links to everything above
