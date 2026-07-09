# Changelog

All notable changes to this package will be documented in this file.

The format is based on [Keep a Changelog](http.keepachangelog.com/en/1.0.0/)
and this project adheres to [Semantic Versioning](http.semver.org/spec/v2.0.0.html).

## [Unreleased]

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
