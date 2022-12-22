// Copyright © Amer Koleci and Contributors.
// Licensed under the MIT License (MIT). See LICENSE in the repository root for more information.

using System.Numerics;
using Alimer.Bindings.SDL;
using CommunityToolkit.Diagnostics;
using Win32;
using Win32.Graphics.Direct3D;
using Win32.Graphics.Direct3D11;
using Win32.Graphics.Dxgi;
using Win32.Graphics.Dxgi.Common;
using static Alimer.Bindings.SDL.SDL;
using static Alimer.Bindings.SDL.SDL.SDL_WindowFlags;
using static Win32.Apis;
using static Win32.Graphics.Direct3D11.Apis;
using static Win32.Graphics.Dxgi.Apis;
using InfoQueueFilter = Win32.Graphics.Direct3D11.InfoQueueFilter;
using MessageId = Win32.Graphics.Direct3D11.MessageId;

namespace Alimer.PBR.Renderer;

public sealed unsafe class D3D11Renderer : IRenderer
{
    private ComPtr<IDXGIFactory2> _dxgiFactory;
    private bool _isTearingSupported;
    private ComPtr<ID3D11Device1> _device = default;
    private ComPtr<ID3D11DeviceContext1> _context = default;
    private FeatureLevel _featureLevel = FeatureLevel.Level_9_1;
    private ComPtr<IDXGISwapChain1> _swapChain = default;
    private ComPtr<ID3D11Texture2D> _backBufferTexture = default;
    private ComPtr<ID3D11RenderTargetView> _backBufferRTV = default;

    private ComPtr<ID3D11DepthStencilView> depthStencilTextureView = default;

    public Format ColorFormat { get; } = Format.B8G8R8A8Unorm;
    public Format depthStencilFormat { get; } = Format.D32Float;

    public D3D11Renderer()
    {

    }


    public void Dispose()
    {
        _context.Get()->Flush();

        _backBufferRTV.Dispose();
        _backBufferTexture.Dispose();
        _swapChain.Dispose();
        _context.Dispose();
        _device.Dispose();

        _dxgiFactory.Dispose();

#if DEBUG
        using ComPtr<IDXGIDebug1> dxgiDebug = default;
        if (DXGIGetDebugInterface1(0, __uuidof<IDXGIDebug1>(), dxgiDebug.GetVoidAddressOf()).Success)
        {
            dxgiDebug.Get()->ReportLiveObjects(DXGI_DEBUG_ALL, ReportLiveObjectFlags.Summary | ReportLiveObjectFlags.IgnoreInternal);
        }
#endif
    }

