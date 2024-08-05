// Copyright (c) Amer Koleci and Contributors.
// Licensed under the MIT License (MIT). See LICENSE in the repository root for more information.

using XenoAtom.Interop;

namespace Alimer.Graphics;

/// <summary>
/// Structure that describes the <see cref="Pipeline"/>.
/// </summary>
public readonly ref struct ComputePipelineDescription
{
    public ReadOnlySpan<byte> ComputeShader { get; init; }

    /// <summary>
    /// Gets or sets the label of <see cref="Pipeline"/>.
    /// </summary>
    public ReadOnlyMemoryUtf8 Label { get; init; }
}
