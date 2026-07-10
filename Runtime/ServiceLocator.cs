// ============================================================================
// Copyright (c) 2026 Daniel Conde Linares
// Licensed under the MIT License. See LICENSE file in the project root.
// ============================================================================
using System;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;

namespace Aim4code.NanoServiceFlow
{
    public static partial class ServiceLocator
    {
        /// <summary>
        /// A single dispatch target plus the instance that owns it, so handlers
        /// can be removed when their owning service is unregistered/replaced.
        /// </summary>
        private readonly struct HandlerEntry
        {
            public readonly object Owner;
            public readonly Action<IAction> Invoke;

            public HandlerEntry(object owner, Action<IAction> invoke)
            {
                Owner = owner;
                Invoke = invoke;
            }
        }

        // State & Handlers
        private static readonly Dictionary<Type, object> _container = new();
        private static readonly Dictionary<Type, List<HandlerEntry>> _actionHandlers = new();

        // Middleware Pipeline
        private static readonly List<IMiddleware> _middlewares = new();

        /// <summary>
        /// Resets all static state at the start of every play session. This runs even
        /// when "Enter Play Mode Options" disables domain reload, so the locator never
        /// leaks registrations between play sessions in the editor.
        /// </summary>
        [UnityEngine.RuntimeInitializeOnLoadMethod(UnityEngine.RuntimeInitializeLoadType.SubsystemRegistration)]
        private static void ResetStaticState()
        {
            ClearAll();
        }

        /// <summary>True if an instance of <typeparamref name="T"/> is currently registered.</summary>
        public static bool IsRegistered<T>() => _container.ContainsKey(typeof(T));

        public static void RegisterState<T>(T stateInstance) where T : class
        {
            _container[typeof(T)] = stateInstance;
#if UNITY_EDITOR
            EditorNotifyStateRegistered(typeof(T), stateInstance);
#endif
        }

        public static void RegisterService<T>() where T : class
        {
            Type type = typeof(T);

            // Idempotent registration: re-registering replaces the previous instance and
            // drops its handlers instead of appending a second set. Without this, a scene
            // re-entry (e.g. game -> menu -> game) would register the service twice and
            // every dispatched action would fire each reducer twice.
            if (_container.ContainsKey(type))
                Unregister(type);

            var constructor = type.GetConstructors().FirstOrDefault() ?? throw new Exception($"No public constructor found for {type.Name}");

            var parameters = constructor.GetParameters();
            var resolvedArgs = new object[parameters.Length];

            for (int i = 0; i < parameters.Length; i++)
            {
                var paramType = parameters[i].ParameterType;
                if (!_container.TryGetValue(paramType, out var dependency))
                    throw new Exception($"Failed to resolve dependency '{paramType.Name}' for '{type.Name}'.");
                
                resolvedArgs[i] = dependency;
            }

            var instance = (T)constructor.Invoke(resolvedArgs);
            _container[type] = instance;
            
            RegisterHandlers(instance);
#if UNITY_EDITOR
            EditorNotifyStateRegistered(type, instance);
#endif
        }

        public static T Get<T>() => (T)_container[typeof(T)];

        public static void InitializeAll()
        {
            foreach (var instance in _container.Values)
            {
                if (instance is IInitializable initializable)
                {
                    initializable.Initialize();
                }
            }
        }

        public static void AddMiddleware(IMiddleware middleware)
        {
            _middlewares.Add(middleware);
        }

        public static void Dispatch<TAction>(TAction action) where TAction : IAction
        {
#if UNITY_EDITOR
            if (EditorIsProfilerActive)
            {
                var stackTrace = new System.Diagnostics.StackTrace(1, true);
                EditorNotifyDispatchStart(action, stackTrace);
            }
#endif
            // Start the action through the middleware pipeline
            ExecuteMiddleware(0, action);
            
#if UNITY_EDITOR
            if (EditorIsProfilerActive)
            {
                EditorNotifyDispatchEnd(action);
            }
#endif
        }

