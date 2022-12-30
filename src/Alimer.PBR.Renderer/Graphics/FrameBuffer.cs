// Copyright © Amer Koleci and Contributors.
// Licensed under the MIT License (MIT). See LICENSE in the repository root for more information.

using System.Drawing;
using CommunityToolkit.Diagnostics;

namespace Alimer.Graphics;

public abstract class FrameBuffer : GraphicsResource
{
    protected FrameBuffer(GraphicsDevice device, in Size size)
        : base(device)
    {
        Size = size;
    }

    public Size Size { get; }
}
