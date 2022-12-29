// Copyright © Amer Koleci and Contributors.
// Licensed under the MIT License (MIT). See LICENSE in the repository root for more information.

namespace Alimer.Graphics;

public record struct DepthStencilState
{
    public DepthStencilState()
    {
        DepthWriteEnabled = true;
        DepthCompare = CompareFunction.Less;
    }

    public bool DepthWriteEnabled { get; set; }
    public CompareFunction DepthCompare { get; set; }
}
