// Copyright © Amer Koleci and Contributors.
// Licensed under the MIT License (MIT). See LICENSE in the repository root for more information.

using Alimer.Bindings.SDL;

namespace Alimer.PBR.Renderer;

public interface IRenderer : IDisposable
{
    SDL_Window Initialize(int width, int height, int maxSamples);

    void Render(in SDL_Window window);
}
