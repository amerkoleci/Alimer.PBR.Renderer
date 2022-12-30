// Copyright © Amer Koleci and Contributors.
// Licensed under the MIT License (MIT). See LICENSE in the repository root for more information.

namespace Alimer.Graphics;

public abstract class CommandContext : GraphicsObject
{
    public abstract void SetPipeline(Pipeline pipeline);

    public abstract void SetRenderTarget(FrameBuffer? frameBuffer = default);

    public abstract void SetVertexBuffer(uint slot, GraphicsBuffer buffer, uint offset = 0);
    public abstract void SetIndexBuffer(GraphicsBuffer buffer, uint offset, IndexType indexType);

    public abstract void SetConstantBuffer(int index, GraphicsBuffer buffer);
    public abstract void SetSampler(int index, Sampler sampler);
    public abstract void SetSRV(int index, Texture texture);
    public abstract void SetUAV(int index, Texture texture);

    public void Dispatch1D(int threadCountX, int groupSizeX = 64)
    {
        Dispatch(
            Utilities.DivideByMultiple(threadCountX, groupSizeX),
            1,
            1);
    }

    public void Dispatch2D(int threadCountX, int threadCountY, int groupSizeX = 8, int groupSizeY = 8)
    {
        Dispatch(
            Utilities.DivideByMultiple(threadCountX, groupSizeX),
            Utilities.DivideByMultiple(threadCountY, groupSizeX),
            1
        );
    }

    public void Dispatch3D(int threadCountX, int threadCountY, int threadCountZ, int groupSizeX, int groupSizeY, int groupSizeZ)
    {
        Dispatch(
            Utilities.DivideByMultiple(threadCountX, groupSizeX),
            Utilities.DivideByMultiple(threadCountY, groupSizeY),
            Utilities.DivideByMultiple(threadCountZ, groupSizeZ)
        );
    }

    public abstract void Dispatch(int groupCountX, int groupCountY, int groupCountZ);

    /// <summary>
    /// Draw non-indexed geometry.
    /// </summary>
    /// <param name="vertexCount"></param>
    /// <param name="instanceCount"></param>
    /// <param name="firstVertex"></param>
    /// <param name="firstInstance"></param>
    public abstract void Draw(int vertexCount, int instanceCount = 1, int firstVertex = 0, int firstInstance = 0);

    /// <summary>
    /// Draw indexed geometry.
    /// </summary>
    /// <param name="indexCount"></param>
    /// <param name="instanceCount"></param>
    /// <param name="firstIndex"></param>
    /// <param name="baseVertex"></param>
    /// <param name="firstInstance"></param>
    public abstract void DrawIndexed(int indexCount, int instanceCount = 1, int firstIndex = 0, int baseVertex = 0, int firstInstance = 0);
}
