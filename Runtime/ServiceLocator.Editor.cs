#if UNITY_EDITOR
// ============================================================================
// Copyright (c) 2026 Daniel Conde Linares
// Licensed under the MIT License. See LICENSE file in the project root.
// ============================================================================
using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Reflection;

namespace Aim4code.NanoServiceFlow
{
    public static partial class ServiceLocator
    {
        // ============================================================================
        // EDITOR PROFILER LOGIC
        // This region is purely for the NanoServiceFlow Profiler Tool
        // ============================================================================

        public struct EditorHandlerInfo
        {
            public object Target;
            public MethodInfo Method;
            public bool IsReducer;
        }

        private static readonly Dictionary<Type, List<EditorHandlerInfo>> _editorActionHandlers = new();

        public static bool EditorIsProfilerActive { get; set; }
        public static IReadOnlyDictionary<Type, object> Container => _container;
        public static IReadOnlyList<IMiddleware> Middlewares => _middlewares;
        public static IReadOnlyDictionary<Type, List<EditorHandlerInfo>> EditorActionHandlers => _editorActionHandlers;

        public static event Action<Type, object> OnStateRegistered;
        public static event Action OnStateCleared;
        public static event Action<IAction, StackTrace> OnDispatchStart;
        public static event Action<IAction> OnDispatchEnd;

        private static void EditorNotifyStateRegistered(Type type, object instance)
        {
            OnStateRegistered?.Invoke(type, instance);
        }

        private static void EditorNotifyStateCleared()
        {
            OnStateCleared?.Invoke();
        }

        private static void EditorNotifyDispatchStart(IAction action, StackTrace trace)
        {
            OnDispatchStart?.Invoke(action, trace);
        }

        private static void EditorNotifyDispatchEnd(IAction action)
        {
            OnDispatchEnd?.Invoke(action);
        }
    }
}
#endif
