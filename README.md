<img src="Documentation~/images/Logo.png" width="600">

# NanoServiceFlow

[![Test](https://github.com/aim4code/nanoserviceflow/actions/workflows/test.yml/badge.svg)](https://github.com/aim4code/nanoserviceflow/actions)
[![openupm](https://img.shields.io/npm/v/com.aim4code.nanoserviceflow?label=openupm&registry_uri=https://package.openupm.com)](https://openupm.com/packages/com.aim4code.nanoserviceflow/)
![](https://img.shields.io/badge/Unity-2022.3+-57b9d3.svg?style=flat&logo=unity)
![last-commit](https://img.shields.io/github/last-commit/aim4code/nanoserviceflow)
![open-issues](https://img.shields.io/github/issues/aim4code/nanoserviceflow)

> **A lightweight, Zustand-inspired state management and event-driven architecture for modern Unity, with no third-party dependencies.**

NanoServiceFlow is a micro-framework designed to bring the predictability of Redux and the pragmatic, modular state slices of Zustand into Unity without the massive boilerplate. Built entirely on modern C# (C# 9.0 compliant), it provides a lightweight state management solution with no third-party dependencies, for Unity 2022.3 LTS and newer. Its only package dependency is Unity's own Test Framework, which its tests use.

> [!NOTE]
> **A Note on C# Compatibility:** While NanoServiceFlow is designed for modern Unity, the codebase is currently strictly **C# 9.0 compliant**. This is a deliberate choice for developer experience (DX). Currently, Unity's `.csproj` generation pipeline (particularly for isolated test assemblies) still defaults to C# 9.0, which causes IDE errors for users trying to use modern syntax out-of-the-box. Once Unity's project generation fully and natively supports the C# 12 pipeline across all contexts without requiring custom compiler overrides, this framework will be updated to leverage cleaner modern features like C# 10 `record struct` and C# 12 primary constructors.

## Key Features

* **Zustand / Redux Inspired:** State changes by dispatching `Actions` that reducers handle. No massive global store—state is sliced into domain-specific modules. This is a convention rather than a lock: `ReactiveProperty<T>.Value` is publicly settable, so keep writes inside reducers.
* **Lightweight Reactivity:** `ReactiveProperty<T>` for values and `ReactiveSignal<T>` for one-shot events. Setting a value and notifying subscribers doesn't allocate. No need for heavy Rx libraries.
* **Service-Oriented CQRS:** Cleanly separate your data (State) from your logic (Services).
* **Async-Agnostic Side-Effects:** A side-effect can return a standard .NET `Task`, a Cysharp `UniTask`, or nothing. The framework calls it without awaiting it, and logs the exception of a failing `Task`. See [Async Side-Effects and Errors](#async-side-effects-and-errors).
* **Low-Boilerplate DI:** Services receive their states and other services through constructor injection, and their `[Reducer]` and `[SideEffect]` methods are wired automatically when they are registered.

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
  "com.aim4code.nanoserviceflow": "0.5.2"
}
```

### Option 2: Install via Git URL

You can also install the package directly from GitHub. Add the following dependency to your `Packages/manifest.json`:

```json
"dependencies": {
  "com.aim4code.nanoserviceflow": "https://github.com/aim4code/nanoserviceflow.git#v0.5.2"
}
```

> [!IMPORTANT]
> **Version Determinism:** Notice the `#v0.5.2` at the end of the URL. If you omit the version tag, Unity will resolve the dependency using the latest commit on the default branch at the time of checkout. As the branch updates, this can lead to team members having different versions of the package installed, breaking version determinism. Always lock your Git dependencies to a specific release tag.
>
> *See Unity's official documentation on [Targeting a specific revision](https://docs.unity3d.com/Manual/upm-git.html#revision) for more details.*

## Quick Start

### 1. Define Pure Data (State) & Actions

Use the built-in `ReactiveProperty` for state, and `readonly struct`s for immutable actions.

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

Services handle both synchronous state mutations (`[Reducer]`) and asynchronous side-effects (`[SideEffect]`). Both attributes are routed the same way: the attribute states the method's intent and labels it in the profiler. See [How Dispatch Works](#how-dispatch-works).

```csharp
using System.Threading.Tasks;
using Aim4code.NanoServiceFlow;

public class PlayerService : IInitializable, System.IDisposable {
    
    private readonly PlayerState _state;

    public PlayerService(PlayerState state) {
        _state = state;
    }

    public void Initialize() {
        // Optional: Run setup logic during Phase 2 Boot
    }

    public void Dispose() {
        // Optional: symmetric teardown. Called automatically when the service is
        // unregistered, replaced by a re-registration, or cleared via ClearAll() —
        // the place to unsubscribe from external events acquired in Initialize().
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

#### Events vs. values: `ReactiveSignal<T>`

`ReactiveProperty<T>` holds a value and only notifies when it *changes* — perfect for "health is 3", wrong for "the player was hit for 2" happening twice. For one-shot events use `ReactiveSignal<T>`: every `Publish` reaches every subscriber, and nothing is replayed on subscribe.

```csharp
public class CombatState
{
    public ReactiveProperty<int> Health { get; } = new(10);   // what is
    public ReactiveSignal<int> PlayerHit { get; } = new();    // what just happened
}

// In a service:   _state.PlayerHit.Publish(damage);
// In a view:      _hitSub = state.PlayerHit.Subscribe(damage => PlayHitEffect(damage));
```

### 5. Middleware Pipeline (Optional)

NanoServiceFlow supports a robust middleware pipeline, allowing you to intercept actions globally before they reach your Reducers or Side Effects. This is perfect for logging, analytics, or action filtering.

First, implement the `IMiddleware` interface:

```csharp
using UnityEngine;
using Aim4code.NanoServiceFlow;

public class LoggingMiddleware : IMiddleware 
{
    public void Invoke(IAction action, NextActionDelegate next) 
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

## API Overview

| API | What it does |
|---|---|
| `ServiceLocator.RegisterState<T>(T instance)` | Registers a state under type `T`. Replacing an existing registration disposes the previous instance (if it is `IDisposable`) and removes any handlers it had. |
| `ServiceLocator.RegisterService<T>()` | Creates `T` with its public constructor, injecting registered instances by exact parameter type, and wires its handlers. Re-registering replaces the previous instance. If a dependency is missing, it throws and keeps the current registration. |
| `ServiceLocator.Get<T>()` | Returns the instance registered as `T`. Throws `KeyNotFoundException` if there is none. |
| `ServiceLocator.IsRegistered<T>()` | `true` if something is registered as `T`. |
| `ServiceLocator.InitializeAll()` | Calls `Initialize()` on every registered `IInitializable` that hasn't been initialized yet. Safe to call on every scene load. |
| `ServiceLocator.UnregisterService<T>()` / `UnregisterState<T>()` | Removes the registration and its handlers, and disposes it if it is `IDisposable`. Does nothing if `T` isn't registered. |
| `ServiceLocator.ClearAll()` | Disposes and removes every registration, handler and middleware. Runs automatically at the start of each play session, even with domain reload disabled. |
| `ServiceLocator.Dispatch(action)` | Sends an action through the middlewares to its handlers. |
| `ServiceLocator.AddMiddleware(middleware)` | Appends a middleware. Middlewares run in the order they were added. |
| `ReactiveProperty<T>` | `Value` notifies subscribers only when it changes. `Subscribe(callback)` calls back immediately with the current value and returns an `IDisposable`. |
| `ReactiveSignal<T>` / `ReactiveSignal` | `Publish(value)` reaches every subscriber every time. `Subscribe(callback)` doesn't replay past events. |
| `IAction`, `IMiddleware`, `IInitializable` | Marker for actions, pipeline step, and one-time setup hook. Implement `System.IDisposable` for teardown. |
| `[Reducer]`, `[SideEffect]` | Mark the methods of a service that handle an action. |

Notes:

* **Registrations are keyed by exact type.** After `RegisterService<Foo>()`, `Get<IFoo>()` won't find it. To share an instance through an interface, register it as a state, `RegisterState<IFoo>(new Foo())`; its handlers are then not wired.
* **Give each service a single public constructor.** With several, which one is used is not defined.
* **Services keep the instances injected when they were created.** If you replace a state, re-register the services that depend on it.
* Members such as `ServiceLocator.Container` and `EditorActionHandlers` exist only in the Editor, for the profiler. Don't use them in game code: they won't compile in player builds.

## How Dispatch Works

* A handler is a **public instance method** of a **service** (registered with `RegisterService`), marked `[Reducer]` or `[SideEffect]`, with **exactly one parameter** whose type implements `IAction`. Methods that don't match, and methods on states, are silently ignored.
* Actions are matched by their **exact runtime type**. A handler that takes `IAction` or a base interface doesn't receive other actions.
* `[Reducer]` and `[SideEffect]` are routed the same way, and **there is no guaranteed order** between the handlers of one action. Don't rely on reducers running before side-effects.
* If a handler throws, the exception leaves `Dispatch` wrapped in a `TargetInvocationException`, and the remaining handlers for that action don't run.
* Dispatching from inside a handler is fine: it runs synchronously, nested inside the current dispatch. Registering or unregistering services from inside a handler or `Initialize()` is not supported yet.
* Return values are ignored, except a `Task` (see below).

## Async Side-Effects and Errors

The locator calls each handler and does not await what it returns, so routing stays async-agnostic.

* **`Task`**: if the returned task fails, now or later, its exception is logged with `Debug.LogException`.
* **UniTask**: the locator adds nothing. UniTask reports its own unobserved errors through `UniTaskScheduler`, logged as exceptions by default. For fire-and-forget side-effects, prefer `async UniTaskVoid`: it reports an error right away, while an `async UniTask` that nobody awaits only reports it when it is garbage-collected or its pooled task is reused.
* **Coroutines**: a side-effect that returns `IEnumerator` is not started, because services are not `MonoBehaviour`s. Start the coroutine yourself from a `MonoBehaviour`, or use `Task` or UniTask instead.

```csharp
[SideEffect]
public async UniTaskVoid PlayHealSequence(HealSequenceAction action)
{
    await UniTask.Delay(1000);
    ServiceLocator.Dispatch(new DamageAction(-50));
}
```

## Performance Notes

* Setting `ReactiveProperty<T>.Value` and notifying subscribers doesn't allocate. Subscribing allocates a small subscription object.
* `Dispatch` allocates a little on every call: struct actions are boxed, each handler is called through reflection with a one-element argument array, and each middleware adds a small closure. Avoid dispatching inside tight per-frame loops.

## Flow Profiler (Editor)

Open it from **Aim4code > NanoServiceFlow Profiler**.

* **Active Services & States**: every registered instance with its public fields and properties, including `ReactiveProperty` values. Refreshed once per second in Play mode.
* **Actions Timeline**: the last 200 dispatched actions. Select one to see its payload, where it was dispatched from, its handlers, and the active middlewares.
* While the window is open, every `Dispatch` also records a stack trace. This costs time in the Editor only.

> [!WARNING]
> **Clear All** calls `ServiceLocator.ClearAll()`. In Play mode this disposes and removes every running service, state and middleware, not just the log.

## Architectural Philosophy

NanoServiceFlow diverges from traditional Redux by embracing **Composition over Inheritance**. Instead of a single global store, state is segregated into modular classes. The `ServiceLocator` acts as a **Mediator**, intercepting dispatched actions and routing them to the correct `[Reducer]` or `[SideEffect]`. This ensures high testability, true decoupling, and an architecture that scales cleanly as your codebase grows.

## License

MIT License. See `LICENSE.md` for more information.