    public SDL_Window Initialize(int width, int height, int maxSamples)
    {
#if DEBUG
        {
            using ComPtr<IDXGIInfoQueue> dxgiInfoQueue = default;
            if (DXGIGetDebugInterface1(0, __uuidof<IDXGIInfoQueue>(), (void**)dxgiInfoQueue.GetAddressOf()).Success)
            {
                dxgiInfoQueue.Get()->SetBreakOnSeverity(DXGI_DEBUG_ALL, InfoQueueMessageSeverity.Error, true);
                dxgiInfoQueue.Get()->SetBreakOnSeverity(DXGI_DEBUG_ALL, InfoQueueMessageSeverity.Corruption, true);
            }
        }
#endif

        HResult hr = CreateDXGIFactory1(__uuidof<IDXGIFactory2>(), _dxgiFactory.GetVoidAddressOf());

        {
            using ComPtr<IDXGIFactory5> factory5 = default;
            if (_dxgiFactory.CopyTo(&factory5).Success)
            {
                _isTearingSupported = factory5.Get()->IsTearingSupported();
            }
        }

        using ComPtr<IDXGIAdapter1> adapter = default;

        using ComPtr<IDXGIFactory6> factory6 = default;
        if (_dxgiFactory.CopyTo(&factory6).Success)
        {
            for (uint adapterIndex = 0;
                factory6.Get()->EnumAdapterByGpuPreference(
                    adapterIndex,
                    GpuPreference.HighPerformance,
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
                _dxgiFactory.Get()->EnumAdapters1(adapterIndex, adapter.ReleaseAndGetAddressOf()).Success;
                adapterIndex++)
            {
                AdapterDescription1 desc = default;
                ThrowIfFailed(adapter.Get()->GetDesc1(&desc));

                if ((desc.Flags & AdapterFlags.Software) != AdapterFlags.None)
                    continue;

                break;
            }
        }

        CreateDeviceFlags creationFlags = CreateDeviceFlags.BgraSupport;
#if DEBUG
        if (SdkLayersAvailable())
        {
            // If the project is in a debug build, enable debugging via SDK Layers with this flag.
            creationFlags |= CreateDeviceFlags.Debug;
        }
#endif

        using ComPtr<ID3D11Device> tempDevice = default;
        using ComPtr<ID3D11DeviceContext> tempContext = default;
        FeatureLevel featureLevel;

        ReadOnlySpan<FeatureLevel> featureLevels = stackalloc FeatureLevel[1]
        {
            FeatureLevel.Level_11_0
        };

        HResult result = D3D11CreateDevice(
            (IDXGIAdapter*)adapter.Get(),
            DriverType.Unknown,
            creationFlags,
            featureLevels,
            tempDevice.GetAddressOf(),
            &featureLevel,
            tempContext.GetAddressOf()
            );

        if (result.Failure)
        {
            throw new InvalidOperationException("Failed to create D3D11 device");
        }

        _featureLevel = featureLevel;

#if DEBUG
        using ComPtr<ID3D11Debug> d3dDebug = default;
        if (tempDevice.CopyTo(&d3dDebug).Success)
        {
            using ComPtr<ID3D11InfoQueue> d3dInfoQueue = default;
            if (d3dDebug.CopyTo(&d3dInfoQueue).Success)
            {
                d3dInfoQueue.Get()->SetBreakOnSeverity(MessageSeverity.Corruption, true);
                d3dInfoQueue.Get()->SetBreakOnSeverity(MessageSeverity.Error, true);

                MessageId* hide = stackalloc MessageId[1]
                {
                    MessageId.SetPrivateDataChangingParams,
                };

                InfoQueueFilter filter = new();
                filter.DenyList.NumIDs = 1u;
                filter.DenyList.pIDList = hide;
                d3dInfoQueue.Get()->AddStorageFilterEntries(&filter);
            }
        }
#endif

        ThrowIfFailed(tempDevice.CopyTo(_device.GetAddressOf()));
        ThrowIfFailed(tempContext.CopyTo(_context.GetAddressOf()));

        SDL_WindowFlags flags = SDL_WINDOW_ALLOW_HIGHDPI | SDL_WINDOW_HIDDEN | SDL_WINDOW_RESIZABLE;

        SDL_Window window = SDL_CreateWindow("Physically Based Rendering (Direct3D 11)",
            SDL_WINDOWPOS_CENTERED,
            SDL_WINDOWPOS_CENTERED,
            width, height, flags);

        SDL_SysWMinfo info = new();
        SDL_VERSION(out info.version);
        SDL_GetWindowWMInfo(window, ref info);
        Guard.IsTrue(info.subsystem == SDL_SYSWM_TYPE.SDL_SYSWM_WINDOWS);

        SwapChainDescription1 swapChainDesc = new()
        {
            Width = (uint)width,
            Height = (uint)height,
            Format = ColorFormat,
            BufferCount = 2u,
            BufferUsage = Win32.Graphics.Dxgi.Usage.RenderTargetOutput,
            SampleDesc = SampleDescription.Default,
            Scaling = Scaling.Stretch,
            SwapEffect = SwapEffect.FlipDiscard,
            AlphaMode = AlphaMode.Ignore,
            Flags = _isTearingSupported ? SwapChainFlags.AllowTearing : SwapChainFlags.None
        };

        SwapChainFullscreenDescription fsSwapChainDesc = new()
        {
            Windowed = true
        };

        result = _dxgiFactory.Get()->CreateSwapChainForHwnd(
             (IUnknown*)_device.Get(),
            info.info.win.window,
            &swapChainDesc,
            &fsSwapChainDesc,
            null,
            _swapChain.GetAddressOf()
            );
        ThrowIfFailed(result);

        _dxgiFactory.Get()->MakeWindowAssociation(info.info.win.window, WindowAssociationFlags.NoAltEnter);

        ThrowIfFailed(
            _swapChain.Get()->GetBuffer(0, __uuidof<ID3D11Texture2D>(), _backBufferTexture.GetVoidAddressOf())
            );

        ThrowIfFailed(_device.Get()->CreateRenderTargetView(
          (ID3D11Resource*)_backBufferTexture.Get(), null, _backBufferRTV.GetAddressOf()));

        return window;
    }

    public void Render(in SDL_Window window)
    {
        Vector4 clearColor = new(1.0f, 0.0f, 0.0f, 1.0f);
        _context.Get()->ClearRenderTargetView(_backBufferRTV.Get(), (float*)&clearColor);
        _context.Get()->OMSetRenderTargets(1, _backBufferRTV.GetAddressOf(), null);

        _swapChain.Get()->Present(1, 0);
    }

#if DEBUG
    static unsafe bool SdkLayersAvailable()
    {
        HResult hr = D3D11CreateDevice(
            null,
            DriverType.Null,       // There is no need to create a real hardware device.
            IntPtr.Zero,
            CreateDeviceFlags.Debug,  // Check for the SDK layers.
            null,                    // Any feature level will do.
            0,
            D3D11_SDK_VERSION,
            null,                    // No need to keep the D3D device reference.
            null,                    // No need to know the feature level.
            null                     // No need to keep the D3D device context reference.
            );

        return hr.Success;
    }
#endif
}
