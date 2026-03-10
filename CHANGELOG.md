# Changelog
All notable changes to this package will be documented in this file.

The format is based on [Keep a Changelog](http://keepachangelog.com/en/1.0.0/)
and this project adheres to [Semantic Versioning](http://semver.org/spec/v2.0.0.html).

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
