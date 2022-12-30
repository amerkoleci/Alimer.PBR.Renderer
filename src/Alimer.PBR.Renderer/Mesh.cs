// Copyright © Amer Koleci and Contributors.
// Licensed under the MIT License (MIT). See LICENSE in the repository root for more information.

using System.Numerics;
using Alimer.Graphics;
using CommunityToolkit.Diagnostics;
using Silk.NET.Assimp;

namespace Alimer.PBR.Renderer;

public sealed class Mesh : GraphicsObject
{
    private static readonly Assimp _assImp;
    private static readonly PostProcessSteps s_postProcessSteps = PostProcessSteps.CalculateTangentSpace
        | PostProcessSteps.Triangulate
        | PostProcessSteps.SortByPrimitiveType
        | PostProcessSteps.PreTransformVertices
        | PostProcessSteps.GenerateNormals
        | PostProcessSteps.GenerateUVCoords
        | PostProcessSteps.OptimizeMeshes
        | PostProcessSteps.Debone
        | PostProcessSteps.ValidateDataStructure;

    static Mesh()
    {
        _assImp = Assimp.GetApi();
    }


    private Mesh(GraphicsDevice graphicsDevice, List<VertexMesh> vertices, List<ushort> indices)
    {
        VertexBuffer = AddDisposable(graphicsDevice.CreateBuffer(vertices.ToArray(), BufferUsage.Vertex));
        IndexBuffer = AddDisposable(graphicsDevice.CreateBuffer(indices.ToArray(), BufferUsage.Index));
        IndexCount = indices.Count;
    }

    public readonly GraphicsBuffer VertexBuffer;
    public readonly GraphicsBuffer IndexBuffer;
    public readonly int IndexCount;

    public static Mesh FromFile(GraphicsDevice graphicsDevice, string filePath)
    {
        using FileStream stream = new(filePath, FileMode.Open);
        return FromStream(graphicsDevice, stream);
    }

    public static Mesh FromStream(GraphicsDevice graphicsDevice, Stream stream)
    {
        if (stream is MemoryStream memoryStream)
            return FromMemory(graphicsDevice, memoryStream.ToArray());

        using (MemoryStream newStream = new())
        {
            stream.CopyTo(newStream);
            return FromMemory(graphicsDevice, newStream.ToArray());
        }
    }

    public static unsafe Mesh FromMemory(GraphicsDevice graphicsDevice, byte[] data)
    {
        Scene* scene = _assImp.ImportFileFromMemory(data.AsSpan(), (uint)data.Length, (uint)s_postProcessSteps, (byte*)null);

        if (scene is null || scene->MFlags == (uint)SceneFlags.Incomplete || scene->MRootNode is null)
        {
            throw new InvalidDataException(_assImp.GetErrorStringS());
        }

        Silk.NET.Assimp.Mesh* mesh = scene->MMeshes[0];
        Guard.IsTrue(mesh->MVertices is not null);
        Guard.IsTrue(mesh->MNormals is not null);

        bool hasTangentsAndBitangents = mesh->MTangents is not null;
        bool hasHasTexCoords0 = mesh->MTextureCoords[0] is not null;
        List<VertexMesh> vertices = new((int)mesh->MNumVertices);
        List<ushort> indices = new((int)mesh->MNumFaces);

        for (int i = 0; i < (int)mesh->MNumVertices; ++i)
        {
            Vector3 position = mesh->MVertices[i];
            Vector3 normal = mesh->MNormals[i];
            Vector3 tangent = Vector3.Zero;
            Vector3 bitangent = Vector3.Zero;
            Vector2 texcoord = Vector2.Zero;

            if (hasTangentsAndBitangents)
            {
                tangent = mesh->MTangents[i];
                bitangent = mesh->MBitangents[i];
            }

            if (hasHasTexCoords0)
            {
                texcoord = new Vector2(mesh->MTextureCoords[0][i].X, mesh->MTextureCoords[0][i].Y);
            }

            vertices.Add(new VertexMesh(position, normal, tangent, bitangent, texcoord));
        }

        for (int i = 0; i < (int)mesh->MNumFaces; ++i)
        {
            Guard.IsTrue(mesh->MFaces[i].MNumIndices == 3);
            indices.Add((ushort)mesh->MFaces[i].MIndices[0]);
            indices.Add((ushort)mesh->MFaces[i].MIndices[1]);
            indices.Add((ushort)mesh->MFaces[i].MIndices[2]);
        }

        return new(graphicsDevice, vertices, indices);
    }
}
