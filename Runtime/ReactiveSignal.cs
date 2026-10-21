// ============================================================================
// Copyright (c) 2026 Daniel Conde Linares
// Licensed under the MIT License. See LICENSE file in the project root.
// ============================================================================

using System;

namespace Aim4code.NanoServiceFlow
{
    /// <summary>
    /// A stream of one-shot events: every <see cref="Publish"/> reaches every subscriber, even when
    /// the payload equals the previous one, and nothing is stored or replayed on subscribe.
    ///
    /// The counterpart of <see cref="ReactiveProperty{T}"/>, which is distinct-until-changed and so
    /// cannot express "the same thing happened twice" — two hits of 2 damage, two heals, a relic
    /// firing again. Use a property for <i>what is</i>, a signal for <i>what just happened</i>.
    /// </summary>
    public class ReactiveSignal<T>
    {
        private Action<T> _listeners;

        /// <summary>Raise the event. Subscribers added or removed while it runs take effect from the next one.</summary>
        public void Publish(T value) => _listeners?.Invoke(value);

        /// <summary>Listen from now on — past events are not replayed. Dispose the result to stop.</summary>
        public IDisposable Subscribe(Action<T> callback)
        {
            _listeners += callback;
            return new Subscription(this, callback);
        }

        private void Unsubscribe(Action<T> callback) => _listeners -= callback;

        private class Subscription : IDisposable
        {
            private ReactiveSignal<T> _signal;
            private readonly Action<T> _callback;

            public Subscription(ReactiveSignal<T> signal, Action<T> callback)
            {
                _signal = signal;
                _callback = callback;
            }

            public void Dispose()
            {
                _signal?.Unsubscribe(_callback);
                _signal = null;   // disposing twice is harmless
            }
        }
    }

    /// <summary>A <see cref="ReactiveSignal{T}"/> with no payload: "it happened".</summary>
    public class ReactiveSignal
    {
        private readonly ReactiveSignal<bool> _inner = new ReactiveSignal<bool>();

        public void Publish() => _inner.Publish(true);

        public IDisposable Subscribe(Action callback) => _inner.Subscribe(_ => callback?.Invoke());
    }
}
