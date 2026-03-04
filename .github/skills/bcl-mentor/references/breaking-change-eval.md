# Protocol: Breaking Change Evaluation

Step-by-step guide for evaluating whether a proposed change is a breaking change in dotnet/runtime.

## Overview

```
Proposed code change
       │
       ▼
  ┌─────────────────────────────────────────┐
  │ Does it modify PUBLIC API behavior?     │
  │ (return type, parameter, exception,     │
  │  timing, parsing, serialization)        │
  └────────────────┬────────────────────────┘
                   │
            ┌──────┴──────┐
            │             │
           YES           NO → Likely safe (Bucket 4)
            │
            ▼
  ┌──────────────────────────────────────────┐
  │ Classify into a bucket (1-4)            │
  │ → Bucket 1: Clear public contract break │
  │ → Bucket 2: Reasonable grey area        │
  │ → Bucket 3: Unlikely grey area          │
  │ → Bucket 4: Clearly non-public          │
  └────────────────┬─────────────────────────┘
                   │
            ┌──────┴──────┐
            │             │
     Bucket 1        Buckets 2-3
     (reject)        (risk-benefit analysis)
                          │
                   ┌──────┴──────┐
                   │             │
            Accepted +      Rejected
            compat switch   (or accepted as-is)
```

## The 4 Buckets

**Reference**: https://github.com/dotnet/runtime/blob/main/docs/coding-guidelines/breaking-changes.md

### Bucket 1: Public Contract Violation ❌

_Clear violation — almost never accepted._

Examples:
- Renaming or removing a public type, member, or parameter
- Changing the value of a public constant or enum member
- Sealing a type that wasn't sealed
- Making a virtual member abstract
- Changing a return type
- Adding an interface to a base interface set

### Bucket 2: Reasonable Grey Area ⚠️

_Behavior change customers would have reasonably depended on._

Examples:
- Throwing a new/different exception in a common scenario
- An exception is no longer thrown
- Different behavior for an input
- Decreasing the range of accepted values
- Change in timing/order of events
- Change in parsing behavior

### Bucket 3: Unlikely Grey Area 🟡

_Behavior change customers could have depended on, but probably wouldn't._

Examples:
- Correcting behavior in a subtle corner case

### Bucket 4: Clearly Non-Public ✅

_Internal changes that theoretically could break apps._

Examples:
- Changes to internal API that break private reflection

## Process

**Reference**: https://github.com/dotnet/runtime/blob/main/docs/project/breaking-change-process.md

### Step 1: Create or Link to an Issue

The issue must include:
- [ ] `breaking-change` label
- [ ] Goals and motivation for the change
- [ ] Pre-change behavior
- [ ] Post-change behavior
- [ ] Versions affected
- [ ] Expected errors when running old code
- [ ] Workarounds and mitigations (including AppContext switches)
- [ ] Link to the feature/bug fix issue

### Step 2: Engage Stakeholders

- @mention `@dotnet/compat` team on the issue
- Engage with commenters
- Provide a design doc if the change is significant

### Step 3: Label the PR

- Add `breaking-change` label to associated PRs
- Link PRs to the breaking change issue

### Step 4: After Merge

- Create a [docs issue](https://github.com/dotnet/docs/issues/new?template=dotnet-breaking-change.md)
- Specify which .NET preview the break ships in

## Quick Decision Aid

| What changed? | Bucket | Likely outcome |
|---|---|---|
| Removed a public method | 1 | **Rejected** |
| New exception on null input (was NRE before) | 2 | Risk-benefit analysis |
| Fixed off-by-one in obscure edge case | 3 | Usually accepted |
| Changed internal helper method | 4 | Accepted |
| Added new overload (could affect resolution) | 2 | Check source compat |
| Changed default value of optional param | 2 | Risk-benefit analysis |
| Obsoleted a method | Not breaking | Accepted (with proper attributes) |

## Checking for Source Breaking Changes

New overloads and extension methods can cause **source breaking changes** even without modifying existing signatures:

```csharp
// Before: only one FromArgb overload
Color.FromArgb(42);  // resolves to FromArgb(int)

// After: new overload added
Color.FromArgb(Argb<byte> argb);  // now compiler must resolve — could affect implicit conversions
```

**How to detect**: Build the TEST project (not just src) — test code exercises real calling patterns.

## Key Links

| Document | URL |
|---|---|
| Breaking Changes (classification) | https://github.com/dotnet/runtime/blob/main/docs/coding-guidelines/breaking-changes.md |
| Breaking Change Process | https://github.com/dotnet/runtime/blob/main/docs/project/breaking-change-process.md |
| Breaking Change Definitions | https://github.com/dotnet/runtime/blob/main/docs/coding-guidelines/breaking-change-definitions.md |
| Breaking Change Rules | https://github.com/dotnet/runtime/blob/main/docs/coding-guidelines/breaking-change-rules.md |
| Compat Team | https://github.com/orgs/dotnet/teams/compat |
| Docs Issue Template | https://github.com/dotnet/docs/issues/new?template=dotnet-breaking-change.md |

## Tips for Newcomers

1. **When in doubt, assume it's breaking** — err on the side of caution
2. **Source compat ≠ binary compat** — a change can be source-breaking but binary-compatible
3. **Overload resolution is subtle** — adding a new overload can change which method existing code calls
4. **AppContext switches are reactive** — only add them when real customers hit issues, not proactively
5. **Talk to @dotnet/compat early** — before writing code, not after
