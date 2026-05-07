# System.ComponentModel.Annotations Async Validation Samples

Demonstrates the async validation APIs (`AsyncValidationAttribute`,
`IAsyncValidatableObject`, `Validator.TryValidateObjectAsync`, etc.) across
Console, WinForms, and WPF applications. All samples share a common set of
entity, validation, and service classes via the `SharedModels` project.

## Folder Structure

```
samples/
├── SharedModels/                       ← Class library shared by ALL samples
│   ├── EntityClasses/                  ← User, Event, Order, Profile, UserRegistration, MoneyTransfer
│   ├── ValidationClasses/              ← IsValidName, AsyncOnlyEmailDomain, AsyncDateRangeValid,
│   │                                     UniqueEmail, UniqueUsername
│   └── ServiceClasses/                 ← UserService, SimpleServiceProvider
│
├── Console/
│   ├── BasicAsyncSample/               ← 5 async validation scenarios + timing comparison
│   └── AsyncValidationConsoleDemo/     ← DI, two-phase, error handling, cancellation
│
├── WinForms/
│   ├── AsyncBasicSample/               ← Programmatic controls + async ErrorProvider bridge
│   ├── AsyncValidationDemo/            ← DI-backed async validation (Registration, Transfer, Errors)
│   ├── AsyncDesignerBasicSample/       ← Designer-generated controls + async validation
│   └── AsyncDesignerValidationDemo/    ← Designer + DI + async validation
│
└── WPF/
    ├── AsyncManualSample/              ← Manual INotifyDataErrorInfo async bridge (~50 LOC base)
    └── AsyncToolkitSample/             ← CommunityToolkit.Mvvm ObservableValidator + async
```

---

## SharedModels

Shared class library containing all entity, validation attribute, and service
classes. Uses `AsyncValidationAttribute`, `IAsyncValidatableObject`, and
`Task.Delay` to simulate async I/O.

---

## Console Samples

### BasicAsyncSample

Minimal sample covering every variation of async validation:

| # | Pattern | Mechanism |
|---|---------|-----------|
| 1 | Reusable property attribute (async-only) | `AsyncValidationAttribute` on property |
| 2 | Reusable property attribute (parameterized) | `AsyncValidationAttribute` on property |
| 3 | Reusable entity-level (class) attribute | `AsyncValidationAttribute` on class |
| 4 | Non-reusable entity validation (cross-property) | `IAsyncValidatableObject` |
| 5 | Non-reusable property-scoped validation | `IAsyncValidatableObject` + `MemberNames` |

Includes async-parallel vs sync-sequential timing comparison for Scenario 3.

### AsyncValidationConsoleDemo

Advanced sample with DI integration, two-phase validation, infrastructure
failure handling, and cancellation token propagation.

---

## WinForms Samples

### AsyncBasicSample
Programmatic UI (no designer). Four tabs (User, Event, Order, Profile) with
`ErrorProvider` wired to `Validator.TryValidateObjectAsync`.

### AsyncValidationDemo
DI-backed validation using `UserRegistration` and `MoneyTransfer` models.
Demonstrates duplicate detection, `IAsyncValidatableObject`, infrastructure
error handling, and two-phase validation.

### AsyncDesignerBasicSample / AsyncDesignerValidationDemo
Same scenarios as above but with designer-generated `InitializeComponent`
controls. Shows the pattern for integrating async validation into
designer-based WinForms apps.

---

## WPF Samples

### AsyncManualSample
Manual `INotifyDataErrorInfo` bridge using `ValidatableViewModelBase` (~50 LOC).
Calls `Validator.TryValidatePropertyAsync` per-property and
`Validator.TryValidateObjectAsync` on "Validate All". UI remains responsive.

### AsyncToolkitSample
Uses `CommunityToolkit.Mvvm` `ObservableValidator` for zero-boilerplate
`INotifyDataErrorInfo`. Full-object async validation via
`Validator.TryValidateObjectAsync` on button click.

---

## Building and Running