        private static void ExecuteMiddleware(int index, IAction action)
        {
            if (index < _middlewares.Count)
            {
                // Pass action to current middleware and advance to the next
                _middlewares[index].Invoke(action, nextAction => ExecuteMiddleware(index + 1, nextAction));
            }
            else
            {
                // Pipeline finished, hit the actual Reducers/SideEffects
                ExecuteHandlers(action);
            }
        }

        private static void ExecuteHandlers(IAction action)
        {
            var actionType = action.GetType();
            if (_actionHandlers.TryGetValue(actionType, out var handlers))
            {
                foreach (var handler in handlers)
                {
                    handler.Invoke(action);
                }
            }
        }

        private static void RegisterHandlers(object service)
        {
            var methods = service.GetType().GetMethods(BindingFlags.Public | BindingFlags.Instance);

            foreach (var method in methods)
            {
                bool isReducer = method.GetCustomAttribute<ReducerAttribute>() != null;
                bool isSideEffect = method.GetCustomAttribute<SideEffectAttribute>() != null;
                bool isHandler = isReducer || isSideEffect;

                if (isHandler)
                {
                    var parameters = method.GetParameters();
                    if (parameters.Length == 1 && typeof(IAction).IsAssignableFrom(parameters[0].ParameterType))
                    {
                        var actionType = parameters[0].ParameterType;
                        
                        if (!_actionHandlers.TryGetValue(actionType, out var list))
                        {
                            list = new List<HandlerEntry>();
                            _actionHandlers[actionType] = list;
                        }

                        list.Add(new HandlerEntry(service, action => method.Invoke(service, new[] { action })));
                        
#if UNITY_EDITOR
                        if (!_editorActionHandlers.TryGetValue(actionType, out var editorList))
                        {
                            editorList = new List<EditorHandlerInfo>();
                            _editorActionHandlers[actionType] = editorList;
                        }
                        
                        editorList.Add(new EditorHandlerInfo 
                        { 
                            Target = service, 
                            Method = method, 
                            IsReducer = isReducer 
                        });
#endif
                    }
                }
            }
        }
        
        /// <summary>
        /// Removes a registered service (or state) and any action handlers it owns.
        /// Safe to call when nothing is registered for the type.
        /// </summary>
        public static void UnregisterService<T>() where T : class => Unregister(typeof(T));

        /// <summary>
        /// Removes a registered state. States hold no handlers, so this only clears
        /// the container entry. Provided for symmetry with <see cref="UnregisterService{T}"/>.
        /// </summary>
        public static void UnregisterState<T>() where T : class => Unregister(typeof(T));

        private static void Unregister(Type type)
        {
            if (!_container.TryGetValue(type, out var instance))
                return;

            _container.Remove(type);

            foreach (var handlers in _actionHandlers.Values)
                handlers.RemoveAll(h => ReferenceEquals(h.Owner, instance));

#if UNITY_EDITOR
            foreach (var handlers in _editorActionHandlers.Values)
                handlers.RemoveAll(h => ReferenceEquals(h.Target, instance));
#endif

            // Symmetric teardown: give the instance a chance to release what Initialize()
            // acquired (e.g. subscriptions to external events) so re-registration or
            // scene-scoped unregistration does not leak.
            TryDispose(instance);
        }

        /// <summary>
        /// Helper for testing/resetting state
        /// </summary>
        public static void ClearAll()
        {
            // Dispose before clearing so IDisposable services/states can tear down cleanly.
            // Snapshot first: a Dispose() implementation may touch the locator.
            foreach (var instance in new List<object>(_container.Values))
                TryDispose(instance);

            _container.Clear();
            _actionHandlers.Clear();
            _middlewares.Clear();

#if UNITY_EDITOR
            _editorActionHandlers.Clear();
            EditorNotifyStateCleared();
#endif
        }

        /// <summary>
        /// Disposes an instance if it implements <see cref="IDisposable"/>. Exception-safe:
        /// a throwing Dispose is logged and never aborts the surrounding teardown.
        /// </summary>
        private static void TryDispose(object instance)
        {
            if (instance is IDisposable disposable)
            {
                try
                {
                    disposable.Dispose();
                }
                catch (Exception e)
                {
                    UnityEngine.Debug.LogException(e);
                }
            }
        }


    }
}