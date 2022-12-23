// Copyright © Amer Koleci and Contributors.
// Licensed under the MIT License (MIT). See LICENSE in the repository root for more information.

namespace Alimer.Graphics;

public abstract class Pipeline : GraphicsResource
{
    protected Pipeline(GraphicsDevice device, in RenderPipelineDescription description)
        : base(device, description.Label)
    {
    }
}
