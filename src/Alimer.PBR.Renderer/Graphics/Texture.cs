// Copyright © Amer Koleci and Contributors.
// Licensed under the MIT License (MIT). See LICENSE in the repository root for more information.

using CommunityToolkit.Diagnostics;

namespace Alimer.Graphics;

public abstract class Texture : GraphicsResource
{
    protected Texture(GraphicsDevice device)
        : base(device)
    {
    }
}