### Console Samples (build in-repo)

Console samples and SharedModels build directly within the `dotnet/runtime`
repo because they only need `Microsoft.NETCore.App` framework types.

```powershell
# 1. Configure the locally-built SDK
$env:PATH = "<repo-root>\.dotnet;$env:PATH"

# 2. Build SharedModels + a specific console sample
cd src/libraries/System.ComponentModel.Annotations/samples/Console/BasicAsyncSample
dotnet build

# 3. Run (requires the locally-built testhost with the new async APIs)
$testhost = "<repo-root>\artifacts\bin\testhost\net11.0-windows-Debug-x64"
& "$testhost\dotnet.exe" exec `
    "<repo-root>\artifacts\bin\BasicAsyncSample\Debug\net11.0\BasicAsyncSample.dll"
```

### WinForms / WPF Samples (build out-of-repo)

> **Why can't these build inside the runtime repo?**
>
> The `dotnet/runtime` build system uses a *local targeting pack* that
> replaces the installed SDK's framework references. This local pack only
> provides `Microsoft.NETCore.App` assemblies (the runtime libraries being
> built). It does **not** provide the `Microsoft.WindowsDesktop.App` framework
> (`System.Windows.Forms`, `System.Windows`, PresentationFramework, etc.)
> because those assemblies live in separate repositories (`dotnet/winforms`,
> `dotnet/wpf`).
>
> Specifically, the repo's build infrastructure:
>
> 1. Sets `DisableImplicitFrameworkReferences=true` for projects targeting
>    `$(NetCoreAppCurrent)`, which suppresses all SDK-provided framework
>    references — including `Microsoft.WindowsDesktop.App.WindowsForms` and
>    `Microsoft.WindowsDesktop.App.WPF`.
> 2. Overrides `KnownFrameworkReference` to point to the locally-built
>    `Microsoft.NETCore.App.Ref` targeting pack.
> 3. Sets `EnableTargetingPackDownload=false`, preventing the SDK from
>    downloading any additional framework packs at restore time.
>
> Console samples work because they only depend on types from
> `Microsoft.NETCore.App` (provided by the local targeting pack) plus the
> locally-built `System.ComponentModel.Annotations` (via project reference).
> WinForms/WPF samples additionally need Windows Desktop types that are not
> available in the local targeting pack.

To build and test the WinForms / WPF samples, copy them out of the repo and
point them at the locally-built `System.ComponentModel.Annotations` DLL via a
local NuGet feed or direct DLL reference.

#### Step-by-step: Build WinForms / WPF samples outside the repo

```powershell
# ─── Prerequisites ───────────────────────────────────────────────────
# 1. Build the runtime repo first (from the repo root):
#    .\build.cmd clr+libs -rc release
#    This produces the locally-built System.ComponentModel.Annotations.dll
#    with the new async validation APIs.

# ─── Step 1: Create a working directory outside the repo ─────────────
$workDir = "C:\temp\async-validation-samples"
New-Item -ItemType Directory -Force -Path $workDir

# ─── Step 2: Copy the sample projects ────────────────────────────────
$samplesRoot = "<repo-root>\src\libraries\System.ComponentModel.Annotations\samples"
Copy-Item -Recurse "$samplesRoot\SharedModels" "$workDir\SharedModels"
Copy-Item -Recurse "$samplesRoot\WinForms"     "$workDir\WinForms"
Copy-Item -Recurse "$samplesRoot\WPF"          "$workDir\WPF"

# ─── Step 3: Create a local NuGet feed with the built assembly ───────
$feedDir = "$workDir\local-feed"
New-Item -ItemType Directory -Force -Path $feedDir

# Find the locally-built DLL
$annotationsDll = "<repo-root>\artifacts\bin\System.ComponentModel.Annotations\Debug\net11.0\System.ComponentModel.Annotations.dll"

# Create a minimal .nupkg (or use the ref assembly directly)
# The simplest approach: reference the DLL directly instead of NuGet.

