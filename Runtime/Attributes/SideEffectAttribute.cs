// ============================================================================
// Copyright (c) 2026 Daniel Conde Linares
// Licensed under the MIT License. See LICENSE file in the project root.
// ============================================================================
using System;

namespace Aim4code.NanoServiceFlow
{
    [AttributeUsage(AttributeTargets.Method)]
    public class SideEffectAttribute : Attribute { }
}