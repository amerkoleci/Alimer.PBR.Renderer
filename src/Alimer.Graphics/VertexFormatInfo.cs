// Copyright © Amer Koleci and Contributors.
// Licensed under the MIT License (MIT). See LICENSE in the repository root for more information.

namespace Alimer.Graphics;

public readonly record struct VertexFormatInfo(
    VertexFormat Format,
    uint ByteSize,
    uint ComponentCount,
    uint ComponentByteSize,
    VertexFormatBaseType BaseType
);
