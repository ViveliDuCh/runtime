# Test Generation Research

## Project Overview

- **Requested workspace**: `R:\`
- **Physical path**: `C:\REPOS\github-worktrees\runtime\last-known-good-options-async-validation`
- **Feature**: opt-in asynchronous options reload validation for `Microsoft.Extensions.Options`
- **Language/runtime**: C# / .NET runtime repository
- **Source project**: `src\libraries\Microsoft.Extensions.Options\src\Microsoft.Extensions.Options.csproj`
- **Primary test project**: `src\libraries\Microsoft.Extensions.Options\tests\Microsoft.Extensions.Options.Tests\Microsoft.Extensions.Options.Tests.csproj`
- **Test framework**: xUnit through the runtime repository test infrastructure
- **Test TFMs**: `$(NetCoreAppCurrent);$(NetFrameworkCurrent)`
- **Research method**: static source/test inspection only. No build, test, formatting, staging, or commit was performed.

The existing `AsyncOptionsValidationTests.cs` suite is primarily a startup-validation
suite. It thoroughly exercises async startup creation, seeding, and several custom
service/cache cases, but it has no direct test of `ValidateOnChange`, the reload
coordinator, either reload failure behavior, error callbacks, or `OptionsEventSource`.

## Scope and Policy Source

The request refers to 16 required policy bullets, but neither the prompt nor a
literal 16-item list in the inspected repository provides their original numbering.
The canonical mapping below reconstructs the 16 distinct contracts from the XML
remarks on `ValidateOnChange` and `ValidateOnStart` in
`src\libraries\Microsoft.Extensions.Options\src\OptionsBuilderExtensions.cs:18-104`.
This is also the policy set reflected by the implementation.

## Public and Internal API Surface

### New public API

`OptionsBuilderExtensions.cs:62-84`:

```csharp
public static OptionsBuilder<TOptions> ValidateOnChange<
    [DynamicallyAccessedMembers(DynamicallyAccessedMemberTypes.PublicParameterlessConstructor)]
    TOptions>(
    this OptionsBuilder<TOptions> optionsBuilder,
    OptionsReloadValidationBehavior behavior = OptionsReloadValidationBehavior.KeepLastGood,
    Action<string?, Exception>? onError = null)
    where TOptions : class;
```

`OptionsReloadValidationBehavior.cs:7-17`:

```csharp
public enum OptionsReloadValidationBehavior
{
    KeepLastGood = 0,
    FailReads = 1,
}
```

### Related changed public contract

Although it is outside the seven files highlighted in the request, this change is
material to compatibility and test generation:

`IAsyncValidateOptions.cs:13-25` now declares:

```csharp
public interface IAsyncValidateOptions<TOptions> : IValidateOptions<TOptions>
    where TOptions : class
{
    Task<ValidateOptionsResult> ValidateAsync(
        string? name,
        TOptions options,
        CancellationToken cancellationToken = default);
}
```

Relative to the base branch, `TOptions` is no longer contravariant (`in TOptions`)
and implementers must also implement `IValidateOptions<TOptions>.Validate`.
Tests should treat source/API compatibility as a separate concern from runtime
behavior.

### Existing public APIs whose behavior changes

`OptionsMonitor<TOptions>` keeps its existing public signatures:

```csharp
public OptionsMonitor(
    IOptionsFactory<TOptions> factory,
    IEnumerable<IOptionsChangeTokenSource<TOptions>> sources,
    IOptionsMonitorCache<TOptions> cache);

public TOptions CurrentValue { get; }
public virtual TOptions Get(string? name);
public IDisposable? OnChange(Action<TOptions, string?> listener);
public void Dispose();
```

For opted-in names, a change now schedules asynchronous creation and validation
instead of synchronously evicting/recreating/notifying.

### New or materially changed internal APIs

- `OptionsReloadValidationRegistration<TOptions>` stores `Name`, `Behavior`, and
  `OnError` (`OptionsReloadValidation.cs:10-27`).
- `OptionsReloadValidation<TOptions>` builds the per-name registration map and
  exposes `TryGetRegistration` (`OptionsReloadValidation.cs:30-50`).
- `OptionsReloadValidationMarker<TOptions> : IValidateOptions<TOptions>` returns
  `ValidateOptionsResult.Skip`; it carries the registration into the built-in
  factory (`OptionsReloadValidation.cs:53-64`).
- `OptionsFactory<TOptions>.HasAsyncValidators`
- `OptionsFactory<TOptions>.ReloadValidation`
- `OptionsFactory<TOptions>.CreateAsync(string name, CancellationToken cancellationToken)`
  (`OptionsFactory.cs:63-65,93-113`).
- `OptionsMonitor<TOptions>.ValidateOnStartAsync(...)`
  (`OptionsMonitor.cs:125-209`).
- `OptionsCache<TOptions>.TryGetValue(...)`
- `OptionsCache<TOptions>.SetValidated(...)`
- `OptionsCache<TOptions>.SetException(...)`
- `OptionsCache<TOptions>.TryAddOrReplace(
  IOptionsMonitorCache<TOptions> cache, string? name, TOptions options)`
  (`OptionsCache.cs:75-153`).

### Diagnostics API

`OptionsEventSource.cs:9-31` is an internal singleton `EventSource` with:

```csharp
ReloadValidationFailed(
    string optionsType, string optionsName, string exceptionType, int behavior);
