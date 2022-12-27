// Copyright © Amer Koleci and Contributors.
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
    public abstract int Samples { get; }

    public static GraphicsDevice CreateDefault(in SDL_Window window, int maxSamples = 4)
    {
        return new D3D11.D3D11GraphicsDevice(window, maxSamples);
    }

    public abstract bool BeginFrame();
    public abstract void EndFrame();

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

    public abstract Pipeline CreateComputePipeline(in ComputePipelineDescription description);
    public abstract Pipeline CreateRenderPipeline(in RenderPipelineDescription description);

    protected abstract unsafe Texture CreateTextureCore(in TextureDescription description, void* initialData); 

    public abstract FrameBuffer CreateFrameBuffer(in Size size, int samples, TextureFormat colorFormat, TextureFormat depthstencilFormat);
}
