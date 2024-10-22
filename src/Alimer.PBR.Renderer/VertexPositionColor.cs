// Copyright (c) Amer Koleci and Contributors.
// Licensed under the MIT License (MIT). See LICENSE in the repository root for more information.

using System.Numerics;
using System.Runtime.InteropServices;
using Alimer.Graphics;
using Vortice.Mathematics;

namespace Alimer.PBR.Renderer;

[StructLayout(LayoutKind.Sequential, Pack = 4)]
public readonly record struct VertexPositionColor
{
    public static readonly unsafe int SizeInBytes = sizeof(VertexPositionColor);

    public static readonly VertexAttributeDescriptor[] Attributes =
    [
        new VertexAttributeDescriptor(VertexFormat.Float3, 0),
        new VertexAttributeDescriptor(VertexFormat.Float4, 12)
    ];

    public VertexPositionColor(in Vector3 position, in Color4 color)
    {
        Position = position;
        Color = color;
    }

    public readonly Vector3 Position;
    public readonly Color4 Color;
}

