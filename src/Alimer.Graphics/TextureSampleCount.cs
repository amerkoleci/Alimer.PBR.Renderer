﻿// Copyright (c) Amer Koleci and Contributors.
// Licensed under the MIT License (MIT). See LICENSE in the repository root for more information.

namespace Alimer.Graphics;

[Flags]
public enum TextureSampleCount : uint
{
    Count1 = (1 << 0),
    Count2 = (1 << 1),
    Count4 = (1 << 2),
    Count8 = (1 << 3),
    Count16 = (1 << 4),
    Count32 = (1 << 5),
    Count64 = (1 << 6),
}
