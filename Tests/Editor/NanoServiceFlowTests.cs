// ============================================================================
// Copyright (c) 2026 Daniel Conde Linares
// Licensed under the MIT License. See LICENSE file in the project root.
// ============================================================================

using System;
using System.Text.RegularExpressions;
using NUnit.Framework;
using UnityEngine;
using UnityEngine.TestTools;

namespace Aim4code.NanoServiceFlow.Tests.Editor
{
    public class NanoServiceFlowTests
    {
        public class MockState
        {
            public ReactiveProperty<int> Score { get; } = new(0);
        }

        public readonly struct AddScoreAction : IAction 
        {
            public readonly int Amount;
            public AddScoreAction(int amount) => Amount = amount;
        }

        public class MockService : IInitializable
        {
            private readonly MockState _state;
            public bool WasInitialized { get; private set; }

            // Constructor injection (State should be provided by the Locator)
            public MockService(MockState state)
            {
                _state = state;
            }

            public void Initialize()
            {
                WasInitialized = true;
            }

            [Reducer]
            public void OnAddScore(AddScoreAction action)
            {
                _state.Score.Value += action.Amount;
            }
        }

        public class DisposableMockService : IInitializable, IDisposable
        {
            public int InitializeCount { get; private set; }
            public int DisposeCount { get; private set; }
            public bool WasInitialized => InitializeCount > 0;

            public void Initialize() => InitializeCount++;
            public void Dispose() => DisposeCount++;
        }

        public class ThrowingDisposableService : IDisposable
        {
            public void Dispose() => throw new InvalidOperationException("boom");
        }

        [TearDown]
        public void TearDown()
        {
            // Crucial: Clear the static locator after every test to ensure isolation
            ServiceLocator.ClearAll();
        }

        [Test]
        public void ServiceLocator_ResolvesDependencies_Successfully()
        {
            // Arrange
            var state = new MockState();
            ServiceLocator.RegisterState(state);

            // Act
            ServiceLocator.RegisterService<MockService>();
            var resolvedService = ServiceLocator.Get<MockService>();

            // Assert
            Assert.IsNotNull(resolvedService, "Service should be registered and retrievable.");
        }

        [Test]
        public void ServiceLocator_InitializeAll_CallsIInitializable()
        {
            // Arrange
            ServiceLocator.RegisterState(new MockState());
            ServiceLocator.RegisterService<MockService>();

            // Act
            ServiceLocator.InitializeAll();
            var service = ServiceLocator.Get<MockService>();

            // Assert
            Assert.IsTrue(service.WasInitialized, "Initialize() should be called during Phase 2 Boot.");
        }

        [Test]
        public void ActionDispatch_RoutesToReducer_AndMutatesState()
        {
            // Arrange
            var state = new MockState();
            ServiceLocator.RegisterState(state);
            ServiceLocator.RegisterService<MockService>();

            // Act
            ServiceLocator.Dispatch(new AddScoreAction(10));
            ServiceLocator.Dispatch(new AddScoreAction(5));

            // Assert
            Assert.AreEqual(15, state.Score.Value, "Reducer should have accumulated 15 points.");
        }

        [Test]
        public void RegisterService_CalledTwice_DoesNotDoubleInvokeReducer()
        {
            // Arrange: register once, then again to simulate a scene re-entry
            // (e.g. game -> menu -> game) hitting the same RegisterService call.
            var state = new MockState();
            ServiceLocator.RegisterState(state);
            ServiceLocator.RegisterService<MockService>();
            ServiceLocator.RegisterService<MockService>();

            // Act
            ServiceLocator.Dispatch(new AddScoreAction(10));

            // Assert
            Assert.AreEqual(10, state.Score.Value,
                "Re-registering a service must replace its handlers, not append a duplicate set.");
        }

        [Test]
        public void UnregisterService_RemovesHandlers_AndContainerEntry()
        {
            // Arrange
            var state = new MockState();
            ServiceLocator.RegisterState(state);
            ServiceLocator.RegisterService<MockService>();

            // Act
            ServiceLocator.UnregisterService<MockService>();
            ServiceLocator.Dispatch(new AddScoreAction(10));

            // Assert
            Assert.IsFalse(ServiceLocator.IsRegistered<MockService>(),
                "Service should no longer be registered after UnregisterService.");
            Assert.AreEqual(0, state.Score.Value,
                "An unregistered service must not receive dispatched actions.");
        }

        [Test]
        public void UnregisterService_WhenNotRegistered_IsNoOp()
        {
            // Should not throw when nothing is registered for the type.
            Assert.DoesNotThrow(() => ServiceLocator.UnregisterService<MockService>());
        }

        [Test]
        public void InitializeAll_CalledRepeatedly_InitializesEachInstanceOnce()
        {
            // Arrange
            ServiceLocator.RegisterService<DisposableMockService>();

            // Act: a per-scene bootstrap calls InitializeAll() on every scene load.
            ServiceLocator.InitializeAll();
            ServiceLocator.InitializeAll();
            ServiceLocator.InitializeAll();

            // Assert
            Assert.AreEqual(1, ServiceLocator.Get<DisposableMockService>().InitializeCount,
                "A persistent service must be initialized once, not once per InitializeAll() call.");
        }

        [Test]
        public void InitializeAll_AfterReRegistration_InitializesTheFreshInstance()
        {
            // Arrange: initialize, then re-register (replaces with a new instance).
            ServiceLocator.RegisterService<DisposableMockService>();
            ServiceLocator.InitializeAll();
            ServiceLocator.RegisterService<DisposableMockService>();
            var fresh = ServiceLocator.Get<DisposableMockService>();

            // Act
            ServiceLocator.InitializeAll();

            // Assert
            Assert.AreEqual(1, fresh.InitializeCount,
                "A re-registered instance is a new object and must be initialized exactly once.");
        }

