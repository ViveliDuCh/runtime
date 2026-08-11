# Test Implementation Plan

## Overview

Use a **targeted** strategy: existing asynchronous startup-validation coverage is
strong, while the opt-in reload coordinator is effectively untested. Add
runtime-convention xUnit coverage to the existing
`AsyncOptionsValidationTests` class, exercising the feature through real
`ServiceCollection` registrations, the built-in options services, and controlled
change tokens.

- **Primary source files covered**:
  - `src\libraries\Microsoft.Extensions.Options\src\OptionsBuilderExtensions.cs`
  - `src\libraries\Microsoft.Extensions.Options\src\OptionsMonitor.cs`
  - `src\libraries\Microsoft.Extensions.Options\src\OptionsFactory.cs`
  - `src\libraries\Microsoft.Extensions.Options\src\OptionsCache.cs`
  - `src\libraries\Microsoft.Extensions.Options\src\OptionsReloadValidation.cs`
  - `src\libraries\Microsoft.Extensions.Options\src\OptionsEventSource.cs`
  - `src\libraries\Microsoft.Extensions.Options\src\IAsyncValidateOptions.cs`
- **Existing test project**:
  `src\libraries\Microsoft.Extensions.Options\tests\Microsoft.Extensions.Options.Tests\Microsoft.Extensions.Options.Tests.csproj`
- **Test file**:
  `src\libraries\Microsoft.Extensions.Options\tests\Microsoft.Extensions.Options.Tests\AsyncOptionsValidationTests.cs`
- **Test class**: `Microsoft.Extensions.Options.Tests.AsyncOptionsValidationTests`

No new test project or dedicated test file is warranted. Keeping the event-source
tests in this class also serializes them with the other tests in the class and
reduces interference around the static `OptionsEventSource.Log` singleton.
Preserve every existing test unchanged; additions should be new facts/theories,
small nested helpers, and required `using` directives only.

No production change is planned up front. A production edit is permitted only
when a deterministic new regression test demonstrates that the implementation
violates one of the MVP policies; see **Conditional Production-Fix Rules**.

## Commands