ReloadErrorCallbackFailed(
    string optionsType, string optionsName, string exceptionType);
ChangeListenerFailed(
    string optionsType, string optionsName, string exceptionType);
ReloadWorkerFailed(
    string optionsType, string optionsName, string exceptionType);
```

No existing Options test observes any of these events.

## Dependency Graph

### Registration and startup path

```text
OptionsBuilder<TOptions>
  -> OptionsBuilderExtensions.ValidateOnChange
     -> OptionsReloadValidationRegistration<TOptions> (one per registration)
     -> OptionsReloadValidation<TOptions> (last registration per name wins)
     -> OptionsReloadValidationMarker<TOptions> as IValidateOptions<TOptions>
     -> ValidateOnStart
        -> StartupValidatorOptions
        -> StartupValidator as IStartupValidator and IAsyncStartupValidator
```

### Creation and reload path

```text
IOptionsChangeTokenSource<TOptions>
  -> OptionsMonitor<TOptions>
     -> per-name ReloadState / ReloadCoordinator
     -> OptionsFactory<TOptions>.CreateAsync
        -> IConfigureOptions<TOptions>
        -> IPostConfigureOptions<TOptions>
        -> IValidateOptions<TOptions>
           -> IAsyncValidateOptions<TOptions>.ValidateAsync when supported
     -> OptionsCache<TOptions>.SetValidated or SetException
     -> OnChange listeners
     -> OptionsEventSource on caught failures
