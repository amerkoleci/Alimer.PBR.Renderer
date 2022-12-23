// Copyright © Amer Koleci and Contributors.
// Licensed under the MIT License (MIT). See LICENSE in the repository root for more information.

using System.Drawing;
using Alimer.Bindings.SDL;
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

    public abstract int Samples { get; }

    public static GraphicsDevice CreateDefault(in SDL_Window window, int maxSamples = 4)
    {
        return new D3D11.D3D11GraphicsDevice(window, maxSamples);
    }

    public abstract bool BeginFrame();
    public abstract void EndFrame();

    public abstract Texture CreateTexture(in Size3 size, TextureFormat format, TextureUsage usage = TextureUsage.ShaderRead, int sampleCount = 1);

    public abstract FrameBuffer CreateFrameBuffer(in Size size, int samples, TextureFormat colorFormat, TextureFormat depthstencilFormat);
}
