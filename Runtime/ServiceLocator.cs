// ============================================================================
// Copyright (c) 2026 aim4code
// Licensed under the MIT License. See LICENSE file in the project root.
// ============================================================================
using System;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;

namespace Aim4code.NanoR3dux
{
    public static class ServiceLocator
    {
        // State & Handlers
        private static readonly Dictionary<Type, object> _container = new();
        private static readonly Dictionary<Type, List<Action<IAction>>> _actionHandlers = new();
        
        // Middleware Pipeline
        private static readonly List<IMiddleware> _middlewares = new();

        public static void RegisterState<T>(T stateInstance) where T : class
        {
            _container[typeof(T)] = stateInstance;
        }
        
        public static void RegisterService<T>() where T : class
        {
            Type type = typeof(T);
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
            // Start the action through the middleware pipeline
            ExecuteMiddleware(0, action);
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
                    handler(action);
                }
            }
        }

        private static void RegisterHandlers(object service)
        {
            var methods = service.GetType().GetMethods(BindingFlags.Public | BindingFlags.Instance);

            foreach (var method in methods)
            {
                bool isHandler = method.GetCustomAttribute<ReducerAttribute>() != null || 
                                 method.GetCustomAttribute<SideEffectAttribute>() != null;

                if (isHandler)
                {
                    var parameters = method.GetParameters();
                    if (parameters.Length == 1 && typeof(IAction).IsAssignableFrom(parameters[0].ParameterType))
                    {
                        var actionType = parameters[0].ParameterType;
                        
                        if (!_actionHandlers.TryGetValue(actionType, out var list))
                        {
                            list = new List<Action<IAction>>();
                            _actionHandlers[actionType] = list;
                        }

                        list.Add(action => method.Invoke(service, new[] { action }));
                    }
                }
            }
        }
        
        /// <summary>
        /// Helper for testing/resetting state
        /// </summary>
        public static void ClearAll()
        {
            _container.Clear();
            _actionHandlers.Clear();
            _middlewares.Clear();
        }
    }
}