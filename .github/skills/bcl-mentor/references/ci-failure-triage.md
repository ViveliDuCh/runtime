# Protocol: CI Failure Triage

Step-by-step guide for when CI is red on your PR in dotnet/runtime.

## Overview

```
CI is red on your PR
       │
       ▼
  ┌─────────────────────────────────────────────┐
  │ Step 1: Open "PR Build Analysis" check      │
  │         (GitHub → Checks tab)               │
  └──────────────────┬──────────────────────────┘
                     │
              ┌──────┴──────┐
              │             │
         Known Issue    Unknown Failure
              │             │
              ▼             ▼
     Already tracked    ┌───────────────┐
     No action needed   │ Step 2: Is it │
                        │ MY fault?     │
                        └───┬───────┬───┘
                            │       │
                          YES      NO
                            │       │
                            ▼       ▼
                      Fix code   ┌──────────────────┐
                      Push       │ Step 3: File a   │
                                 │ Known Issue      │
                                 │ + /ba-g comment  │
                                 └──────────────────┘
```

## Step-by-Step

### Step 1: Read Build Analysis

1. Go to your PR → **Checks** tab → click **"PR Build Analysis"**
2. Look at the summary:
   - **Known test errors**: Already tracked — no action needed from you
   - **Regressions**: Likely caused by your PR
   - **Unknown failures**: Need investigation

### Step 2: Determine if It's Your Fault

| Question | How to Check | If YES → | If NO → |
|---|---|---|---|
| Does the failing test relate to code you changed? | Compare `git diff --name-only` with test name/namespace | Fix your code | Continue checking |
| Does the same test pass on `main`? | Check [AzDO pipeline history](https://dev.azure.com/dnceng-public/public/_build?definitionId=130) | Your PR broke it | It's a pre-existing issue |
| Is it an intermittent failure? | Re-run the failed check; check [Known Issues Board](https://github.com/orgs/dotnet/projects/111) | Flaky test | Investigate further |
| Is it platform-specific (ARM, iOS, WASM)? | Check the leg name | Often infrastructure | Check with area owner |

### Step 3: File a Known Issue (if not your fault)

**Reference**: https://github.com/dotnet/arcade/blob/main/Documentation/Projects/Build%20Analysis/KnownIssues.md

#### Decide: Infrastructure or Repository?

| Type | Where to File | When |
|---|---|---|
| **Infrastructure** | [dotnet/dnceng](https://github.com/dotnet/dnceng/issues/new) | Affects multiple repos, needs `@dotnet/dnceng` |
| **Repository** | [dotnet/runtime](https://github.com/dotnet/runtime/issues/new) | Specific to this repo |

Criteria from the [official guidance](https://github.com/dotnet/arcade/blob/main/Documentation/Projects/Build%20Analysis/KnownIssues.md#decide-infrastructure-or-repository-issue):
- Network timeouts, Docker pull failures, machine issues → **Infrastructure**
- Test logic failures, compilation errors, API compat → **Repository**

#### Fill the JSON Error Section

```json
{
    "ErrorMessage": "<unique string from the error — no machine names, paths, or timestamps>",
    "BuildRetry": false,
    "ErrorPattern": "",
    "ExcludeConsoleLog": false
}
```

**Good ErrorMessage**: `"(NETCORE_ENGINEERING_TELEMETRY=Restore) Failed to retrieve information"`
**Bad ErrorMessage**: `".dotnet/sdk/6.0.100-rc.1.21411.28/NuGet.RestoreEx.targets"` (includes version)

Add the label: **`Known Build Error`**

#### Helper tool

Use the [Build Analysis Known Issue Helper](https://helix.dot.net/BuildAnalysis/CreateKnownIssues) to assist with creating the issue and validating the JSON blob.

### Step 4: Unblock Your PR

Comment on your PR:
```
/ba-g opened issue #XXXXX for the unknown test error
```

Then enable auto-merge.

### Step 5: Retry CI

| Command | Effect |
|---|---|
| `/azp run runtime` | Re-run all pipelines |
| `/azp run runtime-extra-platforms` | Re-run extra platforms |
| Click "Re-run failed checks" | Re-run only failed checks (preferred) |

## Key Links

| Resource | URL |
|---|---|
| Known Issues Documentation | https://github.com/dotnet/arcade/blob/main/Documentation/Projects/Build%20Analysis/KnownIssues.md |
| Known Issues Board | https://github.com/orgs/dotnet/projects/111 |
| Runtime Failure Analysis Guide | https://github.com/dotnet/runtime/blob/main/docs/workflow/ci/failure-analysis.md |
| AzDO Runtime Pipeline | https://dev.azure.com/dnceng-public/public/_build?definitionId=130 |
| Helix Portal | https://helix.dot.net/ |
| Known Issue Helper | https://helix.dot.net/BuildAnalysis/CreateKnownIssues |
| JSON Escape Tool | https://www.freeformatter.com/json-escape.html |
| Regex Tester | https://regex101.com/ |

## Common Patterns

| Error Pattern | Likely Cause | Action |
|---|---|---|
| `exit code 134` (SIGABRT) | Process abort | Check if it's a known crash |
| `exit code 139` (SIGSEGV) | Segmentation fault | Usually infrastructure |
| `exit code -4` | Helix wrapper timeout | Check if tests actually passed (see ci-analysis skill) |
| Package not found on flow PR | Dependency not yet published | NOT infrastructure — check the package |
| NSPOSIXErrorDomain error 49 | iOS device issue | Infrastructure |
| XHarness exit code 78 | Package install failure | Infrastructure |

## Tips for Newcomers

1. **Don't panic** — most CI failures on new PRs are pre-existing flaky tests
2. **Always check Build Analysis first** — it saves time
3. **Don't retry blindly** — understand the failure before retrying
4. **Ask on Teams** — [Infrastructure channel](https://teams.microsoft.com/l/channel/19%3ab27b36ecd10a46398da76b02f0411de7%40thread.skype/Infrastructure) for corpnet, [Discord #runtime](https://aka.ms/dotnet-discord) otherwise
