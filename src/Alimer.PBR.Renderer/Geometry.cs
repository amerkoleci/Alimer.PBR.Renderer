// Copyright © Amer Koleci and Contributors.
// Licensed under the MIT License (MIT). See LICENSE in the repository root for more information.

using Alimer.Graphics;
using CommunityToolkit.Diagnostics;

namespace Alimer.PBR.Renderer;

public abstract class Geometry : GraphicsObject
{
    public Geometry(GraphicsDevice graphicsDevice)
    {
        Guard.IsNotNull(graphicsDevice, name: nameof(graphicsDevice));

        GraphicsDevice = graphicsDevice;
    }

    public GraphicsDevice GraphicsDevice { get; }
}
