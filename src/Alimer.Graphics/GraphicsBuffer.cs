// Copyright (c) Amer Koleci and Contributors.
// Licensed under the MIT License (MIT). See LICENSE in the repository root for more information.

namespace Alimer.Graphics;

public abstract class GraphicsBuffer : GraphicsResource
{
    protected GraphicsBuffer(GraphicsDevice device, in BufferDescription description)
        : base(device, description.Label)
    {
        Usage = description.Usage;
        Size = description.Size;
    }


    /// <summary>
    /// Gets the <see cref="BufferUsage"/>.
    /// </summary>
    public BufferUsage Usage { get; }

    /// <summary>
    /// Gets the size in bytes of the buffer.
    /// </summary>
    public ulong Size { get; }
}
