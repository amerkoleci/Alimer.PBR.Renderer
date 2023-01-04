// Copyright © Amer Koleci and Contributors.
// Licensed under the MIT License (MIT). See LICENSE in the repository root for more information.

using CommunityToolkit.Diagnostics;
using Vortice.Mathematics;

namespace Alimer.Graphics;

public abstract class CommandContext : GraphicsObject
{
    private bool _insideRenderPass;

    public abstract void PushDebugGroup(string groupLabel);
    public abstract void PopDebugGroup();
    public abstract void InsertDebugMarker(string debugLabel);

    public ScopedDebugGroup PushScopedDebugGroup(string groupLabel)
    {
        PushDebugGroup(groupLabel);
        return new(this);
    }

    public void BeginRenderPass(in RenderPassDescriptor renderPass)
    {
        Guard.IsFalse(_insideRenderPass);

        BeginRenderPassCore(renderPass);
        _insideRenderPass = true;
    }

    public void EndRenderPass()
    {
        Guard.IsTrue(_insideRenderPass);

        EndRenderPassCore();
        _insideRenderPass = false;
    }

    public ScopedRenderPass PushScopedPassPass(in RenderPassDescriptor renderPass)
    {
        BeginRenderPass(renderPass);
        return new(this);
    }

    public abstract void SetPipeline(Pipeline pipeline);

    public abstract void SetVertexBuffer(uint slot, GraphicsBuffer buffer, uint offset = 0);
    public abstract void SetIndexBuffer(GraphicsBuffer buffer, uint offset, IndexType indexType);

    public abstract void SetConstantBuffer(int index, GraphicsBuffer buffer);
    public abstract void SetSampler(int index, Sampler sampler);
    public abstract void SetSRV(int index, Texture texture);
    public abstract void SetUAV(int index, Texture texture, int mipLevel);

    public unsafe void UpdateConstantBuffer<T>(GraphicsBuffer source, T data)
        where T : unmanaged
    {
        UpdateConstantBuffer(source, &data, (uint)sizeof(T));
    }

    public abstract unsafe void UpdateConstantBuffer(GraphicsBuffer source, void* data, uint size);
    public abstract void CopyTexture(Texture source, Texture destination);
    public abstract void CopyTexture(Texture source, int sourceArraySlice, Texture destination, int destArraySlice);

    public abstract void GenerateMips(Texture texture);

    public void Dispatch1D(int threadCountX, int groupSizeX = 64)
    {
        Dispatch(
            UnsafeUtilities.DivideByMultiple(threadCountX, groupSizeX),
            1,
            1);
    }

    public void Dispatch2D(int threadCountX, int threadCountY, int groupSizeX = 8, int groupSizeY = 8)
    {
        Dispatch(
            UnsafeUtilities.DivideByMultiple(threadCountX, groupSizeX),
            UnsafeUtilities.DivideByMultiple(threadCountY, groupSizeX),
            1
        );
    }

    public void Dispatch3D(int threadCountX, int threadCountY, int threadCountZ, int groupSizeX, int groupSizeY, int groupSizeZ)
    {
        Dispatch(
            UnsafeUtilities.DivideByMultiple(threadCountX, groupSizeX),
            UnsafeUtilities.DivideByMultiple(threadCountY, groupSizeY),
            UnsafeUtilities.DivideByMultiple(threadCountZ, groupSizeZ)
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

    protected abstract void BeginRenderPassCore(in RenderPassDescriptor descriptor);
    protected abstract void EndRenderPassCore();

    #region Nested
    public readonly struct ScopedDebugGroup : IDisposable
    {
        private readonly CommandContext _commandBuffer;

        public ScopedDebugGroup(CommandContext commandBuffer)
        {
            _commandBuffer = commandBuffer;
        }

        public void Dispose()
        {
            _commandBuffer.PopDebugGroup();
        }
    }

    public readonly struct ScopedRenderPass : IDisposable
    {
        private readonly CommandContext _commandBuffer;

        public ScopedRenderPass(CommandContext commandBuffer)
        {
            _commandBuffer = commandBuffer;
        }

        public void Dispose()
        {
            _commandBuffer.EndRenderPass();
        }
    }
    #endregion
}