```

### Leaf types

- `OptionsReloadValidationBehavior`
- `OptionsReloadValidationRegistration<TOptions>`
- `OptionsReloadValidation<TOptions>`
- `OptionsReloadValidationMarker<TOptions>`
- `OptionsEventSource`
- `OptionsCache<TOptions>` as the atomic storage leaf, backed by
  `ConcurrentDictionary<string, Lazy<TOptions>>`

These are suitable for direct, low-mocking tests. The behavior enum itself should
normally be covered through end-to-end behavior theories rather than standalone
tests.

### Mid-layer types

- `OptionsFactory<TOptions>`: configuration plus sync/async validation dispatch
- `OptionsBuilderExtensions`: DI registration and startup wiring
- `UnnamedOptionsManager<TOptions>`: fixed `IOptions<TOptions>` startup slot

### Top-layer types

- `OptionsMonitor<TOptions>`: change tokens, per-name coordination, publication,
  callbacks, cancellation, and diagnostics
- `StartupValidator`: host startup orchestration

Testing should proceed leaf-first, then exercise the top layer through real
`ServiceCollection` registrations and controlled validators/change tokens. The
existing project does not use a mocking framework for these scenarios.

## Concurrency and Synchronization

### Production primitives

- `ConcurrentDictionary<string, Lazy<TOptions>>` in `OptionsCache<TOptions>`
  (`OptionsCache.cs:18`).
- Per-name `lock (state.SyncObj)` around generation, startup state, and worker
  state (`OptionsMonitor.cs:89-123,125-209,211-341`).
- Per-name `SemaphoreSlim StartupGate` to serialize startup validation.
- `CancellationTokenSource` owned by the reload coordinator, plus linked tokens
  for startup.
- `Volatile.Read`/`Volatile.Write` for `_disposed`.
- A generation counter and `WorkerRunning` flag implement latest-generation
  coalescing.
- Fire-and-forget `_ = ProcessReloadsAsync(...)`, with exceptions intended to be
  contained and reported through `OptionsEventSource`.
- `ExceptionDispatchInfo` in `OptionsCache.SetException` preserves the validation
  exception for `FailReads`.
- `UnnamedOptionsManager<TOptions>` uses a volatile value, lazy lock creation via
  `Interlocked.CompareExchange`, and a lock for one-time publication.

### Existing test primitives and helpers

In the primary Options tests:

- `FakeChangeToken.cs:9-30` provides explicit callback invocation.
- `OptionsMonitorTest.cs:490-525` uses `Barrier` and bounded waits in
  `InstantiatesOnlyOneOptionsInstance`.
- `AsyncOptionsValidationTests.cs` already uses `CancellationToken`,
  `CancellationTokenSource`, `Task`, and `ConcurrentDictionary`.
- Its custom cache helpers include `DelegatingOptionsCache<TOptions>` and
  `RaceInjectingOptionsCache<TOptions>` (`AsyncOptionsValidationTests.cs:730-751`).

Not currently used by `AsyncOptionsValidationTests.cs`: `TaskCompletionSource`,
`ManualResetEventSlim`, `SemaphoreSlim`, explicit `lock`, and `Interlocked`.

Useful established repository patterns:

- `TaskCompletionSource` with `RunContinuationsAsynchronously`:
  `src\libraries\System.IO.Pipelines\tests\Infrastructure\CancelledWritesStream.cs:12-14`
  and `WriteCheckStream.cs:17,42-52`.
- `ManualResetEventSlim` with bounded waits:
  `src\libraries\System.Threading.Channels\tests\RendezvousChannelTests.cs:152-211`.
- `SemaphoreSlim` with timed waits:
  `src\libraries\Microsoft.Extensions.Caching.Memory\tests\CapacityTests.cs:94-148`.

For reload race tests, use a controlled async validator with
`TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously)` for
each generation, an `Interlocked` invocation count, and bounded waits. Avoid
`Task.Delay` as the synchronization mechanism.

## The 16-Policy Coverage and Risk Map

Legend: **Direct** means the asserted API/path is the policy itself; **Indirect**
means a prerequisite or analogous legacy behavior is covered, not the new reload
path.

| # | Required policy | Implementation evidence | Existing coverage | Missing coverage | Likely defect or risk |
|---|---|---|---|---|---|
| 1 | `ValidateOnChange` is opt-in and also enables startup validation. | `OptionsBuilderExtensions.cs:62-84` registers reload metadata and calls `ValidateOnStart`. | **Indirect**: the `ValidateOnStart_*` and async seed tests cover startup wiring. | No test calls `ValidateOnChange`; no registration-shape or end-to-end opt-in assertion. | If `IAsyncStartupValidator.ValidateAsync` is never run (for example, a non-host DI container), the opted-in state remains startup-gated and changes can be recorded without a worker ever starting. Verify whether this silent suppression is intended. |
| 2 | Names not opted in retain legacy synchronous reload behavior. | `OptionsMonitor.cs:79-123` falls back to cache removal, synchronous `Get`, and listener invocation when no reload state exists for the name. | **Indirect**: `OptionsMonitorTest.CanWatchNamedOptions`, `CanWatchOptions`, and multiple-source tests cover legacy behavior. | A mixed named-options test with one opted-in and one non-opted name is absent. Async-only validators on a non-opted name are also untested. | A globally registered async-only validator can make the legacy synchronous path throw for a non-opted name. The interaction needs an explicit contract test. |
| 3 | Reloads coalesce to one worker per options name. | `OptionsMonitor.cs:89-123,211-341`; `WorkerRunning` and generation are protected by the per-name lock. | **None**. | No blocked-validator burst, per-name parallelism, or worker-count test. | A slow validator, `onError`, or change listener serially stalls later generations for that name. This may be deliberate, but must be bounded and documented by tests. |
| 4 | Only the latest observed generation may publish. | Generation is rechecked after async creation and before publication in `OptionsMonitor.cs:211-341`. | **None**. | No out-of-order completion, superseded success, or superseded failure test. | There is a narrow error-reporting race: a generation can become superseded after the locked current-generation check/behavior application but before `onError` runs outside the lock. The callback can therefore observe a failure that is superseded by callback time. |
| 5 | A validator implementing both interfaces is dispatched asynchronously. | `OptionsFactory.CreateAsync` checks `IAsyncValidateOptions<TOptions>` first (`OptionsFactory.cs:93-113`). | **Direct for startup**: `StartupValidator_ValidatorImplementingBoth_DispatchesToAsync`; `ValidateWithValidatorType_PreservesAsyncCapability`. | No reload-path dispatch test; no direct test for every dependency overload or named validator-type filter. | The changed inheritance/variance of `IAsyncValidateOptions<TOptions>` is a source/API compatibility risk independent of dispatch correctness. |
| 6 | Only successfully validated reload values are published and notified. | Success calls `SetValidated` and then notifies; failure keeps the old value or publishes an exception (`OptionsMonitor.cs:211-392`). | **Indirect**: exact-candidate and failed-startup seed tests cover startup publication. | No failed reload/cache/listener assertion for either behavior. | A throwing multicast listener is caught as one invocation; normal delegate semantics stop later listeners after the first throw. This can violate an expectation that all registered listeners are isolated. |
| 7 | Async reload validation requires the exact built-in monitor, factory, and cache. | Exact runtime-type checks in `OptionsMonitor.cs:39-47` and startup registration checks in `OptionsBuilderExtensions.cs:137-163`. | **Indirect/startup**: custom `IOptions<T>`, derived factory, and custom/derived cache tests. | No `ValidateOnChange` startup failure test replacing each of monitor, factory, and cache independently. | Exact-type checks intentionally reject otherwise-correct decorators/subclasses. The failure should be early, deterministic, and name/type-specific. |
| 8 | `IOptions<T>.Value` remains fixed to its startup/singleton winner. | `UnnamedOptionsManager<TOptions>` is seeded once and reload does not mutate it. | **Direct**: `AsyncValidatedOptions_IOptionsValue_ThrowsBeforeStartupAndServesSeededValueAfter`, `BothCapableValidator_PreStartIOptionsValueRemainsWinnerAndAsyncValidationRuns`, `IOptionsValue_RemainsStableAfterMonitorCacheEviction`. | No successful/failed `ValidateOnChange` reload proves the value stays fixed while monitor changes. | A successfully synchronously created pre-start value can own the singleton slot even when a distinct candidate later passes async startup validation. This is documented behavior but can surprise callers. |
| 9 | `IOptionsSnapshot<T>` remains scoped and synchronous. | Snapshot resolves through scoped `OptionsManager<TOptions>` and synchronous factory creation. | **Direct**: `AsyncOnlyValidation_IOptionsSnapshotRemainsUnsupportedAfterStartup`; nearby `OptionsSnapshotTest` covers scope caching/change behavior. | No mixed sync+async validator or named snapshot case after monitor reload. | Async-only validation makes snapshot access fail synchronously by design. The exception type/message is not directly pinned for all paths. |
| 10 | `IOptionsMonitor<T>` receives only successful validated reloads. | `OptionsMonitor.cs:211-392` atomically publishes then notifies. | **None for reload**; legacy monitor change tests are only analogous. | No successful reload, repeated reload, named reload, callback payload, or cache identity test. | The startup-gate issue in policy 1 can leave a monitor permanently stale outside normal host startup. Public cache removal can also eliminate the “last good” value before a failed reload. |
| 11 | Repeated `ValidateOnChange` for the same type/name is last-registration-wins for behavior and callback. | Dictionary assignment overwrites by name in `OptionsReloadValidation.cs:38-41`. | **None**. | Must verify both the final behavior and final callback; also verify registrations for different names do not overwrite each other. | Correctness depends on DI enumeration order. A test should pin the intended registration ordering contract. |
| 12 | `onError` runs once for a failed current generation, after `KeepLastGood`/`FailReads` has been applied. | Failure behavior is applied under the state lock, then reporting occurs outside it (`OptionsMonitor.cs:211-371`). | **None**. | No callback count, callback-time read, exception identity, behavior theory, reentrancy, or null-callback test. | The supersession race described in policy 4 can make “current generation” false by callback time. A blocking callback also delays newer generations because it runs inline on the sole per-name worker. |
| 13 | Superseded failures and disposal cancellation do not invoke `onError`. | Generation/disposal checks surround async creation and failure handling (`OptionsMonitor.cs:211-341`). | **None**. | No superseded-failure, cancellation-honoring validator, cancellation-ignoring validator, or dispose-while-blocked test. | The post-lock callback race can report a newly superseded failure. `Dispose` cancels but does not dispose the coordinator `CancellationTokenSource` or per-name `SemaphoreSlim`, leaving a small resource leak. |
| 14 | Exceptions thrown by `onError` are contained and reported diagnostically, not propagated. | `ReportReloadFailure` catches callback exceptions and emits `ReloadErrorCallbackFailed` (`OptionsMonitor.cs:345-371`). | **None**. | No throwing callback test and no assertion for any of the four EventSource events/payloads. | Diagnostics currently expose type/name/exception type/behavior, but no message or stack. The worker is fire-and-forget, so EventSource coverage is essential to detect swallowed failures. |
| 15 | Async startup creates and publishes the exact validated candidate into built-in `IOptions`/monitor state. | `OptionsMonitor.ValidateOnStartAsync` plus `OptionsFactory.CreateAsync` and `OptionsCache.SetValidated` (`OptionsMonitor.cs:125-209`). | **Direct, strong**: `AsyncOnlyValidation_StartupFirstSeedsExactInstance`, `NamedAsyncOptions_StartupPublishesExactCandidate`, poisoned-cache replacement, failed-startup-no-seed, and custom-cache exact-candidate tests. | No startup/change race and no change arriving just before startup completion. | The documented pre-start singleton winner can differ from the async-validated candidate. A startup/change race could expose regressions unless generation and cache identity are explicitly tested. |
| 16 | Custom/derived startup services follow explicit fallback/failure rules; async reload itself rejects non-built-ins. | `OptionsBuilderExtensions.cs:96-163`; `OptionsCache.TryAddOrReplace` uses a bounded custom-cache fallback (`OptionsCache.cs:127-153`). | **Direct for startup**: `AsyncStartupValidation_CustomOptionsImplementation_ThrowsInvalidOperationException`, `DerivedFactoryUsesSynchronousFallback`, custom cache publication/retry/rejection tests. | No reload-validation rejection matrix and no highly contended custom cache case. | The custom-cache fallback has an arbitrary three-attempt bound and can fail spuriously under contention. Derived-factory synchronous fallback can bypass true async validation; both appear deliberate but need contract tests. |

## Likely Production Defects and Contract Risks

### Highest priority

1. **Potentially breaking `IAsyncValidateOptions<TOptions>` API change**  
   The loss of contravariance and new `IValidateOptions<TOptions>` base interface can
   break existing implementations and assignments. This needs API compatibility
   review in addition to unit tests.

2. **Opted-in reload can remain disabled when async startup is not executed**  
   `TryScheduleReload` suppresses the legacy path for an opted-in name, while the
   worker is gated on `StartupValidated`. In a DI-only/non-host scenario that never
   invokes `IAsyncStartupValidator`, changes can accumulate without reload or an
   actionable exception.

3. **Superseded failure callback race**  
   Current-generation status is checked under the state lock, but `onError` is
   invoked later outside the lock. A concurrent change can advance the generation
   in that gap, contrary to a strict reading of “superseded failures do not invoke
   the callback.”

### Medium priority

4. **One throwing change listener prevents later listeners from running**  
   Catching around a multicast delegate invocation contains the exception but does
   not isolate each subscriber. Later callbacks are skipped.

5. **User callbacks can stall the sole per-name worker**  
   `onError` and normal change listeners execute inline. A blocked callback prevents
   processing newer generations for that name.

6. **`KeepLastGood` assumes the good cache entry still exists**  
   `IOptionsMonitorCache<TOptions>` is public and can be cleared/removed. A failed
   reload cannot keep a value that another caller has evicted.

### Lower priority or deliberate tradeoffs

7. `Dispose` cancels but does not dispose coordinator/token/gate resources.
8. Custom-cache replacement stops after three attempts under contention.
9. EventSource payloads omit exception message/stack details.
10. Exact-type checks reject decorators and derived implementations even when they
    could satisfy the atomicity contract.

These findings are static hypotheses until pinned by deterministic tests. The API
compatibility change and absence of direct reload tests are confirmed facts.

## Existing Tests and Estimated Coverage

### `AsyncOptionsValidationTests.cs`

Existing coverage groups include:

- Async validator filtering:
  - `AsyncValidateOptions_SkipsWhenNameDoesNotMatch`
  - `AsyncValidateOptions_ValidatesWhenNameMatches`
  - `AsyncValidateOptions_ValidatesAllWhenNameIsNull`
  - `AsyncValidateOptions_NameMatching_DefaultAndNamed`
- Builder/factory dispatch:
  - `OptionsBuilder_AsyncValidate_RegistersAndExecutes`
  - `ValidateWithValidatorType_PreservesAsyncCapability`
  - `StartupValidator_ValidatorImplementingBoth_DispatchesToAsync`
- Startup orchestration:
  - `StartupValidator_SinglePath_RunsBothSyncAndAsyncValidators`
  - `StartupValidator_SinglePath_AggregatesSyncAndAsyncFailures`
  - `StartupValidator_ValidateAsync_OnlyAsyncValidators`
  - `StartupValidator_ValidateAsync_AsyncFailureThrowsOptionsValidationException`
  - `StartupValidator_ValidateAsync_CancellationTokenPropagated`
  - `StartupValidator_ValidateAsync_MultipleFailures_ThrowsAggregateException`
- Registration/custom service behavior:
  - `ValidateOnStart_CustomSyncOnlyValidator_UsesSyncPath`
  - `ValidateOnStart_RegistersBuiltInValidatorAsBothInterfaces`
  - `ValidateOnStart_CalledMultipleTimes_RegistersSingleAsyncStartupValidator`
  - `ValidateOnStart_CustomAsyncStartupValidator_CoexistsWithBuiltInInEnumerable`
  - custom `IOptions`, derived factory, custom/derived cache, retry, and rejecting
    cache tests
- Startup seeding/accessor behavior:
  - pre-start `IOptions<T>` failure and post-start seed
  - poisoned-cache replacement
  - fixed `IOptions<T>` identity
  - synchronous snapshot behavior
  - exact startup candidate publication
  - named publication
  - failed-startup-no-seed

There is no direct `ValidateOnChange` test in this file or the nearby Options test
project.

### Nearby baseline tests

- `OptionsMonitorTest.cs`
  - `CanWatchNamedOptions`
  - `CanWatchOptions`
  - `CanWatchOptionsWithMultipleSourcesAndCallbacks`
  - `CanWatchOptionsWithMultipleSources`
  - `CanMonitorConfigBoundOptions`
  - `CanMonitorConfigBoundNamedOptions`
  - `DisposingOptionsMonitorDisposesChangeTokenRegistrations`
  - `InstantiatesOnlyOneOptionsInstance`
- `OptionsSnapshotTest.cs`
  - `SnapshotDoesNotChangeUntilNextRequestOnConfigChanges`
  - `SnapshotOptionsAreCachedPerScope`
- `OptionsValidationTests.cs`
  - sync `ValidateOnStart` called/not-called/multiple-registration baselines
- `OptionsBuilderTest.cs`
  - singleton/transient dependency injection
  - service-provider configuration
  - custom/default validation error registration

### Qualitative coverage estimate

These estimates are based on path/scenario inspection, not instrumented line
coverage:

| File or feature | Estimated preexisting coverage | Assessment |
|---|---:|---|
| `OptionsBuilderExtensions.ValidateOnChange` | 0-10% | No direct call; only the reused `ValidateOnStart` portion is covered. |
| `OptionsMonitor` legacy path | 70-85% | Mature nearby tests cover normal watching, names, sources, callbacks, and disposal. |
| `OptionsMonitor` async startup seed path | 65-80% | Strong startup identity/failure/custom-service tests. |
| `OptionsMonitor` async reload coordinator/worker | 0-5% | No direct scheduling, generation, behavior, callback, or disposal test. |
| `OptionsCache.SetValidated` | 50-70% | Indirect startup and exact-candidate coverage. |
| `OptionsCache.SetException` | 0% | `FailReads` is untested. |
| `OptionsCache.TryAddOrReplace` | 70-85% | Success, injected race/retry, and rejection are covered. |
| `OptionsFactory.CreateAsync` | 70-85% | Sync+async dispatch, aggregation, cancellation, and failure are covered. |
| `OptionsReloadValidation<TOptions>` / marker | 0-10% | No direct registration/lookup/last-wins test. |
| `OptionsReloadValidationBehavior` | 0% behavior coverage | Neither enum value is exercised on reload. |
| `OptionsEventSource` | 0% | No listener or payload assertions. |
| `IAsyncValidateOptions` / base async validator | 55-70% | Core filtering/dispatch covered; compatibility and dependency overload family are incomplete. |

**Overall MVP estimate**: approximately 25-35% behavioral coverage. Startup
infrastructure is comparatively strong; the actual asynchronous reload MVP is
effectively untested.

## Files to Test

### High Priority

| File | Classes/functions | Testability | Estimated coverage | Notes |
|---|---|---|---:|---|
| `src\libraries\Microsoft.Extensions.Options\src\OptionsMonitor.cs` | reload coordinator, startup gate, worker, failure reporting, listener notification, disposal | Medium | 0-5% for new reload path | Core concurrency and policy implementation. Use controlled real validators and change tokens. |
| `src\libraries\Microsoft.Extensions.Options\src\OptionsBuilderExtensions.cs` | `ValidateOnChange` registration and built-in service checks | High | 0-10% for new API | Test through `ServiceCollection`; include named and duplicate registrations. |
| `src\libraries\Microsoft.Extensions.Options\src\OptionsCache.cs` | `SetValidated`, `SetException`, atomic replacement | High | Mixed; `SetException` 0% | Leaf-level identity and exception replay tests plus monitor integration. |
| `src\libraries\Microsoft.Extensions.Options\src\OptionsReloadValidation.cs` | registration map, marker, last-wins | High | 0-10% | Direct DI resolution and end-to-end duplicate-registration assertions. |
| `src\libraries\Microsoft.Extensions.Options\src\OptionsEventSource.cs` | four diagnostic events | High | 0% | Use an in-process `EventListener`; assert event identity and payload. |

### Medium Priority

| File | Classes/functions | Testability | Estimated coverage | Notes |
|---|---|---|---:|---|
| `src\libraries\Microsoft.Extensions.Options\src\OptionsFactory.cs` | `CreateAsync`, async capability detection, marker capture | High | 70-85% | Add reload-path dual-interface and named dispatch tests. |
| `src\libraries\Microsoft.Extensions.Options\src\OptionsReloadValidationBehavior.cs` | `KeepLastGood`, `FailReads` | High through integration | 0% | Cover both values in a shared reload-failure theory. |
| `src\libraries\Microsoft.Extensions.Options\src\IAsyncValidateOptions.cs` | inheritance/variance contract | Medium | Not a runtime percentage | API-compat review; compile-time/API baseline validation. |
| `src\libraries\Microsoft.Extensions.Options\src\AsyncValidateOptions.cs` | sync fail-fast path and async filtering | High | 55-70% | Pin sync exception and dependency-overload behavior. |

### Low Priority / Skip

| File | Reason |
|---|---|
| Generated reference/API files, if regenerated later | Validate through the repository API tooling rather than hand-authored unit tests. |
| Enum numeric values in isolation | Exercise them through `KeepLastGood`/`FailReads` integration and EventSource payload tests. |

## Existing Test Projects

### Core xUnit project

- **Project file**:
  `src\libraries\Microsoft.Extensions.Options\tests\Microsoft.Extensions.Options.Tests\Microsoft.Extensions.Options.Tests.csproj`
- **Target source**: `Microsoft.Extensions.Options`
- **TFMs**: `$(NetCoreAppCurrent);$(NetFrameworkCurrent)`
- **Relevant references**: Configuration, DependencyInjection, Hosting,
  Options.ConfigurationExtensions, and Options.DataAnnotations projects.
- **Relevant files**:
  `AsyncOptionsValidationTests.cs`, `OptionsMonitorTest.cs`,
  `OptionsBuilderTest.cs`, `OptionsSnapshotTest.cs`,
  `OptionsValidationTests.cs`, `OptionsTest.cs`, `FakeChangeToken.cs`,
  and `FakeOptionsFactory.cs`.

### Other projects in `Microsoft.Extensions.Options.slnx`

- `tests\SourceGeneration.Unit.Tests\Microsoft.Extensions.Options.SourceGeneration.Unit.Tests.csproj`
- `tests\SourceGenerationTests\Microsoft.Extensions.Options.SourceGeneration.Tests.csproj`

### Additional project outside that `.slnx`

- `tests\TrimmingTests\Microsoft.Extensions.Options.TrimmingTests.proj`

The directory-level final validation target is preferable to the `.slnx` alone
because it also reaches the trimming test project.

## Test Conventions and Reusable Patterns

### xUnit conventions

- Plain `[Fact]`, `[Theory]`, and `[InlineData]`.
- Test names describe scenario and expected result.
- Real `ServiceCollection`/`BuildServiceProvider` setup.
- Small nested fake validator, cache, options, and startup-validator types.
- Direct xUnit assertions; no mocking library in the inspected tests.
- Explicit named/default options cases.

### DI and custom-registration patterns

- `OptionsBuilderTest.ConfigureOptionsWithSingletonDepWillUpdate`
- `OptionsBuilderTest.ConfigureOptionsWithTransientDep`
- `OptionsBuilderTest.PostConfigureOptionsWithTransientDep`
- `OptionsBuilderTest.CanConfigureWithServiceProvider`
- `AsyncOptionsValidationTests.AsyncStartupValidation_CustomCachePublishesExactCandidate`
- `AsyncOptionsValidationTests.AsyncValidatedOptions_ValidateOnStart_DerivedCacheReplace_RetriesPastConcurrentInsert`

New tests should follow these patterns by replacing registrations in a
`ServiceCollection`, rather than mocking `IServiceProvider`.

### EventSource patterns

No EventSource test helper exists in the Options test project. Closest established
runtime patterns are:

- `src\libraries\Microsoft.Extensions.Logging.EventSource\tests\EventSourceLoggerTest.cs:692-722,792-879`
  - `Logs_AsExpected_AfterSettingsReload`
  - `PreEnableListener`
  - nested `TestEventListener`
- `src\libraries\Microsoft.Extensions.DependencyInjection\tests\DI.Tests\DependencyInjectionEventSourceTests.cs:332-410`
  - `EmitsServiceProviderBuiltOnAttach`
  - `TestEventListenerFixture`
  - nested `TestEventListener`

The Options tests should use an in-process `EventListener`, enable the source
before triggering the failure, collect `EventWrittenEventArgs` thread-safely, use
a bounded signal rather than a sleep, and dispose the listener. Pre-enabling is
important because the EventSource is a static singleton.

## Build and Test Commands

All commands below are written for a prompt whose current directory is `R:\`.
They are documented targets only and were not executed during this research.

### One-time/bootstrap prerequisite when the enlistment lacks runtime/testhost outputs

```powershell
.\build.cmd -subset clr+libs -rc Release
```

This prerequisite is not necessary on an already bootstrapped enlistment.

### Focused library solution build

```powershell
.\build.cmd -projects src\libraries\Microsoft.Extensions.Options\Microsoft.Extensions.Options.slnx
```

The `.slnx` includes the source/dependency graph and these three core test projects:

1. `Microsoft.Extensions.Options.Tests`
2. `Microsoft.Extensions.Options.SourceGeneration.Unit.Tests`
3. `Microsoft.Extensions.Options.SourceGeneration.Tests`

It does not include `tests\TrimmingTests\Microsoft.Extensions.Options.TrimmingTests.proj`.

### Focused core test project

The runtime repository's library test driver is `dotnet build /t:Test`, not
`dotnet test`:

```powershell
.\dotnet.cmd build .\src\libraries\Microsoft.Extensions.Options\tests\Microsoft.Extensions.Options.Tests\Microsoft.Extensions.Options.Tests.csproj /t:Test
```

### Other test projects, when validating them individually

```powershell
.\dotnet.cmd build .\src\libraries\Microsoft.Extensions.Options\tests\SourceGeneration.Unit.Tests\Microsoft.Extensions.Options.SourceGeneration.Unit.Tests.csproj /t:Test
.\dotnet.cmd build .\src\libraries\Microsoft.Extensions.Options\tests\SourceGenerationTests\Microsoft.Extensions.Options.SourceGeneration.Tests.csproj /t:Test
.\dotnet.cmd build .\src\libraries\Microsoft.Extensions.Options\tests\TrimmingTests\Microsoft.Extensions.Options.TrimmingTests.proj /t:Test
```

### Appropriate final full-workspace validation

```powershell
.\build.cmd -projects src\libraries\Microsoft.Extensions.Options -test
```

The project-directory target is recursive and covers the whole
`Microsoft.Extensions.Options` library workspace, including its trimming test.
It is the appropriate final target for this change. Do **not** substitute a
repository-wide `build.cmd` or all-libraries test sweep.

### Formatting/lint

There is no separate library lint target; analyzers are build-integrated.
The repository formatting script is staged-file-oriented:

```powershell
sh .\eng\formatting\format.sh
```

It is not a substitute for the focused build/test validation above.

## Recommended Test Generation Order

1. Add a minimal successful `ValidateOnChange` test for default and named options.
2. Add a theory for `KeepLastGood` and `FailReads`, including callback-time reads.
3. Add deterministic generation/coalescing tests using gated async validators.
4. Add superseded success/failure and startup/change race tests.
5. Add duplicate-registration and mixed opted/non-opted-name tests.
6. Add callback exception, listener exception, cancellation, and disposal tests.
7. Add EventSource tests for all four events and their payloads.
8. Add the exact built-in service rejection matrix.
9. Add API/contract tests or API-review coverage for the changed
   `IAsyncValidateOptions<TOptions>` declaration.

The first six groups should be added to `AsyncOptionsValidationTests.cs` or a
focused new `OptionsReloadValidationTests.cs`, following the existing xUnit and
real-DI conventions. EventSource tests can share a small nested listener modeled
on the Logging.EventSource or DependencyInjection tests.

## Blockers and Caveats

- There is no measured coverage report; percentages are static estimates.
- There is no literal local source for the original numbering of the 16 bullets,
  so the mapping uses the complete 16-contract reconstruction from the public XML
  remarks and implementation.
- Race findings require deterministic tests before being treated as confirmed
  runtime failures.
- No production or test source was changed during research.
