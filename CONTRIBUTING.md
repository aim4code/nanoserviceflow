# Contributing to NanoServiceFlow

First off, thank you for considering contributing to NanoServiceFlow! It is people like you that make the open-source community such an incredible place to learn, inspire, and create.

This document outlines the process, coding standards, and architectural philosophy required to contribute to this repository.

## 1. Architectural Philosophy
Before submitting a Pull Request, please ensure your changes align with the core pillars of this micro-framework:
* **Zero Dependencies:** NanoServiceFlow must remain a standalone package. Do not introduce third-party libraries (e.g., UniRx, Zenject, or NuGet packages) into the core runtime.
* **Zero Allocation:** The framework is designed for high-performance Unity games. Avoid introducing features that generate garbage collection (GC) overhead during runtime (e.g., avoid LINQ in the hot path, use `readonly struct` for actions).
* **Unidirectional Data Flow:** State must remain immutable from the outside and only be modified via Reducers.

## 2. Reporting Bugs
If you find a bug, please open an Issue using the "Bug Report" template. Include:
* The Unity version you are using.
* A clear and concise description of the bug.
* Steps to reproduce the behavior.
* Any relevant console logs or stack traces.

## 3. Suggesting Enhancements
Feature requests are welcome! Please open an Issue using the "Feature Request" template.
* Explain *why* this enhancement is necessary.
* Keep in mind that features bloating the framework or violating the "Zero Dependency" rule will likely be rejected. We prefer to keep the core API surface as small as possible.

## 4. Local Development Setup
1. Clone the repository.
2. Open the project in Unity (Minimum supported version is 6.3+).
3. Ensure your IDE is set up to compile against the Unity engine.

### Coding Standards
* **C# Version:** Even though modern Unity supports newer C# features, please ensure all code is compatible with **C# 9.0**. For example, use standard `readonly struct` instead of C# 10 `record struct` to ensure maximum compatibility for users on older Unity LTS versions.
* **Namespaces:** All core code must reside within the `Aim4code.NanoServiceFlow` namespace. Test code must reside in `Aim4code.NanoServiceFlow.Tests.Editor`.

## 5. Pull Request Process
1. Fork the repository and create your branch from `development` (not `main`).
2. Name your branch descriptively (e.g., `feature/add-async-dispatch` or `fix/locator-caching`).
3. Write tests for your changes. **Pull Requests without accompanying Edit Mode Unit Tests will not be merged.**
4. Ensure the test suite passes locally in Unity's Test Runner.
5. Submit your PR against the `development` branch.
6. Update the README.md with details of changes to the interface, if applicable.

## 6. Code of Conduct
By participating in this project, you agree to maintain a respectful, inclusive, and professional environment. Harassment or abusive behavior will not be tolerated.