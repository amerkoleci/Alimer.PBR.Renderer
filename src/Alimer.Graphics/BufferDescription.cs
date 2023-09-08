// Copyright © Amer Koleci and Contributors.
// Licensed under the MIT License (MIT). See LICENSE in the repository root for more information.

using System.Diagnostics.CodeAnalysis;

namespace Alimer.Graphics;

/// <summary>
/// Structure that describes the <see cref="GraphicsBuffer"/>.
/// </summary>
public readonly record struct BufferDescription
{
    [SetsRequiredMembers]
    public BufferDescription(
       uint size,
       BufferUsage usage = BufferUsage.ShaderReadWrite,
       CpuAccessMode cpuAccess = CpuAccessMode.None,
       string? label = default)
    {
        Usage = usage;
        Size = size;
        CpuAccess = cpuAccess;
        Label = label;
    }

    /// <summary>
    /// Gets or sets the <see cref="BufferUsage"/>.
    /// </summary>
    public required BufferUsage Usage { get; init; }

    /// <summary>
    /// Gets or sets the size in bytes of the buffer.
    /// </summary>
    public required uint Size { get; init; }

    /// <summary>
    /// Gets or sets tge CPU access of the buffer.
    /// </summary>
    public CpuAccessMode CpuAccess { get; init; }

    /// <summary>
    /// Gets or sets the label of <see cref="GraphicsBuffer"/>.
    /// </summary>
    public string? Label { get; init; }
}
