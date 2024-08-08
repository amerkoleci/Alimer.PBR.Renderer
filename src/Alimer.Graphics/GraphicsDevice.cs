// Copyright (c) Amer Koleci and Contributors.
// Licensed under the MIT License (MIT). See LICENSE in the repository root for more information.

using System.Runtime.InteropServices;
using CommunityToolkit.Diagnostics;

namespace Alimer.Graphics;

public abstract unsafe class GraphicsDevice : GraphicsObject
{
    public static readonly int NumFramesInFlight = 2;

    protected GraphicsDevice(in GraphicsDeviceDescription description)
        : base(description.Label)
    {
    }

    /// <summary>
    /// Get the device limits.
    /// </summary>
    public abstract GraphicsDeviceLimits Limits { get; }

    public abstract CommandContext DefaultContext { get; }


    public abstract TextureSampleCount QueryMaxTextureSampleCount(TextureFormat format);

    public abstract bool BeginFrame();
    public abstract void EndFrame();

    #region CreateBuffer
    public GraphicsBuffer CreateBuffer(in BufferDescription description)
    {
        return CreateBuffer(description, null);
    }

    public GraphicsBuffer CreateBuffer(in BufferDescription description, nint initialData)
    {
        return CreateBuffer(description, initialData.ToPointer());
    }

    public GraphicsBuffer CreateBuffer(in BufferDescription description, void* initialData)
    {
        Guard.IsTrue(description.Usage != BufferUsage.None, nameof(BufferDescription.Usage));
        Guard.IsGreaterThanOrEqualTo(description.Size, 4, nameof(BufferDescription.Size));
        Guard.IsLessThanOrEqualTo(description.Size, Limits.MaxBufferSize, nameof(BufferDescription.Size));

        return CreateBufferCore(description, initialData);
    }

    public GraphicsBuffer CreateBuffer<T>(in BufferDescription description, ref T initialData) where T : unmanaged
    {
        fixed (T* initialDataPtr = &initialData)
            return CreateBuffer(description, initialDataPtr);
    }

    public GraphicsBuffer CreateBuffer<T>(in BufferDescription description, ReadOnlySpan<T> initialData) where T : unmanaged
    {
        fixed (T* initialDataPtr = initialData)
            return CreateBuffer(description, initialDataPtr);
    }

    public GraphicsBuffer CreateBuffer<T>(Span<T> initialData,
        BufferUsage usage = BufferUsage.ShaderReadWrite,
        CpuAccessMode cpuAccess = CpuAccessMode.None,
        string? label = default)
        where T : unmanaged
    {
        int typeSize = sizeof(T);
        Guard.IsTrue(initialData.Length > 0, nameof(initialData));

        BufferDescription description = new((uint)(initialData.Length * typeSize), usage, cpuAccess, label);
        return CreateBuffer(description, ref MemoryMarshal.GetReference(initialData));
    }

    public GraphicsBuffer CreateBuffer<T>(T[] initialData,
        BufferUsage usage = BufferUsage.ShaderReadWrite,
        CpuAccessMode cpuAccess = CpuAccessMode.None)
        where T : unmanaged
    {
        Span<T> dataSpan = initialData.AsSpan();

        return CreateBuffer(dataSpan, usage, cpuAccess);
    }
    #endregion

    public unsafe Texture CreateTexture<T>(in TextureDescription description, ref T initialData) where T : unmanaged
    {
        Guard.IsGreaterThanOrEqualTo(description.Width, 1, nameof(TextureDescription.Width));
        Guard.IsGreaterThanOrEqualTo(description.Height, 1, nameof(TextureDescription.Height));

        fixed (void* initialDataPtr = &initialData)
        {
            return CreateTextureCore(description, initialDataPtr);
        }
    }

    public Texture CreateTexture<T>(Span<T> initialData, in TextureDescription description) where T : unmanaged
    {
        return CreateTexture(description, ref MemoryMarshal.GetReference(initialData));
    }

    public Texture CreateTexture<T>(ReadOnlySpan<T> initialData, in TextureDescription description) where T : unmanaged
    {
        return CreateTexture(description, ref MemoryMarshal.GetReference(initialData));
    }

    public unsafe Texture CreateTexture(in TextureDescription description)
    {
        Guard.IsGreaterThanOrEqualTo(description.Width, 1, nameof(TextureDescription.Width));
        Guard.IsGreaterThanOrEqualTo(description.Height, 1, nameof(TextureDescription.Height));

        return CreateTextureCore(description, default);
    }

    public Sampler CreateSampler(in SamplerDescription description)
    {
        return CreateSamplerCore(description);
    }

    public abstract Pipeline CreateComputePipeline(in ComputePipelineDescription description);
    public abstract Pipeline CreateRenderPipeline(in RenderPipelineDescription description);

    public SwapChain CreateSwapChain(SurfaceSource surface, in SwapChainDescription description)
    {
        Guard.IsNotNull(surface, nameof(surface));
        Guard.IsTrue(description.Format != TextureFormat.Invalid, nameof(SwapChainDescription.Format));

        return CreateSwapChainCore(surface, description);
    }

    protected abstract unsafe GraphicsBuffer CreateBufferCore(in BufferDescription description, void* initialData);
    protected abstract unsafe Texture CreateTextureCore(in TextureDescription description, void* initialData);
    protected abstract Sampler CreateSamplerCore(in SamplerDescription description);
    protected abstract SwapChain CreateSwapChainCore(SurfaceSource surface, in SwapChainDescription description);
}
