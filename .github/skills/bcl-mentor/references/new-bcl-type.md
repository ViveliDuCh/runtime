# Protocol: Adding a New BCL Type

Step-by-step guide for adding a new public type to a dotnet/runtime library.
Based on learnings from the blittable-color sessions (adding `Argb<T>` and `Rgba<T>` to `System.Numerics.Vectors`).

## Overview

```
You need to add a new public type
       │
       ▼
  ┌──────────────────────────────────────┐
  │ Step 1: Is the API approved?         │
  │ Check for api-approved label         │
  └───────────────┬──────────────────────┘
                  │
                  ▼
  ┌──────────────────────────────────────┐
  │ Step 2: Identify the target assembly │
  │ Check csproj for facade flags        │
  └───────────────┬──────────────────────┘
                  │
           ┌──────┴──────────────────┐
           │              │          │
     Pure Facade    Partial Facade   Normal Lib
           │              │          │
           ▼              ▼          ▼
     Convert first   Add as real    Add to src/
     (see below)     code in src/   straightforward
                     Use standalone
                     ThrowHelper
```

## Decision Tree: Where Does the Type Go?

```
Is the target assembly a pure facade?
  (Check: IsPartialFacadeAssembly=true AND zero Compile items)
  │
  ├─ YES (e.g., System.Numerics.Vectors before Colors)
  │   │
  │   ├─ Does the JIT/runtime need the type? (intrinsics, GC-special, etc.)
  │   │   ├─ YES → Put in CoreLib (System.Private.CoreLib)
  │   │   └─ NO  → Convert to partial facade first
  │   │           (add ContractTypesPartiallyMoved=true)
  │   │           then put as "real code" in the assembly
  │   │
  │   └─ Would moving it to CoreLib create a circular dependency?
  │       ├─ YES → Must stay outside CoreLib
  │       └─ NO  → Depends on reviewer consensus
  │
  ├─ PARTIAL FACADE (IsPartialFacadeAssembly + ContractTypesPartiallyMoved)
  │   ├─ Add as real code in the assembly's src/ directory
  │   ├─ Use standalone ThrowHelper (not CoreLib's)
  │   └─ Ref assembly: use #if !BUILDING_CORELIB_REFERENCE guard
  │
  └─ NORMAL LIBRARY (no facade flags)
      └─ Add directly to src/ — straightforward
```

## Step-by-Step Checklist

### 1. Source Placement

| Assembly Type | Where to Put Source | Key Detail |
|---|---|---|
| **CoreLib** | `System.Private.CoreLib.Shared.projitems` | Add `<Compile>` entry to shared items project |
| **Partial Facade (real code)** | `src/libraries/<Lib>/src/` | Explicit `<Compile>` items (EnableDefaultItems=false) |
| **Normal Library** | `src/libraries/<Lib>/src/` | Explicit `<Compile>` items (EnableDefaultItems=false) |

### 2. Update the Project File (csproj)

```xml
<!-- Every source file must be explicitly listed -->
<Compile Include="System\Numerics\Colors\Argb.cs" />
<Compile Include="System\Numerics\Colors\Argb.T.cs" />
```

**Common mistakes**:
- ❌ Using `<Reference>` instead of `<ProjectReference>` → 264 build errors
- ❌ Adding `<Nullable>enable</Nullable>` → already global, causes warnings
- ❌ Using `<EmbeddedResource>` for resx → `eng/resources.targets` auto-discovers from `Resources/`

### 3. Reference Assembly

```bash
cd src/libraries/<LibraryName>/src
dotnet msbuild /t:GenerateReferenceAssemblySource
```

Then manually verify and fix:
- [ ] `init` accessors → change to `set` (GenAPI limitation)
- [ ] CLS compliance pragmas: `#pragma warning disable CS3015`
- [ ] Member completeness — compare with implementation

For partial facades, the ref assembly needs:
```csharp
#if !BUILDING_CORELIB_REFERENCE
// Types that live in the DLL, not in CoreLib
#endif
```

### 4. ThrowHelper Pattern

| Context | Pattern | Example |
|---|---|---|
| **CoreLib** | Centralized + enum | `ThrowHelper.ThrowArgumentException(ExceptionResource.Arg_InvalidColorComponent)` |
| **Libraries** | Standalone + SR.Format | `throw new ArgumentOutOfRangeException(nameof(value), SR.Format(SR.Arg_OutOfRange, value))` |

### 5. String Resources

Place `Strings.resx` in `Resources/` folder. The build system auto-discovers it:
```
src/libraries/<LibraryName>/src/Resources/Strings.resx
```

Do NOT use `ResXFileCodeGenerator` — it's legacy. The `eng/resources.targets` handles everything.

### 6. External References

If creating or removing assemblies, update:
- [ ] `src/libraries/NetCoreAppLibrary.props` (assembly list)
- [ ] `src/libraries/netstandard/ref/netstandard.csproj` (shim)
- [ ] `eng/testing/linker/SupportFiles/dotnet-singlefile/crossgen2_comparison.py` (crossgen)
- [ ] Any consuming library csprojs

### 7. Build and Validate

```powershell
# Full build (first time)
.\build.cmd clr+libs -rc release

# After changes to CoreLib:
.\build.cmd clr.corelib+clr.nativecorelib+libs.pretest -rc release

# Build specific library:
cd src\libraries\<LibraryName>
dotnet build

# Run tests:
dotnet build /t:test .\tests\<TestProject>.csproj

# Validate API compat:
dotnet build /p:ApiCompatValidateAssemblies=true
```

### 8. Tests

- Add to existing test files when possible
- Use `[Theory]` with `[InlineData]`
- No `ImplicitUsings` — explicit `using` directives
- Test edge cases: null, empty, boundary values, different generic types

## Code Conventions Discovered (from sessions)

| Convention | ❌ Wrong | ✅ Correct |
|---|---|---|
| Span validation | `!= 4` (exactly N) | `< 4` (at least N) |
| ToString format | `[ARGB Color: ...]` | `<A, R, G, B>` angle-bracket style |
| Reinterpret cast | `Unsafe.As<TFrom, TTo>(ref value)` | `Unsafe.BitCast<TFrom, TTo>(value)` |
| CLS attribute | `[System.CLSCompliantAttribute(false)]` | `[CLSCompliant(false)]` |
| Nullable | `<Nullable>enable</Nullable>` in csproj | Omit — set globally |
| Resource generator | `<EmbeddedResource>` with generator | Let `resources.targets` auto-discover |

## Key Links

| Document | URL |
|---|---|
| Building Libraries | https://github.com/dotnet/runtime/blob/main/docs/workflow/building/libraries/README.md |
| Testing Libraries | https://github.com/dotnet/runtime/blob/main/docs/workflow/testing/libraries/testing.md |
| Updating Ref Source | https://github.com/dotnet/runtime/blob/main/docs/coding-guidelines/updating-ref-source.md |
| Repo Organization | https://github.com/dotnet/runtime/blob/main/docs/project/repo-organization.md |
| Framework Design Guidelines | https://github.com/dotnet/runtime/blob/main/docs/coding-guidelines/framework-design-guidelines-digest.md |

## Tips for Newcomers

1. **Get the placement right on the first try** — this avoids the 3-pivot journey (standalone → CoreLib → partial facade → CoreLib again)
2. **Build locally before pushing** — CI is slow; catch errors early
3. **Check the csproj first** — it tells you everything about the assembly's nature
4. **Don't create new assemblies unless explicitly told to** — reviewers strongly prefer using existing ones
5. **GenAPI is your friend** — but it has bugs with `init` accessors, so always manually verify
