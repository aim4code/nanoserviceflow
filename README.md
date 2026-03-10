# NanoServiceFlow
> **A lightweight, Zustand-inspired, true zero-dependency state management and event-driven architecture for modern Unity.**

NanoServiceFlow is a micro-framework designed to bring the predictability of Redux and the pragmatic, modular state slices of Zustand into Unity without the massive boilerplate. Built entirely on modern C# 12, it provides a blazing-fast, GC-friendly state management solution with absolutely zero external dependencies.

## Key Features
* **Zustand / Redux Inspired:** State is read-only and mutated exclusively by dispatching `Actions`. No massive global store—state is sliced into domain-specific modules.
* **Zero-Allocation Reactivity:** Includes a custom, GC-free `ReactiveProperty<T>` built specifically for high-performance Unity games. No need for heavy Rx libraries.
* **Service-Oriented CQRS:** Cleanly separate your data (State) from your logic (Services).
* **Agnostic Asynchronous Side-Effects:** Safely handle asynchronous logic using your preferred method. The framework routes actions perfectly whether you use standard .NET `Task`, Unity Coroutines, or highly-optimized third-party libraries like `UniTask`.
* **Zero Boilerplate DI:** Features a lightweight, interface-driven Dependency Injection container that resolves services and wires up Reducers automatically via reflection caching.

## Installation (Unity Package Manager)
Add the following dependency to your `Packages/manifest.json`:
```json
"dependencies": {
  "com.aim4code.nanoserviceflow": "https://github.com/aim4code/nanoserviceflow.git"
}
```

## Quick Start

### 1. Define Pure Data (State) & Actions
Use the built-in `ReactiveProperty` for state, and C# records for immutable, zero-allocation actions.
```csharp
using Aim4code.NanoServiceFlow;

public class PlayerState {
    public ReactiveProperty<int> Health { get; } = new(100);
}

public record struct DamageAction(int Amount) : IAction;
public record struct HealSequenceAction() : IAction;
```

### 2. Create a Service (Logic)
Services handle both synchronous state mutations (`[Reducer]`) and asynchronous side-effects (`[SideEffect]`).
```csharp
using System.Threading.Tasks;
using Aim4code.NanoServiceFlow;

public class PlayerService(PlayerState state) : IInitializable {
    
    public void Initialize() {
        // Optional: Run setup logic during Phase 2 Boot
    }

    [Reducer]
    public void OnDamage(DamageAction action) {
        state.Health.Value -= action.Amount;
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

## Architectural Philosophy
NanoServiceFlow diverges from traditional Redux by embracing **Composition over Inheritance**. Instead of a single global store, state is segregated into modular classes. The `ServiceLocator` acts as a **Mediator**, intercepting dispatched actions and routing them to the correct `[Reducer]` or `[SideEffect]`. This ensures high testability, true decoupling, and an architecture that scales cleanly as your codebase grows.

## License
MIT License. See `LICENSE.md` for more information.