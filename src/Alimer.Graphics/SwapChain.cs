// Copyright (c) Amer Koleci and Contributors.
// Licensed under the MIT License (MIT). See LICENSE in the repository root for more information.

using System.Drawing;

namespace Alimer.Graphics;

public abstract class SwapChain : GraphicsResource
{
    public SwapChain(GraphicsDevice device, SurfaceSource surface, in SwapChainDescription description)
        : base(device, description.Label)
    {
        Surface = surface;
        //Surface.SizeChanged += OnSurfaceSizeChanged;
        ColorFormat = description.Format;
        PresentMode = description.PresentMode;
        IsFullscreen = description.IsFullscreen;
    }

    public SurfaceSource Surface { get; }

    public TextureFormat ColorFormat { get; protected set; }
    public PresentMode PresentMode { get; }
    public bool IsFullscreen { get; protected set; }

    public abstract Size DrawableSize { get; }

    protected abstract void ResizeBackBuffer();

    //private void OnSurfaceSizeChanged(object? sender, EventArgs eventArgs)
    //{
    //    ResizeBackBuffer();
    //}

    public abstract Texture? GetCurrentTexture();
    public abstract void Present();
}
