// ============================================================================
// Copyright (c) 2026 Daniel Conde Linares
// Licensed under the MIT License. See LICENSE file in the project root.
// ============================================================================

using System;
using NUnit.Framework;

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
    }
}