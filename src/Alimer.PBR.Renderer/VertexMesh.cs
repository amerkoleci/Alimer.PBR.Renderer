﻿// Copyright © Amer Koleci and Contributors.
// Licensed under the MIT License (MIT). See LICENSE in the repository root for more information.

using System.Numerics;
using System.Runtime.InteropServices;
using Alimer.Graphics;
using Vortice.Mathematics;

namespace Alimer.PBR.Renderer;

[StructLayout(LayoutKind.Sequential, Pack = 4)]
public readonly record struct VertexMesh
{
    public static readonly unsafe int SizeInBytes = sizeof(VertexMesh);

    public static readonly VertexAttributeDescriptor[] Attributes = new[]
    {
        new VertexAttributeDescriptor(VertexFormat.Float32x3, 0),
        new VertexAttributeDescriptor(VertexFormat.Float32x3, 12),
        new VertexAttributeDescriptor(VertexFormat.Float32x3, 24),
        new VertexAttributeDescriptor(VertexFormat.Float32x3, 36),
        new VertexAttributeDescriptor(VertexFormat.Float32x2, 48)
    };

    public VertexMesh(in Vector3 position, in Vector3 normal, in Vector3 tangent, in Vector3 bitangent, in Vector2 texcoord)
    {
        Position = position;
        Normal = normal;
        Tangent = tangent;
        Bitangent = bitangent;
        Texcoord = texcoord;
    }

    public readonly Vector3 Position;
    public readonly Vector3 Normal;
    public readonly Vector3 Tangent;
    public readonly Vector3 Bitangent;
    public readonly Vector2 Texcoord;
}
