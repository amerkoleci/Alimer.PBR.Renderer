﻿// Copyright (c) Amer Koleci and Contributors.
// Licensed under the MIT License (MIT). See LICENSE in the repository root for more information.

namespace Alimer.Graphics;

[Flags]
public enum TextureUsage
{
    /// <summary>
    /// None usage.
    /// </summary>
    None = 0,
    /// <summary>
    /// Supports shader read access.
    /// </summary>
    ShaderRead = 1 << 0,
    /// <summary>
    /// Supports write read access.
    /// </summary>
    ShaderWrite = 1 << 1,
    ShaderReadWrite = ShaderRead | ShaderWrite,
    RenderTarget = 1 << 2,
}
