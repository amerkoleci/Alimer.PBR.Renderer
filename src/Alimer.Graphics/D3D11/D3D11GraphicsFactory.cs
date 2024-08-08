// Copyright (c) Amer Koleci and Contributors.
// Licensed under the MIT License (MIT). See LICENSE in the repository root for more information.

using Win32;
using Win32.Graphics.Dxgi;
using static Win32.Apis;
using static Win32.Graphics.Dxgi.Apis;

namespace Alimer.Graphics.D3D11;

internal sealed unsafe class D3D11GraphicsFactory : GraphicsFactory
{
    private static readonly Lazy<bool> s_isSupported = new(CheckIsSupported);
    private readonly ComPtr<IDXGIFactory2> _handle;

    public static bool IsSupported() => s_isSupported.Value;

    public D3D11GraphicsFactory(in GraphicsFactoryDescription description)
        : base(GraphicsBackend.D3D11, in description)
    {
        bool debugFactory = false;

        if (ValidationMode != ValidationMode.Disabled)
        {
            debugFactory = true;

#if DEBUG
            using ComPtr<IDXGIInfoQueue> dxgiInfoQueue = default;

            if (DXGIGetDebugInterface1(0u, __uuidof<IDXGIInfoQueue>(), dxgiInfoQueue.GetVoidAddressOf()).Success)
            {
                dxgiInfoQueue.Get()->SetBreakOnSeverity(DXGI_DEBUG_ALL, DXGI_INFO_QUEUE_MESSAGE_SEVERITY_ERROR, true);
                dxgiInfoQueue.Get()->SetBreakOnSeverity(DXGI_DEBUG_ALL, DXGI_INFO_QUEUE_MESSAGE_SEVERITY_CORRUPTION, true);

                int* hide = stackalloc int[1]
                {
                    80 /* IDXGISwapChain::GetContainingOutput: The swapchain's adapter does not control the output on which the swapchain's window resides. */,
                };

                Win32.Graphics.Dxgi.InfoQueueFilter filter = new()
                {
                    DenyList = new()
                    {
                        NumIDs = 1,
                        pIDList = hide
                    }
                };

                dxgiInfoQueue.Get()->AddStorageFilterEntries(DXGI_DEBUG_DXGI, &filter);
            }
#endif
        }

        ThrowIfFailed(
            CreateDXGIFactory2(debugFactory, __uuidof<IDXGIFactory2>(), _handle.GetVoidAddressOf())
        );

        {
            using ComPtr<IDXGIFactory5> factory5 = default;
            if (_handle.CopyTo(&factory5).Success)
            {
                IsTearingSupported = factory5.Get()->IsTearingSupported();
            }
        }
    }

    protected override void Dispose(bool disposing)
    {
        if (disposing)
        {
            _handle.Dispose();

#if DEBUG
            using ComPtr<IDXGIDebug1> dxgiDebug = default;
            if (DXGIGetDebugInterface1(0, __uuidof<IDXGIDebug1>(), dxgiDebug.GetVoidAddressOf()).Success)
            {
                dxgiDebug.Get()->ReportLiveObjects(DXGI_DEBUG_ALL, ReportLiveObjectFlags.Summary | ReportLiveObjectFlags.IgnoreInternal);
            }
#endif
        }
    }

    public IDXGIFactory2* Handle => _handle;
    public bool IsTearingSupported { get; }

    public override GraphicsDevice CreateDevice(in GraphicsDeviceDescription description)
    {
        ComPtr<IDXGIAdapter1> adapter = default;

        using ComPtr<IDXGIFactory6> factory6 = default;
        if (_handle.CopyTo(&factory6).Success)
        {
            GpuPreference gpuPreference = (description.PowerPreference == GpuPowerPreference.LowPower) ? DXGI_GPU_PREFERENCE_MINIMUM_POWER : DXGI_GPU_PREFERENCE_HIGH_PERFORMANCE;

            for (uint adapterIndex = 0;
                factory6.Get()->EnumAdapterByGpuPreference(
                    adapterIndex,
                    gpuPreference,
                    __uuidof<IDXGIAdapter1>(),
                    (void**)adapter.ReleaseAndGetAddressOf()).Success;
                adapterIndex++)
            {
                AdapterDescription1 desc = default;
                ThrowIfFailed(adapter.Get()->GetDesc1(&desc));

                if ((desc.Flags & AdapterFlags.Software) != AdapterFlags.None)
                    continue;

                break;
            }
        }

        if (adapter.Get() == null)
        {
            for (uint adapterIndex = 0;
                _handle.Get()->EnumAdapters1(adapterIndex, adapter.ReleaseAndGetAddressOf()).Success;
                adapterIndex++)
            {
                AdapterDescription1 desc = default;
                ThrowIfFailed(adapter.Get()->GetDesc1(&desc));

                if ((desc.Flags & AdapterFlags.Software) != AdapterFlags.None)
                    continue;

                break;
            }
        }

        return new D3D11GraphicsDevice(this, adapter, in description);
    }

    private static bool CheckIsSupported()
    {
        try
        {
            using ComPtr<IDXGIFactory2> dxgiFactory = default;
            using ComPtr<IDXGIAdapter1> dxgiAdapter = default;

            ThrowIfFailed(CreateDXGIFactory1(__uuidof<IDXGIFactory2>(), dxgiFactory.GetVoidAddressOf()));

            bool foundCompatibleDevice = false;
            for (uint adapterIndex = 0;
                dxgiFactory.Get()->EnumAdapters1(adapterIndex, dxgiAdapter.ReleaseAndGetAddressOf()).Success;
                adapterIndex++)
            {
                AdapterDescription1 adapterDesc;
                ThrowIfFailed(dxgiAdapter.Get()->GetDesc1(&adapterDesc));

                if ((adapterDesc.Flags & AdapterFlags.Software) != 0)
                {
                    // Don't select the Basic Render Driver adapter.
                    continue;
                }

                foundCompatibleDevice = true;
                break;
            }

            if (!foundCompatibleDevice)
            {
                return false;
            }

            return true;
        }
        catch
        {
            return false;
        }
    }
}
