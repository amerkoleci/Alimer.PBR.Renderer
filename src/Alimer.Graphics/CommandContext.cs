﻿// Copyright (c) Amer Koleci and Contributors.
// Licensed under the MIT License (MIT). See LICENSE in the repository root for more information.

using System.Text;
using CommunityToolkit.Diagnostics;
using Vortice.Mathematics;
using XenoAtom.Interop;

namespace Alimer.Graphics;

public abstract class CommandContext : GraphicsObject
{
    private bool _insideRenderPass;

    /// <summary>
    /// Flush existing commands to the GPU and optinally wait for completion
    /// </summary>
    /// <param name="waitForCompletion"></param>
    public abstract void Flush(bool waitForCompletion = false);

    public void PushDebugGroup(string groupLabel, in Color4 color = default)
    {
        int utf8Count = Encoding.UTF8.GetByteCount(groupLabel);
        Span<byte> utf8Buffer = stackalloc byte[utf8Count + 1];
        Encoding.UTF8.GetBytes(groupLabel, utf8Buffer);
        utf8Buffer[utf8Count] = 0;
        PushDebugGroup((ReadOnlySpan<byte>)utf8Buffer, color);
    }

    public abstract void PushDebugGroup(ReadOnlySpanUtf8 groupLabel, in Color4 color = default);
    public abstract void PopDebugGroup();
    public abstract void InsertDebugMarker(string debugLabel);

    public ScopedDebugGroup PushScopedDebugGroup(string groupLabel)
    {
        PushDebugGroup(groupLabel);
        return new(this);
    }

    public void BeginRenderPass(in RenderPassDescription renderPass)
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

    public ScopedRenderPass PushScopedPassPass(in RenderPassDescription renderPass)
    {
        BeginRenderPass(renderPass);
        return new(this);
    }

    public abstract void SetPipeline(Pipeline pipeline);

    public abstract void SetVertexBuffer(uint slot, GraphicsBuffer buffer, ulong offset = 0);
    public abstract void SetIndexBuffer(GraphicsBuffer buffer, ulong offset, IndexType indexType);

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

    protected abstract void BeginRenderPassCore(in RenderPassDescription descriptor);
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

    public readonly struct ScopedRenderPass(CommandContext commandBuffer) : IDisposable
    {
        private readonly CommandContext _commandBuffer = commandBuffer;

        public void Dispose()
        {
            _commandBuffer.EndRenderPass();
        }
    }
    #endregion
}
