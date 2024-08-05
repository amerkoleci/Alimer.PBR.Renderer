// Copyright (c) Amer Koleci and Contributors.
// Licensed under the MIT License (MIT). See LICENSE in the repository root for more information.

using XenoAtom.Interop;

namespace Alimer.Graphics;

/// <summary>
/// Structure that describes the <see cref="Pipeline"/>.
/// </summary>
public readonly ref struct RenderPipelineDescription
{
    public RenderPipelineDescription()
    {
        BlendState = BlendState.Opaque;
        RasterizerState = RasterizerState.CullBack;
        DepthStencilState = DepthStencilState.DepthDefault;
        PrimitiveTopology = PrimitiveTopology.TriangleList;
    }

    public ReadOnlySpan<byte> VertexShader { get; init; }

    public ReadOnlySpan<byte> FragmentShader { get; init; }

    public BlendState BlendState { get; init; }

    public RasterizerState RasterizerState { get; init; }

    public DepthStencilState DepthStencilState { get; init; }
    public VertexDescriptor VertexDescriptor { get; init; }

    public PrimitiveTopology PrimitiveTopology { get; init; }

    /// <summary>
    /// Gets or sets the label of <see cref="Pipeline"/>.
    /// </summary>
    public ReadOnlyMemoryUtf8 Label { get; init; }
}
