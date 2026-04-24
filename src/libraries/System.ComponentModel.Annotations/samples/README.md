# System.ComponentModel.Annotations Samples

## BasicAsyncSample

Minimal sample covering every variation of writing a custom async validation attribute
or self-validating entity using the new async validation infrastructure.

### Scenarios Covered

| # | Pattern | Mechanism | Sync Fallback? |
|---|---------|-----------|----------------|
| 1 | Reusable property attribute (async-only) | `AsyncValidationAttribute` on property | No |
| 2 | Reusable property attribute (async-only, parameterized) | `AsyncValidationAttribute` on property | No |
| 3 | Reusable entity-level (class) attribute | `AsyncValidationAttribute` on class | Yes |
| 4 | Non-reusable entity validation (cross-property) | `IAsyncValidatableObject` | No |
| 5 | Non-reusable property-scoped validation | `IAsyncValidatableObject` + `MemberNames` | No |

Scenario 3 also includes an async-parallel vs sync-sequential timing comparison.

### Project Structure

```
BasicAsyncSample/
├── Program.cs                  — Exercises all scenarios
├── EntityClasses/
│   ├── User.cs                 — Scenarios 1 & 2
│   ├── Event.cs                — Scenario 3
│   ├── Order.cs                — Scenario 4
│   └── Profile.cs              — Scenario 5
└── ValidationClasses/
    ├── IsValidNameAttribute.cs             — Scenario 1
    ├── AsyncOnlyEmailDomainAttribute.cs    — Scenario 2
    └── AsyncDateRangeValidAttribute.cs     — Scenario 3
```

### Building and Running

```powershell
# Build (from the sample directory)
$env:PATH = "C:\REPOS\runtime\.dotnet;$env:PATH"
cd src/libraries/System.ComponentModel.Annotations/samples/BasicAsyncSample
dotnet build BasicAsyncSample.csproj

# Run (requires the locally-built testhost with the new async APIs)
$testhost = "C:\REPOS\runtime\artifacts\bin\testhost\net11.0-windows-Debug-x64"
& "$testhost\dotnet.exe" exec "C:\REPOS\runtime\artifacts\bin\BasicAsyncSample\Debug\net11.0\BasicAsyncSample.dll"
```

---

## AsyncValidationConsoleDemo

Advanced sample demonstrating DI integration, two-phase validation, infrastructure
failure handling, and cancellation token propagation.

### Patterns Covered

| # | Pattern | API Used |
|---|---------|----------|
| 1 | DI Service Resolution + Multiple Async Attributes | `TryValidateObjectAsync`, `ValidationContext.GetService()` |
| 2 | Two-Phase Validation (sync blocks async) | `TryValidateObjectAsync` with mixed sync/async attrs |
| 3 | `IAsyncValidatableObject` (cross-property) | `TryValidateObjectAsync` with object-level async validation |
| 4 | Infrastructure Failure Handling | Exception propagation through async pipeline |
| 5 | CancellationToken Propagation | Pre-cancelled token flows through async pipeline |

### Running

```bash
cd samples/AsyncValidationConsoleDemo
dotnet run
```

---

## New API Surface

- `AsyncValidationAttribute` — Abstract base for async validation attributes
- `IAsyncValidatableObject` — Interface for object-level async validation
- `Validator.TryValidateObjectAsync` — Async version of `TryValidateObject`
- `Validator.TryValidatePropertyAsync` — Async version of `TryValidateProperty`
- `Validator.TryValidateValueAsync` — Async version of `TryValidateValue`
- `Validator.ValidateObjectAsync` — Async version of `ValidateObject`
- `Validator.ValidatePropertyAsync` — Async version of `ValidateProperty`
- `Validator.ValidateValueAsync` — Async version of `ValidateValue`
