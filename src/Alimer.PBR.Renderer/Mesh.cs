// Copyright (c) Amer Koleci and Contributors
// Licensed under the MIT License (MIT). See LICENSE in the repository root for more information.

using System.Numerics;
using Alimer.Graphics;
using CommunityToolkit.Diagnostics;
using Silk.NET.Assimp;

namespace Alimer.PBR.Renderer;

public sealed class Mesh : GraphicsObject
{
    private static readonly Assimp _assImp;
    private static readonly PostProcessSteps s_postProcessSteps =
        PostProcessSteps.FindDegenerates
        | PostProcessSteps.FindInvalidData
        //| PostProcessSteps.FlipUVs               // Required for Direct3D
        | PostProcessSteps.FlipWindingOrder
        | PostProcessSteps.JoinIdenticalVertices
        | PostProcessSteps.ImproveCacheLocality
        | PostProcessSteps.OptimizeMeshes
        | PostProcessSteps.Triangulate
        | PostProcessSteps.PreTransformVertices
        | PostProcessSteps.GenerateNormals
        | PostProcessSteps.CalculateTangentSpace
        // | PostProcessSteps.GenerateUVCoords
        // | PostProcessSteps.SortByPrimitiveType
        // | PostProcessSteps.Debone
        ;

    static Mesh()
    {
        _assImp = Assimp.GetApi();
    }


    private Mesh(GraphicsDevice graphicsDevice, List<VertexMesh> vertices, uint[] indices)
    {
        VertexBuffer = AddDisposable(graphicsDevice.CreateBuffer(vertices.ToArray(), BufferUsage.Vertex));
        IndexCount = indices.Length;

        IndexType = vertices.Count > 65536 ? IndexType.Uint32 : IndexType.Uint16;
        if (IndexType == IndexType.Uint32)
        {
            IndexBuffer = AddDisposable(graphicsDevice.CreateBuffer(indices.ToArray(), BufferUsage.Index));
        }
        else
        {
            Span<ushort> shortIndices = stackalloc ushort[indices.Length];
            for (int i = 0; i < indices.Length; ++i)
            {
                shortIndices[i] = (ushort)indices[i];
            }

            IndexBuffer = AddDisposable(graphicsDevice.CreateBuffer(shortIndices, BufferUsage.Index));
        }
    }

    public readonly GraphicsBuffer VertexBuffer;
    public readonly GraphicsBuffer IndexBuffer;
    public readonly int IndexCount;
    public readonly IndexType IndexType;

    public static Mesh FromFile(GraphicsDevice graphicsDevice, string filePath)
    {
        using FileStream stream = new(filePath, FileMode.Open);
        return FromStream(graphicsDevice, stream);
    }

    public static Mesh FromGltf(GraphicsDevice graphicsDevice, string filePath)
    {
        using FileStream stream = new(filePath, FileMode.Open);
        return FromGltfStream(graphicsDevice, stream);
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

    public static Mesh FromGltfStream(GraphicsDevice graphicsDevice, Stream stream)
    {
        var sharpModel = SharpGLTF.Schema2.ModelRoot.ReadGLB(stream);

        // Process materials
        foreach (SharpGLTF.Schema2.Material material in sharpModel.LogicalMaterials)
        {
        }

        List<VertexMesh> vertices = new();
        uint[] indices = Array.Empty<uint>();
        foreach (var mesh in sharpModel.LogicalMeshes)
        {
            foreach (var primitive in mesh.Primitives)
            {
                bool hasPosition = primitive.GetVertexAccessor("POSITION") is not null;
                bool hasNormal = primitive.GetVertexAccessor("NORMAL") is not null;
                bool hasTangent = primitive.GetVertexAccessor("TANGENT") is not null;
                bool hasTexCoord0 = primitive.GetVertexAccessor("TEXCOORD_0") is not null;

                Guard.IsTrue(hasPosition);
                Guard.IsTrue(hasNormal);
                Guard.IsTrue(hasTexCoord0);

                IList<Vector3> positionAccessor = primitive.GetVertexAccessor("POSITION").AsVector3Array();
                IList<Vector3> normalAccessor = primitive.GetVertexAccessor("NORMAL").AsVector3Array();
                IList<Vector3>? tangentAccessor = hasTangent ? primitive.GetVertexAccessor("TANGENT").AsVector3Array() : default;
                IList<Vector2> texcoordAccessor = primitive.GetVertexAccessor("TEXCOORD_0").AsVector2Array();
                var indexAccessor = primitive.GetIndexAccessor().AsIndicesArray();

                if (!hasTangent)
                {
                    Span<Vector4> tangents = new Vector4[positionAccessor.Count];
                    VertexHelper.GenerateTangents(tangents,
                        positionAccessor.ToArray(),
                        texcoordAccessor.ToArray(),
                        indexAccessor.ToArray());
                    tangentAccessor = new List<Vector3>();
                    for (int i = 0; i < positionAccessor.Count; ++i)
                    {
                        tangentAccessor.Add(new Vector3(tangents[i].X, tangents[i].Y, tangents[i].Z));
                    }
                }

                for (int i = 0; i < positionAccessor.Count; ++i)
                {
                    Vector3 position = positionAccessor[i];
                    Vector3 normal = normalAccessor[i];
                    Vector3 tangent = tangentAccessor[i]!;
                    Vector2 texcoord = texcoordAccessor[i];

                    vertices.Add(new VertexMesh(position, normal, tangent, texcoord));
                }


                // Indices
                indices = new uint[indexAccessor.Count];

                for (int i = 0; i < indices.Length; i++)
                {
                    indices[i] = indexAccessor[i];
                }

                // TODO: Material
                //primitive.Material;
            }
        }

        return new(graphicsDevice, vertices, indices);
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
        List<uint> indices = new((int)mesh->MNumFaces);

        for (int i = 0; i < (int)mesh->MNumVertices; ++i)
        {
            Vector3 position = mesh->MVertices[i];
            Vector3 normal = mesh->MNormals[i];
            Vector3 tangent = Vector3.Zero;
            Vector2 texcoord = Vector2.Zero;

            if (hasTangentsAndBitangents)
            {
                tangent = mesh->MTangents[i];
            }

            if (hasHasTexCoords0)
            {
                texcoord = new Vector2(mesh->MTextureCoords[0][i].X, 1.0f - mesh->MTextureCoords[0][i].Y);
            }

            vertices.Add(new VertexMesh(position, normal, tangent, texcoord));
        }

        for (int i = 0; i < (int)mesh->MNumFaces; ++i)
        {
            Guard.IsTrue(mesh->MFaces[i].MNumIndices == 3);
            indices.Add(mesh->MFaces[i].MIndices[0]);
            indices.Add(mesh->MFaces[i].MIndices[1]);
            indices.Add(mesh->MFaces[i].MIndices[2]);
        }

        return new(graphicsDevice, vertices, indices.ToArray());
    }
}
