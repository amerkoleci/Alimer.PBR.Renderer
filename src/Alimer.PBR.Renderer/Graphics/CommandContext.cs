// Copyright © Amer Koleci and Contributors.
// Licensed under the MIT License (MIT). See LICENSE in the repository root for more information.

namespace Alimer.Graphics;

public abstract class CommandContext : GraphicsObject
{
    public abstract void SetPipeline(Pipeline pipeline);

    public abstract void SetRenderTarget(FrameBuffer? frameBuffer = default);

    /// <summary>
    /// Draw non-indexed geometry.
    /// </summary>
    /// <param name="vertexCount"></param>
    /// <param name="instanceCount"></param>
    /// <param name="firstVertex"></param>
    /// <param name="firstInstance"></param>
    public abstract void Draw(int vertexCount, int instanceCount = 1, int firstVertex = 0, int firstInstance = 0);

    /// <summary>
    /// Draw indexed geometry.
    /// </summary>
    /// <param name="indexCount"></param>
    /// <param name="instanceCount"></param>
    /// <param name="firstIndex"></param>
    /// <param name="baseVertex"></param>
    /// <param name="firstInstance"></param>
    public abstract void DrawIndexed(int indexCount, int instanceCount = 1, int firstIndex = 0, int baseVertex = 0, int firstInstance = 0);
}
