// Copyright © Amer Koleci and Contributors.
// Licensed under the MIT License (MIT). See LICENSE in the repository root for more information.

namespace Alimer.Graphics;

public abstract class GraphicsBuffer : GraphicsResource
{
    protected GraphicsBuffer(GraphicsDevice device, in BufferDescription description)
        : base(device, description.Label)
    {
    }
}
