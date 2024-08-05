// Copyright (c) Amer Koleci and Contributors.
// Licensed under the MIT License (MIT). See LICENSE in the repository root for more information.

using System.Runtime.InteropServices;
using CommunityToolkit.Diagnostics;

namespace Alimer.Graphics;

public readonly struct GraphicsDeviceLimits
{
    public uint MaxTextureDimension1D {get; init;}
    public uint MaxTextureDimension2D {get; init;}
    public uint MaxTextureDimension3D { get; init; }
    public uint MaxTextureDimensionCube { get; init; }
    public uint MaxTextureArrayLayers { get; init; }

    public ulong MaxBufferSize { get; init; }
    public uint MinConstantBufferOffsetAlignment { get; init; }
    public ulong MaxConstantBufferBindingSize { get; init; }
    public uint MinStorageBufferOffsetAlignment { get; init; }
    public ulong MaxStorageBufferBindingSize { get; init; }

}