        [Test]
        public void UnregisterService_DisposesService()
        {
            // Arrange
            ServiceLocator.RegisterService<DisposableMockService>();
            var service = ServiceLocator.Get<DisposableMockService>();

            // Act
            ServiceLocator.UnregisterService<DisposableMockService>();

            // Assert
            Assert.AreEqual(1, service.DisposeCount,
                "Unregistering an IDisposable service should dispose it exactly once.");
        }

        [Test]
        public void RegisterService_CalledTwice_DisposesPreviousInstance()
        {
            // Arrange: registering again (e.g. a scene re-entry) replaces the instance.
            ServiceLocator.RegisterService<DisposableMockService>();
            var first = ServiceLocator.Get<DisposableMockService>();

            // Act
            ServiceLocator.RegisterService<DisposableMockService>();
            var second = ServiceLocator.Get<DisposableMockService>();

            // Assert
            Assert.AreEqual(1, first.DisposeCount,
                "Re-registering must dispose the previous instance so it can release its subscriptions.");
            Assert.AreNotSame(first, second, "Re-registering should create a fresh instance.");
            Assert.AreEqual(0, second.DisposeCount, "The current instance must not be disposed.");
        }

        [Test]
        public void ClearAll_DisposesRegisteredInstances()
        {
            // Arrange
            ServiceLocator.RegisterService<DisposableMockService>();
            var service = ServiceLocator.Get<DisposableMockService>();

            // Act
            ServiceLocator.ClearAll();

            // Assert
            Assert.AreEqual(1, service.DisposeCount,
                "ClearAll should dispose every IDisposable instance before clearing the container.");
        }

        [Test]
        public void Dispose_ThatThrows_IsLoggedAndDoesNotAbortTeardown()
        {
            // Arrange
            ServiceLocator.RegisterService<ThrowingDisposableService>();
            LogAssert.Expect(LogType.Exception, new Regex("boom"));

            // Act & Assert
            Assert.DoesNotThrow(() => ServiceLocator.UnregisterService<ThrowingDisposableService>(),
                "A throwing Dispose must be swallowed, not propagated to the caller.");
            Assert.IsFalse(ServiceLocator.IsRegistered<ThrowingDisposableService>(),
                "The instance must still be removed even if its Dispose threw.");
        }

        [Test]
        public void ReactiveProperty_TriggersCallback_WhenValueChanges()
        {
            // Arrange
            var state = new MockState();
            int notifiedValue = 0;
            int invocationCount = 0;

            // Act
            var subscription = state.Score.Subscribe(val => 
            {
                notifiedValue = val;
                invocationCount++;
            });

            // Mutate the value
            state.Score.Value = 100;
            
            // Mutate to the SAME value (Should be ignored by DistinctUntilChanged)
            state.Score.Value = 100; 

            // Assert
            Assert.AreEqual(100, notifiedValue, "Subscriber should receive the updated value.");
            Assert.AreEqual(2, invocationCount, "Should trigger exactly twice: once immediately on subscribe, once on change.");
            
            // Cleanup
            subscription.Dispose();
        }

        [Test]
        public void ReactiveSignal_FiresOnEveryPublish_EvenWithTheSameValue()
        {
            var signal = new ReactiveSignal<int>();
            int count = 0, last = 0;
            var subscription = signal.Subscribe(value => { count++; last = value; });

            signal.Publish(2);
            signal.Publish(2);

            Assert.AreEqual(2, count, "A signal is not distinct-until-changed: two hits of 2 are two events.");
            Assert.AreEqual(2, last);
            subscription.Dispose();
        }

        [Test]
        public void ReactiveSignal_DoesNotReplayOnSubscribe()
        {
            var signal = new ReactiveSignal<int>();
            signal.Publish(7);

            int count = 0;
            var subscription = signal.Subscribe(_ => count++);

            Assert.AreEqual(0, count, "Past events are not replayed to new subscribers.");
            subscription.Dispose();
        }

        [Test]
        public void ReactiveSignal_DisposeStopsDelivery_AndIsIdempotent()
        {
            var signal = new ReactiveSignal<int>();
            int count = 0;
            var subscription = signal.Subscribe(_ => count++);

            signal.Publish(1);
            subscription.Dispose();
            subscription.Dispose();
            signal.Publish(1);

            Assert.AreEqual(1, count);
        }

        [Test]
        public void ReactiveSignal_UnsubscribingDuringPublish_FinishesTheCurrentEvent()
        {
            var signal = new ReactiveSignal<int>();
            int first = 0, second = 0;
            IDisposable firstSubscription = null;
            firstSubscription = signal.Subscribe(_ => { first++; firstSubscription.Dispose(); });
            var secondSubscription = signal.Subscribe(_ => second++);

            signal.Publish(1);
            signal.Publish(1);

            Assert.AreEqual(1, first, "A subscriber that leaves during an event is gone from the next one.");
            Assert.AreEqual(2, second, "The others still receive both events.");
            secondSubscription.Dispose();
        }

        [Test]
        public void ReactiveSignal_WithoutPayload_FiresEachTime()
        {
            var signal = new ReactiveSignal();
            int count = 0;
            var subscription = signal.Subscribe(() => count++);

            signal.Publish();
            signal.Publish();

            Assert.AreEqual(2, count);
            subscription.Dispose();
        }
    }
}
