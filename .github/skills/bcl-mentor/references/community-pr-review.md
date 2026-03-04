# Protocol: Community PR Review

Step-by-step guide for reviewing or iterating on a community-contributed PR in dotnet/runtime.

## Overview

```
Community PR arrives
       │
       ▼
  ┌─────────────────────────────────────┐
  │ Step 1: Understand the context      │
  │ - Read linked issue                 │
  │ - Check labels (api-approved?)      │
  │ - Read PR description               │
  └──────────────┬──────────────────────┘
                 │
                 ▼
  ┌─────────────────────────────────────┐
  │ Step 2: Check prerequisites         │
  │ - Is the API approved?              │
  │ - Are there breaking changes?       │
  │ - Is the area owner assigned?       │
  └──────────────┬──────────────────────┘
                 │
                 ▼
  ┌─────────────────────────────────────┐
  │ Step 3: Code review checklist       │
  │ - Code conventions                  │
  │ - Test coverage                     │
  │ - Ref assembly updated              │
  │ - Build infrastructure correct      │
  └──────────────┬──────────────────────┘
                 │
                 ▼
  ┌─────────────────────────────────────┐
  │ Step 4: Prototype / iterate         │
  │ - Checkout their branch locally     │
  │ - Build and test                    │
  │ - Provide actionable feedback       │
  └─────────────────────────────────────┘
```

## Step 1: Understand the Context

1. **Read the linked issue** — What was the original request?
2. **Check labels**:
   - `api-approved` → API surface is settled, focus on implementation quality
   - `api-suggestion` → API hasn't been reviewed yet — don't iterate on implementation until it's approved
   - `api-needs-work` → API was reviewed but needs changes — check reviewer notes
3. **Who is the contributor?** — First-time? Experienced? This adjusts feedback tone
4. **Is there a related spec or design doc?** — Check issue comments for design discussions

## Step 2: Check Prerequisites

| Prerequisite | How to Check | If Missing → |
|---|---|---|
| API is approved | Issue has `api-approved` label | Politely ask contributor to wait for API review |
| No breaking changes | Review public API diff | Route to [breaking-change-eval.md](breaking-change-eval.md) |
| Area owner assigned | Check issue assignee | Ping the area owner (see [issue-guide.md](https://github.com/dotnet/runtime/blob/main/docs/project/issue-guide.md)) |
| Tests exist | Check `tests/` directory in PR diff | Request tests before merging |

## Step 3: Code Review Checklist

### Security

- [ ] No new deserialization of untrusted input without validation
- [ ] No `TypeNameHandling`-style patterns (type-based polymorphic deserialization from untrusted JSON)
- [ ] No relaxed parsing defaults (`AllowTrailingCommas`, `ReadCommentHandling.Skip`) on paths handling untrusted input
- [ ] Unsafe code (`Unsafe.BitCast`, `Unsafe.As`, pointer operations) reviewed for bounds and type safety
- [ ] No accidental public API surface leaks (check ref assembly diff)
- [ ] If adding overloads: verify no overload resolution ambiguity that could silently route to wrong method

### Coding Conventions

Per [`.editorconfig`](https://github.com/dotnet/runtime/blob/main/.editorconfig) and [copilot-instructions.md](https://github.com/dotnet/runtime/blob/main/.github/copilot-instructions.md):

- [ ] File-scoped namespaces
- [ ] `is null` / `is not null` (not `== null`)
- [ ] `nameof()` instead of string literals
- [ ] Pattern matching and switch expressions where applicable
- [ ] `ObjectDisposedException.ThrowIf` where applicable
- [ ] No `var` abuse — use explicit types for clarity in BCL code
- [ ] `Unsafe.BitCast` over `Unsafe.As` for reinterpret casts
- [ ] Span validation: `< N` not `!= N`

### Test Quality

- [ ] Uses `[Theory]` with `[InlineData]`/`[MemberData]` over duplicate `[Fact]` methods
- [ ] Tests added to existing test files (not new files unless necessary)
- [ ] No regression comments citing issue numbers (unless explicitly asked)
- [ ] No "Arrange/Act/Assert" comments
- [ ] Edge cases covered: null, empty, boundary values

### Build Infrastructure

- [ ] `<Compile>` items explicit (if `EnableDefaultItems=false` in csproj)
- [ ] No redundant `<Nullable>enable</Nullable>` (it's global)
- [ ] `<ProjectReference>` correct (not `<Reference>`)
- [ ] Ref assembly updated: `dotnet msbuild /t:GenerateReferenceAssemblySource`
- [ ] No accidental public API surface leaks

### ThrowHelper Patterns

| Context | Pattern |
|---|---|
| **CoreLib** | Centralized `ThrowHelper` with enum: `ThrowHelper.ThrowArgumentException(ExceptionResource.Arg_X)` |
| **Libraries** | Standalone per-assembly: `throw new ArgumentException(SR.Format(SR.ResourceName, args))` |

### Transitive Dependencies

If the PR adds methods to a type's public API that reference types from another assembly, check consuming assemblies:

```powershell
# Find who references this library
Select-String -Path "src/libraries/*/src/*.csproj" -Pattern "System.Drawing.Primitives" -List
```

See the [transitive dependency explanation](https://github.com/dotnet/runtime/blob/main/docs/coding-guidelines/breaking-change-definitions.md) — overload resolution forces the compiler to resolve ALL parameter types.

## Step 4: Prototype / Iterate

### Working with a Community Contributor's Branch

```powershell
# Add their fork as a remote
git remote add contributor https://github.com/<user>/runtime.git
git fetch contributor

# Create a worktree for their branch
git worktree add ../review-<pr-number> contributor/<branch-name>
cd ../review-<pr-number>

# Build and test
.\build.cmd clr+libs -rc release  # baseline (if not done)
cd src\libraries\<LibraryName>
dotnet build
dotnet build /t:test .\tests\<TestProject>.csproj
```

### Providing Feedback

- **Be specific** — "Line 42: use `is null` instead of `== null`" not "fix style"
- **Explain WHY** — "CoreLib uses centralized ThrowHelper because..." not just "wrong pattern"
- **Batch feedback** — one comprehensive review, not 10 small ones
- **Distinguish blocking vs suggestions** — clearly mark what must change vs nice-to-have

## Key Links

| Resource | URL |
|---|---|
| Contribution Guidelines | https://github.com/dotnet/runtime/blob/main/CONTRIBUTING.md |
| API Review Process | https://github.com/dotnet/runtime/blob/main/docs/project/api-review-process.md |
| Issue Guide (Area Owners) | https://github.com/dotnet/runtime/blob/main/docs/project/issue-guide.md |
| EditorConfig | https://github.com/dotnet/runtime/blob/main/.editorconfig |
| Copilot Instructions | https://github.com/dotnet/runtime/blob/main/.github/copilot-instructions.md |

## Tips for Newcomers

1. **You can iterate on community PRs** — push to their branch if they give permission
2. **Don't approve PRs you're not confident about** — ask the area owner
3. **Community contributors may not have CI access** — help them triage failures
4. **Be kind** — community contributions are volunteer work
