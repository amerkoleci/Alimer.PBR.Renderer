// Copyright © Amer Koleci and Contributors.
// Licensed under the MIT License (MIT). See LICENSE in the repository root for more information.

namespace Alimer.Graphics;

public readonly record struct VertexAttributeDescriptor
{
    public VertexAttributeDescriptor(VertexFormat format, uint offset = 0)
    {
        Format = format;
        Offset = offset;
    }

    public VertexFormat Format { get; init; }
    public uint Offset { get; init; }
}