All commands are documentation for the implementation phase and must be run from
the `R:\` worktree. They were not run while creating this plan.

### Optional bootstrap

Only if runtime/testhost outputs are absent:

```powershell
& R:\build.cmd -subset clr+libs -rc Release
```

### Gate after every phase

Build the one existing test project:

```powershell
& R:\.dotnet\dotnet.exe build R:\src\libraries\Microsoft.Extensions.Options\tests\Microsoft.Extensions.Options.Tests\Microsoft.Extensions.Options.Tests.csproj
```

Run the complete existing-plus-new `AsyncOptionsValidationTests` class through
the runtime repository's `/t:Test` driver:

```powershell
& R:\.dotnet\dotnet.exe build R:\src\libraries\Microsoft.Extensions.Options\tests\Microsoft.Extensions.Options.Tests\Microsoft.Extensions.Options.Tests.csproj /t:Test /p:TestFilter="FullyQualifiedName~Microsoft.Extensions.Options.Tests.AsyncOptionsValidationTests"
```

Do not substitute `dotnet test`; this repository's library test convention is
`dotnet build /t:Test`. Running the whole class after each phase protects the
existing startup tests from regressions.

### Final focused project validation

```powershell
& R:\.dotnet\dotnet.exe build R:\src\libraries\Microsoft.Extensions.Options\tests\Microsoft.Extensions.Options.Tests\Microsoft.Extensions.Options.Tests.csproj --no-incremental
& R:\.dotnet\dotnet.exe build R:\src\libraries\Microsoft.Extensions.Options\tests\Microsoft.Extensions.Options.Tests\Microsoft.Extensions.Options.Tests.csproj /t:Test
```

### Final full-library validation

Run from `R:\`:

```powershell
.\build.cmd -projects src\libraries\Microsoft.Extensions.Options -test
```

This recursive library target is the required final validation. It covers the
core, source-generation, and trimming tests; the nearby `.slnx` alone omits the
trimming project.

### Lint/format

There is no separate library lint target; analyzers are build-integrated. Do not
stage files merely to use the staged-file-oriented formatting script. Match the
existing file style manually and let the scoped/final builds enforce analyzers.

## Deterministic Test Infrastructure

Add these as private nested helpers in `AsyncOptionsValidationTests`; reuse the
existing `FakeOptions` and existing startup helpers.

1. **`ReloadChangeTokenSource<TOptions>`**
   - Implements `IOptionsChangeTokenSource<TOptions>`.
   - Carries an explicit name.
   - Rotates to a fresh `FakeChangeToken` under a lock *before* invoking the old
     token, so `ChangeToken.OnChange` deterministically subscribes to the next
     token and repeated/burst signals are reliable.
   - `Trigger()` is the only way tests advance a generation.

2. **`ControlledAsyncValidator` and `ValidationInvocation`**
   - Implements both `IValidateOptions<FakeOptions>` and
     `IAsyncValidateOptions<FakeOptions>`.
   - Enqueues each async invocation in a `ConcurrentQueue` and releases a
     `SemaphoreSlim` when it has entered.
   - Each invocation records `Name`, the exact candidate instance,
     `CancellationToken`, and has a
     `TaskCompletionSource<ValidateOptionsResult>` created with
     `TaskCreationOptions.RunContinuationsAsynchronously`.
   - Tests explicitly complete an invocation with success, failure, cancellation,
     or an exception. Track current and maximum active invocations using
     `Interlocked`.
   - Provide separate modes that honor cancellation and intentionally ignore it.

3. **Bounded wait helpers**
   - Use `SemaphoreSlim.Wait(TimeSpan)` and
     `ManualResetEventSlim.Wait(TimeSpan)` with one shared 30-second watchdog.
   - Timeouts only fail a hung test; they never decide test ordering.
   - Never use `Thread.Sleep`, `Task.Delay`, `SpinWait`, polling loops, or an
     unbounded `Wait`.

4. **Candidate/callback tracking**
   - Assign each configured `FakeOptions` a unique value using
     `Interlocked.Increment`.
   - Capture exact candidates from `ValidationInvocation.Options`.
   - Use `Interlocked` counters and bounded signals for listeners and `onError`;
     never infer completion from elapsed time.

5. **`TestOptionsEventListener`**
   - Derives from `EventListener`, enabling only
     `Microsoft-Extensions-Options`.
   - Copies event ID, level, and payload into a thread-safe queue and signals
     matching events with `ManualResetEventSlim`.
   - Supports an optional bounded blocker for event ID 1, needed to place a
     generation change deterministically between failure behavior application and
     `onError`.
   - Filters by a unique options name, enables an already-created source in
     `OnEventSourceCreated`, and is disposed by every test.

Representative synchronization pattern:

```csharp
ValidationInvocation reload = validator.TakeNextInvocation(TestTimeout);
Assert.Equal(Options.DefaultName, reload.Name);
reload.Complete(ValidateOptionsResult.Success);
Assert.True(listenerCalled.Wait(TestTimeout));
```

## Phase Summary

| Phase | Focus | New test methods | Estimated executions |
|---|---|---:|---:|
| 1 | Public contract, opt-in startup, basic successful reload | 6 | 6 |
| 2 | Failure behaviors and accessor contracts | 4 | 6 |
| 3 | Names, dispatch, overloads, and registration precedence | 7 | 7 |
| 4 | Coalescing, generations, and startup/change races | 5 | 5 |
| 5 | Disposal and all diagnostics | 6 | 7 |
| 6 | Exact built-in service rejection matrix | 2 | 7 |
| 7 | Coverage-gap review, complete validation, cleanup | 0 | 0 |
| **Total** |  | **30** | **38** |

---

## Phase 1: Public Contract and Successful Reload Foundation

### Overview

Establish public argument behavior, pin the changed async-validator type contract,
prove that `ValidateOnChange` also wires startup validation, and cover successful
default/named publication before introducing failures or races.

### Files to Test

- **Sources**:
  `OptionsBuilderExtensions.cs`, `IAsyncValidateOptions.cs`,
  `OptionsReloadValidation.cs`, `OptionsFactory.cs`, `OptionsMonitor.cs`,
  `OptionsCache.cs`
- **Test file**: `AsyncOptionsValidationTests.cs`

### Exact Tests and Assertions

1. **`ValidateOnChange_NullBuilder_ThrowsArgumentNullException`**
   - Invoke `OptionsBuilderExtensions.ValidateOnChange<FakeOptions>(null!)`.
   - Assert `ArgumentNullException`.
   - Assert `ParamName == "optionsBuilder"`.

2. **`ValidateOnChange_UndefinedBehavior_ThrowsArgumentOutOfRangeException`**
   - Call `ValidateOnChange((OptionsReloadValidationBehavior)42)` on a real
     builder.
   - Assert `ArgumentOutOfRangeException`.
   - Assert `ParamName == "behavior"`.
   - Assert no reload registration was added after the rejected call.

3. **`IAsyncValidateOptions_Contract_InheritsIValidateOptionsAndIsInvariant`**
   - Assert `typeof(IAsyncValidateOptions<FakeOptions>).GetInterfaces()` contains
     `typeof(IValidateOptions<FakeOptions>)`.
   - Inspect the generic parameter variance mask on
     `typeof(IAsyncValidateOptions<>)` and assert it is
     `GenericParameterAttributes.None`, not contravariant.

4. **`ValidateOnChange_EnablesAsyncStartupValidationAndSeedsExactCandidate`**
   - Register an async validator and call `ValidateOnChange` without separately
     calling `ValidateOnStart`.
   - Assert one built-in `IAsyncStartupValidator` is resolvable.
   - Run startup validation and assert the async validator ran exactly once.
   - Assert the exact candidate observed by the validator is the same instance
     returned by both `IOptions<FakeOptions>.Value` and
     `IOptionsMonitor<FakeOptions>.CurrentValue`.

5. **`ValidateOnChange_SuccessfulDefaultReload_PublishesExactCandidateAndNotifiesOnce`**
   - Complete startup, retain the startup candidate, register one monitor
     listener, trigger the default token, and explicitly release the reload
     validation.
   - Assert the trigger itself does not require the validator to complete.
   - Assert one listener call with `Options.DefaultName`.
   - Assert the listener value and `monitor.CurrentValue` are the exact reload
     candidate.
   - Assert the reload candidate differs from the startup candidate.
   - Assert `IOptions<FakeOptions>.Value` is still the exact startup candidate.
   - Assert two async validations total: startup plus reload.

6. **`ValidateOnChange_SuccessfulNamedReload_UpdatesOnlyMatchingName`**
   - Opt in `"watched"`, cache a distinct `"other"` value, and trigger only the
     `"watched"` token.
   - Assert the callback name is exactly `"watched"` and fires once.
   - Assert `monitor.Get("watched")` is the exact validated reload candidate.
   - Assert `monitor.Get("other")` remains the same cached instance and no
     callback reports `"other"`.

### Success Criteria

- [ ] Six tests are additive; no existing test is renamed or weakened.
- [ ] Exact candidate identity is asserted with `Assert.Same`, not only property
      equality.
- [ ] The standard scoped build and class-filtered test gate passes.

---

## Phase 2: Failure Behavior and Accessor Contracts

### Overview

Cover both `OptionsReloadValidationBehavior` values, callback ordering and
exception identity, fixed `IOptions`, synchronous scoped snapshots, and recovery
from a cached `FailReads` exception.

### Files to Test

- **Sources**: `OptionsMonitor.cs`, `OptionsCache.cs`,
  `OptionsReloadValidationBehavior.cs`
- **Test file**: `AsyncOptionsValidationTests.cs`

### Exact Tests and Assertions

1. **`ValidateOnChange_FailedCurrentReload_AppliesBehaviorBeforeInvokingOnError`**
   (`[Theory]` for `KeepLastGood` and `FailReads`)
   - Seed a successful startup value, fail the next current generation with the
     message `"reload failed"`, and perform a reentrant monitor read inside
     `onError`.
   - For both rows assert:
     - `onError` runs exactly once with `Options.DefaultName`.
     - The callback receives an `OptionsValidationException` whose `OptionsType`
       is `typeof(FakeOptions)`, whose `OptionsName` is the default name, and
       whose single failure is `"reload failed"`.
     - No `OnChange` listener runs.
   - For `KeepLastGood`, assert the callback-time read and subsequent read are the
     exact startup instance.
   - For `FailReads`, assert callback-time and subsequent reads throw the exact
     same exception instance passed to `onError` (`Assert.Same`), proving
     `SetException` occurred before the callback.

2. **`ValidateOnChange_IOptionsValue_RemainsStartupWinnerAcrossSuccessfulAndFailedReloads`**
   - Use `FailReads`; retain `IOptions.Value` after startup.
   - Complete one successful reload and assert the monitor changes to that exact
     candidate while `IOptions.Value` remains the startup instance.
   - Fail the next reload and wait for `onError`.
   - Assert monitor reads now throw, listener count remains one, and
     `IOptions.Value` still returns the exact startup instance without throwing.

3. **`ValidateOnChange_IOptionsSnapshot_RemainsScopeLocalAndSynchronousAfterMonitorReload`**
   (`[Theory]` for `Options.DefaultName` and `"named"`)
   - Use a validator implementing both interfaces with separate sync/async
     counters.
   - After startup, resolve a snapshot twice in scope 1 and assert both reads are
     the same instance and only one synchronous validation occurred.
   - Successfully reload the monitor and assert its value is the exact async
     reload candidate while scope 1 still returns its original snapshot.
   - Resolve scope 2 and assert its snapshot is a new instance, is not the monitor
     candidate, and is synchronously validated exactly once.
   - Assert async calls are exactly startup plus reload; snapshot reads do not add
     async calls.

4. **`ValidateOnChange_FailReads_RecoversOnNextSuccessfulReload`**
   - Fail one current reload under `FailReads`, capture the callback exception,
     and assert monitor reads throw that exact instance.
   - Trigger and complete the next generation successfully.
   - Assert the exception entry is replaced by the exact successful candidate,
     `onError` remains at one call, and `OnChange` runs exactly once for the
     successful generation only.

### Success Criteria

- [ ] Both enum values and callback-time behavior are directly asserted.
- [ ] `IOptions`, `IOptionsSnapshot`, and `IOptionsMonitor` contracts are
      distinguished by identity and dispatch counts.
- [ ] No failed reload reaches a change listener.
- [ ] The standard phase gate passes.

---

## Phase 3: Names, Dispatch, Overloads, and Registration Precedence

### Overview

Pin mixed opted/non-opted behavior, async-capability preservation, dependency
overloads, and the per-name last-registration-wins map.

### Files to Test

- **Sources**: `OptionsBuilder.cs`, `AsyncValidateOptions.cs`,
  `OptionsFactory.cs`, `OptionsReloadValidation.cs`, `OptionsMonitor.cs`
- **Test file**: `AsyncOptionsValidationTests.cs`

### Exact Tests and Assertions

1. **`ValidateOnChange_NonOptedName_RetainsLegacySynchronousReload`**
   - Configure `"opted"` with `ValidateOnChange` and `"legacy"` without it,
     using a dual-interface validator and separate change sources.
   - Trigger `"legacy"` and assert, immediately on return from `Trigger`, that
     its cache value and listener payload changed and the synchronous validation
     count increased by one.
   - Assert the async count did not change for `"legacy"`.
   - Trigger `"opted"`, wait for its controlled async invocation, and assert it
     uses `ValidateAsync` without increasing the sync count.

2. **`ValidateOnChange_NonOptedName_WithAsyncOnlyValidator_FailsSynchronously`**
   - Register an async-only validator that applies to all names; opt in only
     `"opted"`.
   - Trigger `"legacy"` and assert the legacy synchronous change callback throws
     `OptionsValidationException`.
   - Assert its failure contains
     `"Synchronous creation paths cannot execute or await ValidateAsync"`.
   - Assert no listener is notified and no async invocation is made for
     `"legacy"`.

3. **`ValidateOnChange_ValidatorImplementingBoth_UsesOnlyValidateAsyncForReload`**
   - Record startup baselines, trigger a reload, and complete it successfully.
   - Assert the reload adds exactly one async call and zero sync calls.
   - Assert the exact async-validated candidate is published.

4. **`ValidateOnChange_ValidatorTypeRegistration_PreservesAsyncCapabilityAndNameFilter`**
   - Register a singleton spy through `Validate<TValidator>()` on a builder named
     `"selected"` and call `ValidateOnChange`.
   - Create/reload `"other"` and assert the named wrapper skips it without
     invoking either underlying method.
   - Reload `"selected"` and assert the underlying async method runs once, the
     sync method never runs, and the validator sees `"selected"`.

5. **`ValidateOnChange_AsyncDependencyOverloads_AllRunDuringReload`**
   - Register distinct dependency marker instances and all async `Validate`
     overloads from zero through five dependencies on one builder.
   - Reset counters after startup, perform one successful reload, and assert every
     overload runs exactly once.
   - For each overload assert it receives the registered dependency instance(s),
     the same reload candidate, and the coordinator cancellation token.
   - Assert no synchronous fail-fast path is used and the candidate is published
     once.

6. **`ValidateOnChange_DuplicateSameNameRegistration_LastBehaviorAndCallbackWin`**
   - Register the same name first with `KeepLastGood`/callback A, then with
     `FailReads`/callback B.
   - Fail a current reload.
   - Assert callback A count is zero, callback B count is one with the correct
     name/error, and monitor reads throw the callback-B exception, proving the
     final behavior and callback both won.

7. **`ValidateOnChange_DifferentNames_KeepIndependentBehaviorAndCallbacks`**
   - Register `"keep"` with `KeepLastGood`/callback A and `"fail"` with
     `FailReads`/callback B.
   - Fail each name independently.
   - Assert `"keep"` still returns its own exact startup value and only callback A
     receives `"keep"`.
   - Assert `"fail"` throws its own callback-B exception and only callback B
     receives `"fail"`.
   - Assert neither registration changes the other name's behavior, value, or
     callback count.

### Success Criteria

- [ ] Mixed-name behavior is proven in one provider.
- [ ] Dual-interface, type-based, and dependency-overload registrations retain
      async capability.
- [ ] Same-name overwrite and different-name isolation are both pinned.
- [ ] The standard phase gate passes.

---

## Phase 4: Coalescing, Generations, and Startup/Change Races

### Overview

Exercise the per-name worker and generation state machine with explicit gates.
Every ordering is established by an entered/release signal; no test relies on
scheduler timing.

### Files to Test

- **Source**: `OptionsMonitor.cs`
- **Test file**: `AsyncOptionsValidationTests.cs`

### Exact Tests and Assertions

1. **`ValidateOnChange_BurstForSameName_UsesSingleWorkerAndCoalescesToLatestGeneration`**
   - Block the first reload validation, issue a fixed burst of additional change
     signals, then complete the first candidate successfully.
   - Wait for the one follow-up validation representing the latest generation and
     hold it.
   - Before releasing it, assert the first reload candidate was neither published
     nor notified and the monitor still returns the startup instance.
   - Complete the latest invocation and assert:
     - maximum active validations for the name is one;
     - only two reload candidates were created regardless of burst size;
     - listener count is one;
     - the exact latest candidate is published;
     - the superseded successful candidate is never observed.

2. **`ValidateOnChange_DifferentNames_RunReloadWorkersIndependently`**
   - Complete startup for names `"one"` and `"two"`.
   - Enter and hold `"one"` reload validation, then trigger `"two"`.
   - Assert `"two"` enters validation while `"one"` remains blocked and maximum
     active validation count reaches two.
   - Release both and assert each name publishes its own exact candidate and
     notifies once, with no cross-name value/callback.

3. **`ValidateOnChange_SupersededFailure_DoesNotApplyBehaviorNotifyOrInvokeOnError`**
   - Under `FailReads`, block generation 1, signal generation 2, then complete
     generation 1 with failure.
   - Hold generation 2 after it enters.
   - Assert generation 1 did not install an exception, did not notify, and did not
     invoke `onError`; the monitor still returns the exact startup value.
   - Complete generation 2 successfully and assert only it publishes/notifies.

4. **`ValidateOnChange_FailureSupersededBeforeCallback_DoesNotInvokeOnError`**
   - Fail a current `KeepLastGood` generation.
   - Use `TestOptionsEventListener` to block synchronously inside event ID 1,
     after failure behavior was selected but before production calls `onError`.
   - While blocked, trigger the next generation, then release the event listener.
   - Wait until the next validator invocation enters and assert `onError` count is
     still zero.
   - Complete the new generation successfully and assert it alone publishes and
     notifies.
   - This is the deterministic regression test for the narrow supersession race
     identified during research.

5. **`ValidateOnChange_ChangeDuringStartup_DiscardsSupersededCandidateAndSeedsLatestCandidate`**
   - Start async startup validation and hold its first candidate.
   - Trigger a change before startup validation completes, then release the first
     candidate successfully.
   - Assert startup does not complete and a second startup validation enters.
   - Release the second candidate and assert it is the exact value seeded into
     both `IOptions.Value` and the monitor.
   - Assert the first candidate is never published, no change listener fires, and
     only one validation is active at a time.

### Conditional Fix Point

If test 4 fails as predicted, make only this focused production change:

- In `OptionsMonitor<TOptions>.ProcessReloadsAsync` /
  `ReportReloadFailure`, carry the `ReloadState` and observed generation into
  failure reporting.
- After emitting the diagnostic and immediately before choosing to invoke user
  `onError`, recheck disposal and generation under `state.SyncObj`.
- Treat that check as the callback-decision linearization point, then invoke user
  code outside the lock so callback-time monitor reads remain reentrant.
- Do not alter coalescing, cache behavior, public API, or listener semantics.

No fix is made unless the deterministic test fails against the current code.

### Success Criteria

- [ ] Same-name serialization and different-name parallelism are both asserted.
- [ ] Superseded success, superseded failure, and the post-behavior callback
      window are covered.
- [ ] Startup/change reconciliation preserves exact candidate identity.
- [ ] No sleep, delay, polling, or unbounded wait exists.
- [ ] The standard phase gate passes.

---

## Phase 5: Disposal and Diagnostics

### Overview

Cover both cooperative and non-cooperative disposal paths and every
`OptionsEventSource` event. Events 1-3 are asserted through integration paths.
Event 4 is a defensive outer-worker event with no supported deterministic fault
injection point when exact built-ins are required, so its leaf payload contract is
tested by directly invoking the internal event method.

### Files to Test

- **Sources**: `OptionsMonitor.cs`, `OptionsEventSource.cs`
- **Test file**: `AsyncOptionsValidationTests.cs`

### Exact Tests and Assertions

1. **`ValidateOnChange_DisposeCancelsCooperativeValidatorWithoutCallbackOrNotification`**
   - Enter a reload validator that registers for its supplied cancellation token.
   - Dispose the monitor and wait for the validator's cancellation-observed and
     exited signals.
   - Assert the token is canceled, no value is published, no listener runs, and
     `onError` count remains zero.
   - Assert the cached startup value remains readable.

2. **`ValidateOnChange_DisposeDiscardsLateFailureFromCancellationIgnoringValidator`**
   - Enter a validator that deliberately ignores cancellation, dispose the
     monitor, assert its captured token is canceled, then explicitly complete it
     with a failed result.
   - Wait for validator exit.
   - Assert no exception is installed, no listener runs, no diagnostic failure
     callback runs, and the startup value remains cached.

3. **`ValidateOnChange_FailedReload_EmitsReloadValidationFailedEvent`**
   (`[Theory]` for `KeepLastGood` and `FailReads`)
   - Fail a uniquely named current generation and wait for event ID 1.
   - Assert source name `Microsoft-Extensions-Options`, ID `1`, and level
     `Warning`.
   - Assert payload, in order, equals:
     `typeof(FakeOptions).ToString()`, the exact options name,
     `typeof(OptionsValidationException).ToString()`, and the integer behavior.

4. **`ValidateOnChange_ThrowingOnError_IsContainedEmitsDiagnosticAndNextReloadSucceeds`**
   - Have `onError` increment once and throw `InvalidOperationException`.
   - Assert event ID 1 is emitted for the validation failure.
   - Assert event ID 2 has level `Error` and payload:
     options type, exact name, and
     `typeof(InvalidOperationException).ToString()`.
   - Trigger a following successful generation and assert its exact candidate is
     published/notified, proving the callback exception neither escapes nor
     kills the worker.

5. **`ValidateOnChange_ThrowingChangeListener_EmitsDiagnosticAndKeepsWorkerUsable`**
   - Register a listener that records its candidate and throws
     `InvalidOperationException`.
   - Complete a successful reload and wait for event ID 3.
   - Assert level `Error` and payload: options type, exact name, and thrown
     exception type.
   - Assert the candidate was already installed in the monitor.
   - Complete another successful reload and assert the monitor advances and a
     second event is observed, proving the worker remains usable.
   - Do not add an unsupported assertion that multicast subscribers are isolated;
     that is not one of the documented MVP policies.

6. **`OptionsEventSource_ReloadWorkerFailed_EmitsExpectedPayload`**
   - Enable the listener, directly call the internal
     `OptionsEventSource.Log.ReloadWorkerFailed` with unique values, and wait for
     event ID 4.
   - Assert level `Error` and the exact three payload entries: options type,
     options name, and exception type.

### Success Criteria

- [ ] Disposal is covered for validators that honor and ignore cancellation.
- [ ] Event IDs 1, 2, 3, and 4 have exact source, level, and payload assertions.
- [ ] Every listener is disposed and filters on a unique name.
- [ ] Callback/listener exceptions cannot strand subsequent reloads.
- [ ] The standard phase gate passes.

---

## Phase 6: Exact Built-In Service Rejection

### Overview

Verify that opting into asynchronous reload validation rejects every custom or
derived core service independently and does so at startup before candidate
validation. This distinguishes the `ValidateOnChange` contract from the existing
`ValidateOnStart` fallback tests, which must remain unchanged.

### Files to Test

- **Sources**: `OptionsBuilderExtensions.cs`, `OptionsMonitor.cs`
- **Test file**: `AsyncOptionsValidationTests.cs`

### Exact Tests and Assertions

1. **`ValidateOnChange_NonBuiltInCoreService_StartupFailsBeforeAsyncValidation`**
   - Use one theory with six explicit replacement cases:
     `CustomMonitor`, `DerivedMonitor`, `CustomFactory`, `DerivedFactory`,
     `CustomCache`, and `DerivedCache`.
   - Replace only the selected service in each row; leave the other two built-in.
   - Run `IAsyncStartupValidator.ValidateAsync`.
   - Assert `InvalidOperationException`.
   - Assert the message contains:
     - `typeof(FakeOptions).ToString()`;
     - `"requires the built-in options monitor, factory, and cache implementations"`;
     - the actual runtime type names of the resolved monitor, factory, and cache.
   - Assert the candidate async validator call count is zero, proving rejection
     occurs before validation/publication.
   - Assert no `onError` or `OnChange` callback runs.

2. **`ValidateOnChange_CustomDefaultIOptions_StartupFailsBeforeAsyncValidation`**
   - Keep monitor/factory/cache built-in but replace default
     `IOptions<FakeOptions>` with `OptionsWrapper<FakeOptions>`.
   - Assert `InvalidOperationException`.
   - Assert the message contains `typeof(FakeOptions).ToString()`,
     `"requires the built-in IOptions<TOptions> implementation"`, and
     `typeof(OptionsWrapper<FakeOptions>).ToString()`.
   - Assert async validation was not invoked and nothing was published/notified.

### Success Criteria

- [ ] Monitor, factory, and cache are each tested as custom and derived types.
- [ ] Default `IOptions` rejection remains independently covered.
- [ ] Existing derived-factory/custom-cache `ValidateOnStart` fallback tests are
      preserved and still pass in the class-filtered gate.
- [ ] The standard phase gate passes.

---

## Policy-to-Test Traceability

| # | Required policy | Direct proposed tests and exact proof |
|---:|---|---|
| 1 | `ValidateOnChange` is opt-in and enables startup validation. | `ValidateOnChange_EnablesAsyncStartupValidationAndSeedsExactCandidate` asserts implicit startup registration and exact seeding; `ValidateOnChange_NonOptedName_RetainsLegacySynchronousReload` proves opt-in scope. |
| 2 | Non-opted names keep legacy synchronous reload. | `ValidateOnChange_NonOptedName_RetainsLegacySynchronousReload` asserts inline sync validation/publication; `ValidateOnChange_NonOptedName_WithAsyncOnlyValidator_FailsSynchronously` pins the async-only interaction and failure text. |
| 3 | One coalescing worker per name. | `ValidateOnChange_BurstForSameName_UsesSingleWorkerAndCoalescesToLatestGeneration` asserts max active = 1 and two reload candidates for an arbitrary burst; `ValidateOnChange_DifferentNames_RunReloadWorkersIndependently` proves no global worker lock. |
| 4 | Only the latest observed generation publishes. | The burst test asserts the superseded successful candidate is never published/notified; `ValidateOnChange_SupersededFailure_DoesNotApplyBehaviorNotifyOrInvokeOnError` covers a superseded failure. |
| 5 | Dual-interface validators dispatch asynchronously. | `ValidateOnChange_ValidatorImplementingBoth_UsesOnlyValidateAsyncForReload`, `ValidateOnChange_ValidatorTypeRegistration_PreservesAsyncCapabilityAndNameFilter`, and `ValidateOnChange_AsyncDependencyOverloads_AllRunDuringReload` assert zero sync dispatch and exact async calls. |
| 6 | Only successful validated values publish and notify. | Successful default/named tests assert exact identity and one notification; failure-behavior and superseded-failure tests assert old/exception cache behavior and zero notifications. |
| 7 | Exact built-in monitor, factory, and cache are required. | `ValidateOnChange_NonBuiltInCoreService_StartupFailsBeforeAsyncValidation` covers custom and derived forms of all three, exact message types, and zero validator calls. |
| 8 | `IOptions<T>.Value` remains fixed to its startup winner. | `ValidateOnChange_IOptionsValue_RemainsStartupWinnerAcrossSuccessfulAndFailedReloads` asserts `Assert.Same(startup, options.Value)` after both successful publication and `FailReads`. |
| 9 | `IOptionsSnapshot<T>` remains scoped and synchronous. | `ValidateOnChange_IOptionsSnapshot_RemainsScopeLocalAndSynchronousAfterMonitorReload` covers default/named instances, same-scope identity, next-scope replacement, sync counts, and no snapshot async dispatch. |
| 10 | The monitor receives only successful validated reloads. | Default/named success tests assert exact candidates; failure tests assert zero listener calls; `ValidateOnChange_FailReads_RecoversOnNextSuccessfulReload` asserts recovery and notification only for success. |
| 11 | Repeated same-name registration is last-wins. | `ValidateOnChange_DuplicateSameNameRegistration_LastBehaviorAndCallbackWin` asserts final callback and `FailReads`; `ValidateOnChange_DifferentNames_KeepIndependentBehaviorAndCallbacks` proves dictionary entries do not overwrite other names. |
| 12 | `onError` runs once after behavior application. | `ValidateOnChange_FailedCurrentReload_AppliesBehaviorBeforeInvokingOnError` asserts one call, exact error/name, reentrant callback-time reads, last-good identity, and exact exception identity for `FailReads`. |
| 13 | Superseded/disposal failures do not call `onError`. | Both superseded-failure tests assert zero callbacks at distinct race windows; both disposal tests cover cancellation-honoring and cancellation-ignoring validators with zero callbacks. |
| 14 | Exceptions from `onError` are contained and diagnosed. | `ValidateOnChange_ThrowingOnError_IsContainedEmitsDiagnosticAndNextReloadSucceeds` asserts event ID 2 payload and a later successful reload. |
| 15 | Startup publishes the exact asynchronously validated candidate. | `ValidateOnChange_EnablesAsyncStartupValidationAndSeedsExactCandidate` covers the ordinary path; `ValidateOnChange_ChangeDuringStartup_DiscardsSupersededCandidateAndSeedsLatestCandidate` covers the generation race and exact winning identity. |
| 16 | Custom/derived startup services follow explicit fallback/failure rules; reload rejects non-built-ins. | The core-service rejection theory and custom-`IOptions` test pin reload failure. Existing `AsyncStartupValidation_DerivedFactoryUsesSynchronousFallback`, custom-cache publication/retry/rejection tests remain in place and run after every phase to protect the startup-only fallback rules. |

---

## Conditional Production-Fix Rules

1. **No speculative production changes.** Implement each test phase first and run
   its scoped gate.
2. A production change requires a repeatable failure of a new test whose
   assertion is directly tied to the policy table.
3. The only pre-identified likely fix is the generation recheck described in
   Phase 4 for
   `ValidateOnChange_FailureSupersededBeforeCallback_DoesNotInvokeOnError`.
   Limit that fix to `OptionsMonitor.cs`.
4. Any other defect fix must touch only the smallest responsible source file and
   must retain the failing test as its regression test. Do not refactor unrelated
   startup code, public APIs, caches, or existing tests.
5. If a failure is caused by an invalid test assumption rather than an MVP
   contract violation, correct the test; do not change production merely to make
   the test green.
6. After any permitted production fix, rerun the current phase gate, all prior
   phase tests through the class filter, the unfiltered core test project, and the
   final full-library target.

## Phase 7: Coverage-Gap Review, Final Validation, and Cleanup

### Coverage-Gap Review

Perform this review after all six implementation phases:

- [ ] Reconcile every implemented test name against all 16 rows in the
      traceability table; no row may rely only on an analogous legacy test.
- [ ] Confirm default and named options, both failure behaviors, success/failure
      recovery, same-name bursts, per-name parallelism, startup races,
      supersession at both identified windows, and both disposal modes are
      present.
- [ ] Confirm exact instance identity is asserted for startup, reload,
      last-known-good, `IOptions`, snapshot, listener, and monitor values.
- [ ] Confirm exact exception identity is asserted for `FailReads`.
- [ ] Confirm callback/listener counts and names are asserted, not inferred.
- [ ] Confirm events 1-4 have exact source, ID, level, and ordered payload checks.
- [ ] Confirm no `Task.Delay`, `Thread.Sleep`, `SpinWait`, polling loop, or
      unbounded wait was introduced.
- [ ] Confirm every gate uses the existing test project and both project TFMs;
      no new package, test project, or mock framework was added.
- [ ] Confirm all preexisting `AsyncOptionsValidationTests` remain present and
      pass.
- [ ] Document the accepted structural limit: event ID 4's payload is tested
      directly because the defensive outer-worker catch cannot be reached through
      supported exact-built-in services without artificial reflection/fault
      injection.
- [ ] Do not add a coverage collector; repository coverage is evaluated
      separately.

### Final Validation Sequence

1. Run the no-incremental focused project build.
2. Run the unfiltered core test project through `/t:Test`.
3. Run `.\build.cmd -projects src\libraries\Microsoft.Extensions.Options -test`
   from `R:\`.
4. Inspect the final diff and require that it contains only:
   - additive changes to `AsyncOptionsValidationTests.cs`; and
   - a narrowly scoped production fix backed by a deterministically failing test,
     if one was actually required.
5. Do not stage or commit.

### `.testagent` Cleanup

Only after all validation is green and the implementation summary has recorded
the final test/fix results, remove the planning artifacts:

```powershell
Remove-Item -LiteralPath R:\.testagent -Recurse -Force
```

### Final Success Criteria

- [ ] All 38 planned xUnit executions pass alongside all existing tests.
- [ ] The complete `Microsoft.Extensions.Options` library target passes,
      including trimming coverage.
- [ ] No timing-based synchronization, unrelated production change, new test
      project, staged file, or commit exists.
- [ ] `.testagent` is removed only at completion.
