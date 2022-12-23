// Copyright © Amer Koleci and Contributors.
// Licensed under the MIT License (MIT). See LICENSE in the repository root for more information.

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
using Alimer.Bindings.SDL;
using CommunityToolkit.Diagnostics;
using System.Drawing;
using Vortice.Mathematics;

namespace Alimer.Graphics.D3D11;

public sealed unsafe class D3D11GraphicsDevice : GraphicsDevice
{
    private readonly ComPtr<IDXGIFactory2> _dxgiFactory;
    private readonly bool _isTearingSupported;
    private readonly ComPtr<ID3D11Device1> _device;
    private readonly ComPtr<ID3D11DeviceContext1> _context;
    private readonly FeatureLevel _featureLevel = FeatureLevel.Level_9_1;
    private readonly ComPtr<IDXGISwapChain1> _swapChain;
    private ComPtr<ID3D11Texture2D> _backBufferTexture = default;
    private ComPtr<ID3D11RenderTargetView> _backBufferRTV = default;

    public Size Size { get; private set; }
    public Format ColorFormat { get; } = Format.B8G8R8A8Unorm;
    public ID3D11Device1* NativeDevice => _device;
    public ID3D11DeviceContext1* NativeContext => _context;

    public override int Samples { get; }

    public D3D11GraphicsDevice(in SDL_Window window, int maxSamples = 4)
        : base(window, GraphicsBackend.Direct3D11)
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

        // Determine maximum supported MSAA level.
        uint samples;
        for (samples = (uint)maxSamples; samples > 1; samples /= 2)
        {
            uint colorQualityLevels;
            uint depthStencilQualityLevels;
            _device.Get()->CheckMultisampleQualityLevels(Format.R16G16B16A16Float, samples, &colorQualityLevels);
            _device.Get()->CheckMultisampleQualityLevels(Format.D24UnormS8Uint, samples, &depthStencilQualityLevels);
            if (colorQualityLevels > 0 && depthStencilQualityLevels > 0)
            {
                break;
            }
        }

        Samples = (int)samples;

        // Create SwapChain
        {
            SDL_SysWMinfo info = new();
            SDL_VERSION(out info.version);
            SDL_GetWindowWMInfo(window, ref info);
            Guard.IsTrue(info.subsystem == SDL_SYSWM_TYPE.SDL_SYSWM_WINDOWS);

            bool isFullscreen = (SDL_GetWindowFlags(window) & SDL_WINDOW_FULLSCREEN) != 0;

            SwapChainDescription1 swapChainDesc = new()
            {
                Width = 0u,
                Height = 0u,
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
                Windowed = !isFullscreen
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
            AfterResize();
        }
    }

    protected override void Dispose(bool disposing)
    {
        _context.Get()->Flush();

        base.Dispose(disposing);

        if (disposing)
        {
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
    }

    private void AfterResize()
    {
        if (_backBufferTexture.Get() is not null)
        {
        }

        SwapChainDescription1 swapChainDesc;
        ThrowIfFailed(_swapChain.Get()->GetDesc1(&swapChainDesc));

        Size = new Size((int)swapChainDesc.Width, (int)swapChainDesc.Height);

        ThrowIfFailed(
            _swapChain.Get()->GetBuffer(0, __uuidof<ID3D11Texture2D>(), _backBufferTexture.GetVoidAddressOf())
            );

        ThrowIfFailed(_device.Get()->CreateRenderTargetView(
          (ID3D11Resource*)_backBufferTexture.Get(), null, _backBufferRTV.GetAddressOf())
            );
    }

    public override bool BeginFrame()
    {
        return true;
    }

    public override void EndFrame()
    {
        _swapChain.Get()->Present(1, 0);
    }

    public override Texture CreateTexture(in Size3 size, TextureFormat format, TextureUsage usage = TextureUsage.ShaderRead, int sampleCount = 1)
    {
        return new D3D11Texture(this, size, format, usage, sampleCount);
    }

    public override FrameBuffer CreateFrameBuffer(in Size size, int samples, TextureFormat colorFormat, TextureFormat depthstencilFormat)
    {
        return new D3D11FrameBuffer(this, size, samples, colorFormat, depthstencilFormat);
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
