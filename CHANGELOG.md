# Changelog

All notable changes to this package will be documented in this file.

The format is based on [Keep a Changelog](http.keepachangelog.com/en/1.0.0/)
and this project adheres to [Semantic Versioning](http.semver.org/spec/v2.0.0.html).

## [Unreleased]

### Fixed

- `ServiceLocator.RegisterState<T>()` now replaces a registration the same way `RegisterService<T>()` does: the previous instance is disposed and any handlers it owned are dropped. Previously a replaced `IDisposable` state was never disposed, contrary to the 0.4.0 notes, and registering a state over a service left that service's reducers firing. Re-registering the same instance is a no-op.
- `ServiceLocator.RegisterService<T>()` resolves dependencies and constructs the new instance before tearing down the old one. A missing dependency or a throwing constructor now leaves the current registration intact instead of unregistering and disposing it.
- Handlers that return a `Task` (e.g. an `async` `[SideEffect]`) have their exceptions logged with `Debug.LogException`. Previously a fault after the first `await` was lost in an unobserved task.
- `ReactiveProperty<T>` subscriptions can be disposed twice safely. Previously the second `Dispose()` could remove another subscription that used the same delegate.
- `ServiceLocator.Get<T>()` on an unregistered type throws a `KeyNotFoundException` that names the type and the call to make. The missing-dependency error of `RegisterService<T>()` also names the call to make.
- `Docs`: the README middleware example now compiles (`NextActionDelegate` instead of `Action<IAction>`).
- `Tests`: regression coverage for each fix above, plus `UnregisterState` and constructor injection of the registered instance.

## [0.5.0] - 2026-10-21

### Added

- `ReactiveSignal<T>` (and a payload-less `ReactiveSignal`): a stream of one-shot events. Every `Publish` reaches every subscriber — even when the payload equals the previous one — and nothing is stored or replayed on subscribe. It complements `ReactiveProperty<T>`, which is distinct-until-changed and so cannot express repeated events (two hits of the same damage, a heal, an effect firing again). Subscriptions are `IDisposable`, disposing twice is harmless, and a subscriber removed during a publish stops receiving from the next event on.
- `Tests`: coverage for repeated payloads, no replay on subscribe, disposal, unsubscribing during a publish, and the payload-less signal.

## [0.4.1] - 2026-07-10

### Fixed

- `ServiceLocator.InitializeAll()` now initializes each instance at most once, even when called repeatedly (a per-scene bootstrap typically calls it on every scene load). Previously a persistent `IInitializable` had its `Initialize()` re-invoked on every call, so a service that acquired a resource there (e.g. an event subscription) would stack duplicates. The tracking is kept in sync on unregister/`ClearAll`, so a re-registered instance is still initialized afresh. This makes `Initialize()`/`Dispose()` a clean once-each lifecycle — services no longer need a manual guard.

## [0.4.0] - 2026-07-10

### Added

- `ServiceLocator`: services and states implementing `IDisposable` are now disposed when they are unregistered, replaced by a re-registration, or cleared via `ClearAll()`. This pairs with `IInitializable` to give registrations a symmetric setup/teardown lifecycle — e.g. a service can unsubscribe from an external event in `Dispose()` without leaking across scene re-entries. Disposal is exception-safe: a throwing `Dispose()` is logged and never aborts the surrounding teardown.
- `Tests`: coverage for disposal on unregister, re-registration, and `ClearAll`, including the exception-safe path.

## [0.3.0] - 2026-07-09

### Fixed

- `ServiceLocator`: `RegisterService<T>` is now idempotent — re-registering a service replaces the previous instance and its handlers instead of appending a duplicate set. This prevents reducers from firing multiple times after a scene re-entry.

### Added

- `ServiceLocator.UnregisterService<T>()` / `UnregisterState<T>()`: remove a registration and any action handlers it owns (enables scene-scoped teardown).
- `ServiceLocator.IsRegistered<T>()`: query whether a type is currently registered.
- `ServiceLocator`: automatic static-state reset on `SubsystemRegistration`, so registrations no longer leak between play sessions when domain reload is disabled ("Enter Play Mode Options").
- `Tests`: coverage for idempotent re-registration and unregistration.

## [0.2.1] - 2026-03-27

### Changed

- `CI`: updated GitHub Actions workflow for unit tests.
- `Docs`: updated installation options in README.
- `Docs`: minor format adaptations in README.
- `Package`: downgraded `com.unity.test-framework` dependency to v1.4.6 for Unity 2022.3 LTS compatibility.

### Added

- `Docs`: added new repository badges in README.

## [0.2.0] - 2026-03-20

### Changed

- `CI`: updated tests workflow trigger conditions.
- `CI`: updated github tests workflow name.

### Added

- `CI`: added github tests workflow.
- `Docs`: added c# compatibility remark to README.
- `Docs`: added tests status badge to README.

## [0.1.0] - 2026-03-10

### This is the first release of *NanoServiceFlow*.

Initial release of the NanoServiceFlow micro-framework, providing a true zero-dependency, allocation-free Unidirectional Data Flow architecture for modern Unity.

### Added

- `ServiceLocator` core container for lightweight dependency injection and automated action routing.
- `ReactiveProperty<T>` primitive for zero-allocation, GC-friendly state observation with built-in `DistinctUntilChanged` evaluation.
- `IAction` interface to enforce strict, predictable data flow.
- `[Reducer]` attribute to automatically map and route synchronous state mutations.
- `[SideEffect]` attribute to support async-agnostic logic routing (compatible with standard Tasks, Coroutines, and UniTask).
- `IInitializable` interface to support Phase 2 boot and setup logic in injected services.
- Comprehensive Edit Mode Unit Test suite verifying DI resolution, state mutation, and reactive subscriptions.
- Full UPM package structure including Assembly Definitions and documentation.