# ─── Step 4: Update SharedModels.csproj ──────────────────────────────
# Replace the project references with a direct DLL reference.
# Change SharedModels.csproj from:
#
#   <ProjectReference Include="$(LibrariesProjectRoot)..." />
#
# To:
#
#   <Reference Include="System.ComponentModel.Annotations"
#              HintPath="<repo-root>\artifacts\bin\System.ComponentModel.Annotations\Debug\net11.0\System.ComponentModel.Annotations.dll" />
#
# Also change the TargetFramework from $(NetCoreAppCurrent) to a concrete
# value like net11.0, and remove the runtime-specific suppressions.

# ─── Step 5: Update all .csproj files ────────────────────────────────
# For each WinForms/WPF project csproj:
#   a. Change <TargetFramework>$(NetCoreAppCurrent)-windows</TargetFramework>
#      to    <TargetFramework>net11.0-windows</TargetFramework>
#   b. Remove EnableDefaultItems, EnableTrimAnalyzer, EnableAotAnalyzer,
#      and the NoWarn suppressions (they are repo-specific).
#   c. Ensure UseWindowsForms / UseWPF is set.
#   d. Add <ImplicitUsings>enable</ImplicitUsings> for convenience.

# ─── Step 6: Remove the Directory.Build.props ────────────────────────
# The copied Directory.Build.props has an <Import> pointing back into the
# repo. Delete it — the SDK defaults are sufficient for standalone builds.
Remove-Item "$workDir\SharedModels\..\Directory.Build.props" -ErrorAction Ignore

# ─── Step 7: Build ───────────────────────────────────────────────────
cd $workDir\WinForms\AsyncBasicSample
dotnet build

# ─── Step 8: Run ─────────────────────────────────────────────────────
# Option A: run directly (if using the installed .NET 11 SDK)
dotnet run

# Option B: run against the locally-built testhost
$testhost = "<repo-root>\artifacts\bin\testhost\net11.0-windows-Debug-x64"
& "$testhost\dotnet.exe" exec `
    "$workDir\WinForms\AsyncBasicSample\bin\Debug\net11.0-windows\AsyncBasicSample.dll"
```

#### Quick reference: standalone SharedModels.csproj

```xml
<Project Sdk="Microsoft.NET.Sdk">
  <PropertyGroup>
    <TargetFramework>net11.0</TargetFramework>
    <Nullable>enable</Nullable>
    <ImplicitUsings>enable</ImplicitUsings>
  </PropertyGroup>
  <ItemGroup>
    <Reference Include="System.ComponentModel.Annotations"
               HintPath="<repo-root>\artifacts\bin\System.ComponentModel.Annotations\Debug\net11.0\System.ComponentModel.Annotations.dll" />
  </ItemGroup>
</Project>
```

#### Quick reference: standalone WinForms .csproj

```xml
<Project Sdk="Microsoft.NET.Sdk">
  <PropertyGroup>
    <OutputType>WinExe</OutputType>
    <TargetFramework>net11.0-windows</TargetFramework>
    <Nullable>enable</Nullable>
    <ImplicitUsings>enable</ImplicitUsings>
    <UseWindowsForms>true</UseWindowsForms>
  </PropertyGroup>
  <ItemGroup>
    <ProjectReference Include="..\..\SharedModels\SharedModels.csproj" />
  </ItemGroup>
</Project>
```

---

## Async API Surface

- `AsyncValidationAttribute` — Abstract base for async validation attributes
- `IAsyncValidatableObject` — Interface for object-level async validation
- `Validator.TryValidateObjectAsync` — Async version of `TryValidateObject`
- `Validator.TryValidatePropertyAsync` — Async version of `TryValidateProperty`
- `Validator.TryValidateValueAsync` — Async version of `TryValidateValue`
- `Validator.ValidateObjectAsync` — Async version of `ValidateObject`
- `Validator.ValidatePropertyAsync` — Async version of `ValidateProperty`
- `Validator.ValidateValueAsync` — Async version of `ValidateValue`
