// Copyright (c) Amer Koleci and Contributors.
// Licensed under the MIT License (MIT). See LICENSE in the repository root for more information.

using System.Numerics;
using System.Runtime.InteropServices;
using Alimer.Graphics;

namespace Alimer.PBR.Renderer;

[StructLayout(LayoutKind.Sequential, Pack = 4)]
public readonly record struct VertexMesh
{
    public static readonly unsafe int SizeInBytes = sizeof(VertexMesh);

    public static readonly VertexAttributeDescriptor[] Attributes =
    [
        new VertexAttributeDescriptor(VertexFormat.Float3, 0),
        new VertexAttributeDescriptor(VertexFormat.Float3, 12),
        new VertexAttributeDescriptor(VertexFormat.Float3, 24),
        new VertexAttributeDescriptor(VertexFormat.Float2, 36)
    ];

    public VertexMesh(in Vector3 position, in Vector3 normal, in Vector3 tangent, in Vector2 texcoord)
    {
        Position = position;
        Normal = normal;
        Tangent = tangent;
        Texcoord = texcoord;
    }

    public readonly Vector3 Position;
    public readonly Vector3 Normal;
    public readonly Vector3 Tangent;
    public readonly Vector2 Texcoord;
}

