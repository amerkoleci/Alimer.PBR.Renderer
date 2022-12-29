// Copyright © Amer Koleci and Contributors.
// Licensed under the MIT License (MIT). See LICENSE in the repository root for more information.

namespace Alimer.Graphics;

/// <summary>
/// Structure that describes the <see cref="Sampler"/>.
/// </summary>
public readonly record struct SamplerDescription
{
    /// <summary>
    /// Gets or sets the min filter of <see cref="Sampler"/>
    /// </summary>
    public SamplerMinMagFilter MinFilter { get; init; }

    /// <summary>
    /// Gets or sets the mag filter of <see cref="Sampler"/>
    /// </summary>
    public SamplerMinMagFilter MagFilter { get; init; }

    /// <summary>
    /// Gets or sets the mip filter of <see cref="Sampler"/>
    /// </summary>
    public SamplerMipFilter MipFilter { get; init; }

    public SamplerAddressMode AddressModeU { get; init; }
    public SamplerAddressMode AddressModeV { get; init; }
    public SamplerAddressMode AddressModeW { get; init; }

    public float LodMinClamp { get; init; }
    public float LodMaxClamp { get; init; }

    public CompareFunction Compare { get; init; }

    public ushort MaxAnisotropy { get; init; }

    /// <summary>
    /// Gets or sets the label of <see cref="Sampler"/>.
    /// </summary>
    public string? Label { get; init; }
}
