// ============================================================================
// Copyright (c) 2026 Daniel Conde Linares
// Licensed under the MIT License. See LICENSE file in the project root.
// ============================================================================
using System;
using System.Collections.Generic;

namespace Aim4code.NanoR3dux
{
    public class ReactiveProperty<T>
    {
        private T _value;
        private Action<T> _onValueChanged;

        public T Value
        {
            get => _value;
            set
            {
                // Only trigger if the value actually changed (DistinctUntilChanged)
                if (EqualityComparer<T>.Default.Equals(_value, value)) return;

                _value = value;
                _onValueChanged?.Invoke(_value);
            }
        }

        public ReactiveProperty(T initialValue = default)
        {
            _value = initialValue;
        }

        // Returns an IDisposable so you can easily un-subscribe
        public IDisposable Subscribe(Action<T> callback)
        {
            _onValueChanged += callback;

            // Instantly fire with the current value upon subscription
            callback?.Invoke(_value);

            return new Subscription(this, callback);
        }

        private void Unsubscribe(Action<T> callback)
        {
            _onValueChanged -= callback;
        }

        // --- Helper class to handle cleanups ---
        private class Subscription : IDisposable
        {
            private readonly ReactiveProperty<T> _property;
            private readonly Action<T> _callback;

            public Subscription(ReactiveProperty<T> property, Action<T> callback)
            {
                _property = property;
                _callback = callback;
            }

            public void Dispose()
            {
                _property.Unsubscribe(_callback);
            }
        }
    }
}