---
applyTo: "src/libraries/**"
---

# BCL Library Development Instructions

When working on files under `src/libraries/`, follow these conventions:

## Build Infrastructure

- `EnableDefaultItems=false` is global — all `<Compile>` items must be explicit in the csproj
- Don't add `<Nullable>enable</Nullable>` — it's set globally
- String resources go in `Resources/Strings.resx` — `eng/resources.targets` auto-discovers them
- Don't use `ResXFileCodeGenerator` — it's legacy
- Use `<ProjectReference>` not `<Reference>` for assembly dependencies

## Assembly Types

Before adding code, check the target assembly's csproj for facade flags:
- **Pure facade** (`IsPartialFacadeAssembly=true`, no `<Compile>` items): Must convert to partial facade before adding real code
- **Partial facade** (`ContractTypesPartiallyMoved=true`): Use standalone ThrowHelper, not CoreLib's
- **Normal library**: Straightforward — add to `src/`, update `ref/`

## Code Conventions

- Use `Unsafe.BitCast<TFrom, TTo>()` over `Unsafe.As` for reinterpret casts
- Span length validation: `< N` (at least N), not `!= N` (exactly N)
- `[CLSCompliant(false)]` not `[System.CLSCompliantAttribute(false)]`
- `is null` / `is not null`, never `== null` / `!= null`
- `nameof()` over string literals for member names
- Use `[Theory]` with `[InlineData]` for tests, not duplicate `[Fact]` methods

## Reference Assemblies

After modifying public API:
```bash
cd src/libraries/<LibraryName>/src
dotnet msbuild /t:GenerateReferenceAssemblySource
```
Then manually verify: `init` → `set`, CLS pragmas, member completeness.

## Transitive Dependencies

When adding public members that reference types from another assembly, check if consuming
assemblies need a new `<ProjectReference>`. The C# compiler must resolve ALL overload
parameter types during overload resolution — even overloads that won't be called.
