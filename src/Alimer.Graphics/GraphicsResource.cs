// Copyright © Amer Koleci and Contributors.
// Licensed under the MIT License (MIT). See LICENSE in the repository root for more information.

using CommunityToolkit.Diagnostics;

namespace Alimer.Graphics;

/// <summary>
/// Defines a graphics resource created by <see cref="GraphicsDevice"/>
/// </summary>
public abstract class GraphicsResource : GraphicsObject
{
    /// <summary>Initializes a new instance of the <see cref="GraphicsResource" /> class.</summary>
    /// <param name="device">The device object that created the resource..</param>
    /// <param name="label">The label of the object or <c>null</c> to use <see cref="System.Reflection.MemberInfo.Name" />.</param>
    protected GraphicsResource(GraphicsDevice device, string? label = default)
        : base(label)
    {
        Guard.IsNotNull(device, nameof(device));

        Device = device;
    }

    /// <summary>
    /// Get the <see cref="GraphicsDevice"/> object that created the resource.
    /// </summary>
    public GraphicsDevice Device { get; }
}
