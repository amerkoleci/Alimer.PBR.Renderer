// Copyright © Amer Koleci and Contributors.
// Licensed under the MIT License (MIT). See LICENSE in the repository root for more information.

using System.Numerics;
using System.Runtime.CompilerServices;

namespace Alimer.PBR.Renderer;

public static class VertexHelper
{
    public static Span<Vector4> GenerateTangents(
        Span<Vector3> positions,
        Span<Vector2> texcoords,
        Span<uint> indices)
    {
        Span<Vector4> tangentBuffer = new Vector4[positions.Length];

        int indexCount = indices.IsEmpty
            ? positions.Length / Unsafe.SizeOf<Vector3>()
            : indices.Length;

        for (int i = 0; i < indexCount; i += 3)
        {
            int index1 = i + 0;
            int index2 = i + 1;
            int index3 = i + 2;

            if (!indices.IsEmpty)
            {
                index1 = (int)indices[index1];
                index2 = (int)indices[index2];
                index3 = (int)indices[index3];
            }

            Vector3 position1 = positions[index1];
            Vector3 position2 = positions[index2];
            Vector3 position3 = positions[index3];

            Vector2 uv1 = texcoords[index1];
            Vector2 uv2 = texcoords[index2];
            Vector2 uv3 = texcoords[index3];

            Vector3 edge1 = position2 - position1;
            Vector3 edge2 = position3 - position1;

            Vector2 uvEdge1 = uv2 - uv1;
            Vector2 uvEdge2 = uv3 - uv1;

            float dR = uvEdge1.X * uvEdge2.Y - uvEdge2.X * uvEdge1.Y;

            if (Math.Abs(dR) < 1e-6f)
            {
                dR = 1.0f;
            }

            float r = 1.0f / dR;
            Vector3 t = (uvEdge2.Y * edge1 - uvEdge1.Y * edge2) * r;

            tangentBuffer[index1] += new Vector4(t, 0.0f);
            tangentBuffer[index2] += new Vector4(t, 0.0f);
            tangentBuffer[index3] += new Vector4(t, 0.0f);
        }

        for (int i = 0; i < tangentBuffer.Length; i++)
        {
            tangentBuffer[i].W = 1.0f;
        }

        return tangentBuffer;
    }
}

