// Copyright (c) Amer Koleci and Contributors.
// Licensed under the MIT License (MIT). See LICENSE in the repository root for more information.

namespace Alimer.PBR.Renderer;

public sealed class Model
{
    public IList<Material> Materials { get; } = new List<Material>();

    public IList<Mesh> Meshes { get; } = new List<Mesh>();
}
