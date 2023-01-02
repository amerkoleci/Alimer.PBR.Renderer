﻿// Copyright © Amer Koleci and Contributors.
// Licensed under the MIT License (MIT). See LICENSE in the repository root for more information.

using System.Drawing;
using System.Runtime.InteropServices;
using Alimer.Bindings.SDL;
using CommunityToolkit.Diagnostics;
using Vortice.Mathematics;

namespace Alimer.Graphics;

public abstract class GraphicsDevice : GraphicsObject
{
    protected GraphicsDevice(in SDL_Window window, GraphicsBackend backend)
    {
        Window = window;
        Backend = backend;
    }

    public readonly SDL_Window Window;

    /// <summary>
    /// Get the device backend type.
    /// </summary>
    public GraphicsBackend Backend { get; }

    public abstract CommandContext DefaultContext { get; }
    public abstract Texture ColorTexture { get; }
    public abstract int Samples { get; }

    public static GraphicsDevice CreateDefault(in SDL_Window window, int maxSamples = 4)
    {
        return new D3D11.D3D11GraphicsDevice(window, maxSamples);
    }

    public abstract bool BeginFrame();
    public abstract void EndFrame();

    public unsafe GraphicsBuffer CreateBuffer(in BufferDescription description)
    {
        return CreateBuffer(description, null);
    }

    public unsafe GraphicsBuffer CreateBuffer(in BufferDescription description, IntPtr initialData)
    {
        return CreateBuffer(description, initialData.ToPointer());
    }

    public unsafe GraphicsBuffer CreateBuffer(in BufferDescription description, void* initialData)
    {
        Guard.IsGreaterThanOrEqualTo(description.Size, 4, nameof(BufferDescription.Size));

        return CreateBufferCore(description, initialData);
    }

    public unsafe GraphicsBuffer CreateBuffer<T>(in BufferDescription description, ref T initialData) where T : unmanaged
    {
        Guard.IsGreaterThanOrEqualTo(description.Size, 4, nameof(BufferDescription.Size));

        fixed (void* initialDataPtr = &initialData)
        {
            return CreateBuffer(description, initialDataPtr);
        }
    }

    public GraphicsBuffer CreateBuffer<T>(T[] initialData,
        BufferUsage usage = BufferUsage.ShaderReadWrite,
        CpuAccessMode cpuAccess = CpuAccessMode.None)
        where T : unmanaged
    {
        ReadOnlySpan<T> dataSpan = initialData.AsSpan();

        return CreateBuffer(dataSpan, usage, cpuAccess);
    }

    public unsafe GraphicsBuffer CreateBuffer<T>(ReadOnlySpan<T> initialData,
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

    protected abstract unsafe GraphicsBuffer CreateBufferCore(in BufferDescription description, void* initialData);
    protected abstract unsafe Texture CreateTextureCore(in TextureDescription description, void* initialData);
    protected abstract Sampler CreateSamplerCore(in SamplerDescription description);
}
