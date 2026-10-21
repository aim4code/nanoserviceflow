// ============================================================================
// Copyright (c) 2026 Daniel Conde Linares
// Licensed under the MIT License. See LICENSE file in the project root.
// ============================================================================
using System;
using System.Collections.Generic;

namespace Aim4code.NanoServiceFlow
{
    /// <summary>
    /// A value that notifies its subscribers when it changes (distinct-until-changed).
    ///
    /// Use it for <i>what is</i> ("health is 3"). For <i>what just happened</i>, including the same
    /// event twice in a row, use <see cref="ReactiveSignal{T}"/>.
    /// </summary>
    public class ReactiveProperty<T>
    {
        private T _value;
        private Action<T> _onValueChanged;

        /// <summary>
        /// The current value. Setting a different value notifies every subscriber; setting an equal
        /// value (by <see cref="EqualityComparer{T}.Default"/>) does nothing.
        /// </summary>
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

        /// <summary>
        /// Listen for changes. The callback runs immediately with the current value, then on every change.
        /// Subscribers added or removed during a notification take effect from the next one.
        /// Dispose the result to stop; disposing twice is harmless.
        /// </summary>
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
            private ReactiveProperty<T> _property;
            private readonly Action<T> _callback;

            public Subscription(ReactiveProperty<T> property, Action<T> callback)
            {
                _property = property;
                _callback = callback;
            }

            public void Dispose()
            {
                _property?.Unsubscribe(_callback);
                _property = null;   // disposing twice is harmless
            }
        }
    }
}
