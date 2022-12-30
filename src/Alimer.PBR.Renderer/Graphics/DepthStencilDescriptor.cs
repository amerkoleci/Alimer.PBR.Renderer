// Copyright © Amer Koleci and Contributors.
// Licensed under the MIT License (MIT). See LICENSE in the repository root for more information.

namespace Alimer.Graphics;

public readonly record struct DepthStencilDescriptor
{
    public DepthStencilDescriptor()
    {
        DepthWriteEnabled = true;
        DepthCompare = CompareFunction.Less;
        StencilReadMask = 0xFF;
        StencilWriteMask = 0xFF;

        FrontFaceStencil = new();
        BackFaceStencil = new();
    }

    public bool DepthWriteEnabled { get; init; }
    public CompareFunction DepthCompare { get; init; }

    public byte StencilReadMask { get; init; }
    public byte StencilWriteMask { get; init; }

    public StencilDescriptor FrontFaceStencil { get; init; }
    public StencilDescriptor BackFaceStencil { get; init; }
}
