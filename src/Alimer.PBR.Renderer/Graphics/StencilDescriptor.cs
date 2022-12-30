// Copyright © Amer Koleci and Contributors.
// Licensed under the MIT License (MIT). See LICENSE in the repository root for more information.

namespace Alimer.Graphics;

public readonly record struct StencilDescriptor
{
    public StencilDescriptor()
    {
        StencilFailureOperation = StencilOperation.Keep;
        DepthFailureOperation = StencilOperation.Keep;
        DepthStencilPassOperation = StencilOperation.Keep;
        StencilCompareFunction = CompareFunction.Always;
    }

    public CompareFunction StencilCompareFunction { get; init; }
    public StencilOperation StencilFailureOperation { get; init; }
    public StencilOperation DepthFailureOperation { get; init; }
    public StencilOperation DepthStencilPassOperation { get; init; }
}
