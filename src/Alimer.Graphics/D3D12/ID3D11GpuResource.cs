// Copyright © Amer Koleci and Contributors.
// Licensed under the MIT License (MIT). See LICENSE in the repository root for more information.

using Win32.Graphics.Direct3D12;

namespace Alimer.Graphics.D3D12;

internal unsafe interface ID3D11GpuResource
{
    ResourceStates State { get; set; }

    ResourceStates TransitioningState { get; set; }

    ID3D12Resource* Handle { get; }
}
