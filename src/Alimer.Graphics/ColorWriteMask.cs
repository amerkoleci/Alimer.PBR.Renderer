// Copyright (c) Amer Koleci and Contributors.
// Licensed under the MIT License (MIT). See LICENSE in the repository root for more information.

namespace Alimer.Graphics;

[Flags]
public enum ColorWriteMask : uint
{
    None = 0,
    Red = 0x00000001,
    Green = 0x00000002,
    Blue = 0x00000004,
    Alpha = 0x00000008,
    All = Red | Green | Blue | Alpha,
}
