// Copyright (c) Amer Koleci and Contributors.
// Licensed under the MIT License (MIT). See LICENSE in the repository root for more information.

using Alimer.Graphics;
using CommunityToolkit.Diagnostics;

namespace Alimer.PBR.Renderer;

public abstract class Material : GraphicsObject
{
    private static int s_nextMaterialId = 1;

    public Material(GraphicsDevice device)
    {
        Guard.IsNotNull(device, nameof(device));

        Device = device;
        Id = s_nextMaterialId++;
    }

    public GraphicsDevice Device { get; }

    /// <summary>
    /// Gets the unique material id.
    /// </summary>
    public int Id { get; }
}
