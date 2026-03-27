<img src="Documentation~\images\Logo.png" width="600">

# NanoServiceFlow

[![Test](https://github.com/aim4code/nanoserviceflow/actions/workflows/test.yml/badge.svg)](https://github.com/aim4code/nanoserviceflow/actions)
[![openupm](https://img.shields.io/npm/v/com.aim4code.nanoserviceflow?label=openupm&registry_uri=https://package.openupm.com)](https://openupm.com/packages/com.aim4code.nanoserviceflow/)
![](https://img.shields.io/badge/Unity-2022.3+-57b9d3.svg?style=flat&logo=unity)
![last-commit](https://img.shields.io/github/last-commit/aim4code/nanoserviceflow)
![open-issues](https://img.shields.io/github/issues/aim4code/nanoserviceflow)

> **A lightweight, Zustand-inspired, true zero-dependency state management and event-driven architecture for modern Unity.**

NanoServiceFlow is a micro-framework designed to bring the predictability of Redux and the pragmatic, modular state slices of Zustand into Unity without the massive boilerplate. Built entirely on modern C# (C# 9.0 compliant), it provides a blazing-fast, GC-friendly state management solution with absolutely zero external dependencies, ready for enterprise Unity LTS versions.

> [!NOTE]
> **A Note on C# Compatibility:** While NanoServiceFlow is designed for modern Unity, the codebase is currently strictly **C# 9.0 compliant**. This is a deliberate choice for developer experience (DX). Currently, Unity's `.csproj` generation pipeline (particularly for isolated test assemblies) still defaults to C# 9.0, which causes IDE errors for users trying to use modern syntax out-of-the-box. Once Unity's project generation fully and natively supports the C# 12 pipeline across all contexts without requiring custom compiler overrides, this framework will be updated to leverage cleaner modern features like C# 10 `record struct` and C# 12 primary constructors.

## Key Features

* **Zustand / Redux Inspired:** State is read-only and mutated exclusively by dispatching `Actions`. No massive global store—state is sliced into domain-specific modules.
* **Zero-Allocation Reactivity:** Includes a custom, GC-free `ReactiveProperty<T>` built specifically for high-performance Unity games. No need for heavy Rx libraries.
* **Service-Oriented CQRS:** Cleanly separate your data (State) from your logic (Services).
* **Agnostic Asynchronous Side-Effects:** Safely handle asynchronous logic using your preferred method. The framework routes actions perfectly whether you use standard .NET `Task`, Unity Coroutines, or highly-optimized third-party libraries like `UniTask`.
* **Zero Boilerplate DI:** Features a lightweight, interface-driven Dependency Injection container that resolves services and wires up Reducers automatically via reflection caching.

## Installation

### Option 1: Install via OpenUPM (Recommended)

The package is available on the [OpenUPM](https://openupm.com/) registry. The easiest way to install it is via the `openupm-cli`:

```bash
openupm add com.aim4code.nanoserviceflow
```

Alternatively, you can manually add the scoped registry to your `Packages/manifest.json`:

```json
"scopedRegistries": [
  {
    "name": "package.openupm.com",
    "url": "https://package.openupm.com",
    "scopes": [
      "com.aim4code"
    ]
  }
],
"dependencies": {
  "com.aim4code.nanoserviceflow": "0.2.0"
}
```

### Option 2: Install via Git URL

You can also install the package directly from GitHub. Add the following dependency to your `Packages/manifest.json`:

```json
"dependencies": {
  "com.aim4code.nanoserviceflow": "https://github.com/aim4code/nanoserviceflow.git#v0.2.0"
}
```

> [!IMPORTANT]
> **Version Determinism:** Notice the `#v0.2.0` at the end of the URL. If you omit the version tag, Unity will resolve the dependency using the latest commit on the default branch at the time of checkout. As the branch updates, this can lead to team members having different versions of the package installed, breaking version determinism. Always lock your Git dependencies to a specific release tag.
>
> *See Unity's official documentation on [Targeting a specific revision](https://docs.unity3d.com/Manual/upm-git.html#revision) for more details.*

## Quick Start

### 1. Define Pure Data (State) & Actions

Use the built-in `ReactiveProperty` for state, and standard C# structs for immutable, zero-allocation actions.

```csharp
using Aim4code.NanoServiceFlow;

public class PlayerState {
    public ReactiveProperty<int> Health { get; } = new(100);
}

public readonly struct DamageAction : IAction {
    public readonly int Amount;
    public DamageAction(int amount) => Amount = amount;
}

public readonly struct HealSequenceAction : IAction {}
```

### 2. Create a Service (Logic)

Services handle both synchronous state mutations (`[Reducer]`) and asynchronous side-effects (`[SideEffect]`).

```csharp
using System.Threading.Tasks;
using Aim4code.NanoServiceFlow;

public class PlayerService : IInitializable {
    
    private readonly PlayerState _state;

    public PlayerService(PlayerState state) {
        _state = state;
    }

    public void Initialize() {
        // Optional: Run setup logic during Phase 2 Boot
    }

    [Reducer]
    public void OnDamage(DamageAction action) {
        _state.Health.Value -= action.Amount;
    }

    // Note: The framework is async-agnostic. Standard Tasks are used here, 
    // but libraries like Cysharp's UniTask are highly recommended for production!
    [SideEffect]
    public async Task PlayHealSequenceAsync(HealSequenceAction action) {
        await Task.Delay(1000); // Non-blocking async flow
        
        // Re-use Reducer logic via the Locator
        ServiceLocator.Dispatch(new DamageAction(-50)); 
    }
}
```

### 3. Bootstrap the Architecture

Register your states, resolve your services, and start the engine.

```csharp
using UnityEngine;
using Aim4code.NanoServiceFlow;

public class GameBootstrapper : MonoBehaviour {
    void Awake() {
        // 1. Build the Data Graph (State Slices)
        ServiceLocator.RegisterState(new PlayerState());
        
        // 2. Resolve Services (State is injected automatically)
        ServiceLocator.RegisterService<PlayerService>();
        
        // 3. Start the Engine
        ServiceLocator.InitializeAll();
    }
}
```

### 4. Bind to the View (Unity UI)

Views only query the State and dispatch Actions. They never know about the Services.

```csharp
using System;
using UnityEngine;
using UnityEngine.UI;
using Aim4code.NanoServiceFlow;

public class PlayerView : MonoBehaviour {
    [SerializeField] private Text _healthText;
    private IDisposable _healthSub;

    void Start() {
        var state = ServiceLocator.Get<PlayerState>();

        // Bind UI to state
        _healthSub = state.Health.Subscribe(hp => _healthText.text = $"HP: {hp}");
    }

    public void OnDamageButtonClicked() {
        ServiceLocator.Dispatch(new DamageAction(10));
    }

    void OnDestroy() {
        _healthSub?.Dispose(); // Clean up subscription
    }
}
```

### 5. Middleware Pipeline (Optional)

NanoServiceFlow supports a robust middleware pipeline, allowing you to intercept actions globally before they reach your Reducers or Side Effects. This is perfect for logging, analytics, or action filtering.

First, implement the `IMiddleware` interface:

```csharp
using System;
using UnityEngine;
using Aim4code.NanoServiceFlow;

public class LoggingMiddleware : IMiddleware 
{
    public void Invoke(IAction action, Action<IAction> next) 
    {
        Debug.Log($"[Dispatcher] Action Started: {action.GetType().Name}");
        
        // Pass the action to the next middleware, or to the handlers if this is the last one
        next(action); 
        
        Debug.Log($"[Dispatcher] Action Finished: {action.GetType().Name}");
    }
}
```

Then, register it to the `ServiceLocator` during your boot phase. Middlewares are executed in the exact order they are added.

```csharp
ServiceLocator.AddMiddleware(new LoggingMiddleware());
```

## Architectural Philosophy

NanoServiceFlow diverges from traditional Redux by embracing **Composition over Inheritance**. Instead of a single global store, state is segregated into modular classes. The `ServiceLocator` acts as a **Mediator**, intercepting dispatched actions and routing them to the correct `[Reducer]` or `[SideEffect]`. This ensures high testability, true decoupling, and an architecture that scales cleanly as your codebase grows.

## License

MIT License. See `LICENSE.md` for more information.