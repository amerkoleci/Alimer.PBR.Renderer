// Copyright (c) Amer Koleci and Contributors.
// Licensed under the MIT License (MIT). See LICENSE in the repository root for more information.

using System.Diagnostics;
using System.Drawing;
using CommunityToolkit.Diagnostics;
using Win32;
using Win32.Graphics.Direct3D;
using Win32.Graphics.Direct3D12;
using Win32.Graphics.Dxgi;
using Win32.Graphics.Dxgi.Common;
using static Win32.Apis;
using static Win32.Graphics.Direct3D12.Apis;
using static Win32.Graphics.Dxgi.Apis;
using Feature = Win32.Graphics.Direct3D12.Feature;
using InfoQueueFilter = Win32.Graphics.Direct3D12.InfoQueueFilter;
using MessageId = Win32.Graphics.Direct3D12.MessageId;

namespace Alimer.Graphics.D3D12;

internal sealed unsafe class D3D12GraphicsFactory : GraphicsFactory
{
    public static readonly FeatureLevel MinFeatureLevel = FeatureLevel.Level_11_0;

    private static readonly Lazy<bool> s_isSupported = new(CheckIsSupported);
    private readonly ComPtr<IDXGIFactory4> _handle;

    public static bool IsSupported() => s_isSupported.Value;

    public D3D12GraphicsFactory(in GraphicsFactoryDescription description)
        : base(GraphicsBackend.D3D12, in description)
    {
        bool debugFactory = false;

        if (ValidationMode != ValidationMode.Disabled)
        {
            debugFactory = true;

            using ComPtr<ID3D12Debug> d3d12Debug = default;
            if (D3D12GetDebugInterface(__uuidof<ID3D12Debug>(), d3d12Debug.GetVoidAddressOf()).Success)
            {
                d3d12Debug.Get()->EnableDebugLayer();

                if (description.ValidationMode == ValidationMode.GPU)
                {
                    using ComPtr<ID3D12Debug1> d3d12Debug1 = default;
                    using ComPtr<ID3D12Debug2> d3d12Debug2 = default;

                    if (d3d12Debug.CopyTo(d3d12Debug1.GetAddressOf()).Success)
                    {
                        d3d12Debug1.Get()->SetEnableGPUBasedValidation(true);
                        d3d12Debug1.Get()->SetEnableSynchronizedCommandQueueValidation(true);
                    }

                    if (d3d12Debug.CopyTo(d3d12Debug2.GetAddressOf()).Success)
                    {
                        d3d12Debug2.Get()->SetGPUBasedValidationFlags(D3D12_GPU_BASED_VALIDATION_FLAGS_NONE);
                    }
                }
            }
            else
            {
                Debug.WriteLine("WARNING: Direct3D Debug Device is not available");
            }

            // DRED
            {
                using ComPtr<ID3D12DeviceRemovedExtendedDataSettings1> pDredSettings = default;
                if (D3D12GetDebugInterface(__uuidof<ID3D12DeviceRemovedExtendedDataSettings1>(), pDredSettings.GetVoidAddressOf()).Success)
                {
                    // Turn on auto - breadcrumbs and page fault reporting.
                    pDredSettings.Get()->SetAutoBreadcrumbsEnablement(D3D12_DRED_ENABLEMENT_FORCED_ON);
                    pDredSettings.Get()->SetPageFaultEnablement(D3D12_DRED_ENABLEMENT_FORCED_ON);
                    pDredSettings.Get()->SetBreadcrumbContextEnablement(D3D12_DRED_ENABLEMENT_FORCED_ON);
                }
            }

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
            CreateDXGIFactory2(debugFactory, __uuidof<IDXGIFactory4>(), _handle.GetVoidAddressOf())
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

    public IDXGIFactory4* Handle => _handle;
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

                // Check to see if the adapter supports Direct3D 12, but don't create the actual device yet.
                if (D3D12CreateDevice((IUnknown*)adapter.Get(), MinFeatureLevel, __uuidof<ID3D12Device5>(), null).Success)
                {
                    break;
                }
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

                // Check to see if the adapter supports Direct3D 12, but don't create the actual device yet.
                if (D3D12CreateDevice((IUnknown*)adapter.Get(), MinFeatureLevel, __uuidof<ID3D12Device5>(), null).Success)
                {
                    break;
                }
            }
        }

        return new D3D12GraphicsDevice(this, adapter, in description);
    }

    private static bool CheckIsSupported()
    {
        try
        {
            if (!OperatingSystem.IsWindowsVersionAtLeast(10, 0, 19041))
            {
                return false;
            }

            using ComPtr<IDXGIFactory4> dxgiFactory = default;
            using ComPtr<IDXGIAdapter1> dxgiAdapter = default;

            ThrowIfFailed(CreateDXGIFactory1(__uuidof<IDXGIFactory4>(), dxgiFactory.GetVoidAddressOf()));

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

                // Check to see if the adapter supports Direct3D 12, but don't create the actual device.
                if (D3D12CreateDevice((IUnknown*)dxgiAdapter.Get(), FeatureLevel.Level_12_0,
                     __uuidof<ID3D12Device>(), null).Success)
                {
                    foundCompatibleDevice = true;
                    break;
                }
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
