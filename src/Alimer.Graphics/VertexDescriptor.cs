// Copyright (c) Amer Koleci and Contributors.
// Licensed under the MIT License (MIT). See LICENSE in the repository root for more information.

namespace Alimer.Graphics;

public readonly record struct VertexDescriptor
{
    public VertexDescriptor(params VertexLayoutDescriptor[] layouts)
    {
        Layouts = layouts;
    }

    public VertexLayoutDescriptor[] Layouts { get; init; }
}
