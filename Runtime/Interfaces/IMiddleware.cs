// ============================================================================
// Copyright (c) 2026 Daniel Conde Linares
// Licensed under the MIT License. See LICENSE file in the project root.
// ============================================================================
using System;

namespace Aim4code.NanoR3dux
{ 
    // A delegate representing the next step in the pipeline
    public delegate void NextActionDelegate(IAction action);

    public interface IMiddleware
    {
        // The middleware decides when (or if) to call 'next'
        void Invoke(IAction action, NextActionDelegate next);
    }
}