// Copyright (c) Amer Koleci and Contributors.
// Licensed under the MIT License (MIT). See LICENSE in the repository root for more information.

using XenoAtom.Interop;

namespace Alimer.Graphics;

public record struct RenderPassDescription
{
    public RenderPassDescription()
    {
        ColorAttachments = [];
    }

    public RenderPassDescription(params RenderPassColorAttachment[] colorAttachments)
    {
        ColorAttachments = colorAttachments;
    }

    public RenderPassDescription(RenderPassDepthStencilAttachment depthStencilAttachment, params RenderPassColorAttachment[] colorAttachments)
    {
        ColorAttachments = colorAttachments;
        DepthStencilAttachment = depthStencilAttachment;
    }

    public RenderPassColorAttachment[] ColorAttachments;
    public RenderPassDepthStencilAttachment DepthStencilAttachment;

    /// <summary>
    /// Gets or sets the label of <see cref="RenderPassDescription"/>.
    /// </summary>
    public ReadOnlyMemoryUtf8 Label;
}
