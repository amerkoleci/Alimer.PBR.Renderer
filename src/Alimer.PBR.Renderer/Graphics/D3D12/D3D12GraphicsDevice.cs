// Copyright © Amer Koleci and Contributors.
// Licensed under the MIT License (MIT). See LICENSE in the repository root for more information.

using System.Drawing;
using Alimer.Bindings.SDL;
using CommunityToolkit.Diagnostics;
using Win32;
using Win32.Graphics.Direct3D;
using Win32.Graphics.Direct3D12;
using Win32.Graphics.Dxgi;
using Win32.Graphics.Dxgi.Common;
using static Alimer.Bindings.SDL.SDL;
using static Alimer.Bindings.SDL.SDL.SDL_WindowFlags;
using static Win32.Apis;
using static Win32.Graphics.Direct3D12.Apis;
using static Win32.Graphics.Dxgi.Apis;
using Feature = Win32.Graphics.Direct3D12.Feature;
using InfoQueueFilter = Win32.Graphics.Direct3D12.InfoQueueFilter;
using MessageId = Win32.Graphics.Direct3D12.MessageId;

namespace Alimer.Graphics.D3D12;

public sealed unsafe class D3D12GraphicsDevice : GraphicsDevice
{
    private static readonly FeatureLevel s_minFeatureLevel = FeatureLevel.Level_11_0;

    private readonly ComPtr<IDXGIFactory4> _dxgiFactory;
    private readonly bool _isTearingSupported;
    private readonly ComPtr<ID3D12Device> _device;
    private readonly ComPtr<ID3D12CommandQueue> _graphicsQueue;

    private readonly FeatureLevel _featureLevel = FeatureLevel.Level_9_1;
    private readonly ComPtr<IDXGISwapChain3> _swapChain;
    private D3D12Texture? _colorTexture;

    public Size Size { get; private set; }
    public TextureFormat ColorFormat { get; } = TextureFormat.Bgra8Unorm;
    public ID3D12Device* NativeDevice => _device;
    public ID3D12CommandQueue* GraphicsQueue => _graphicsQueue;

    public override CommandContext DefaultContext { get; }

    public override Texture ColorTexture => _colorTexture!;

    public override TextureSampleCount SampleCount { get; }

    public D3D12GraphicsDevice(in SDL_Window window, TextureSampleCount maxSamples = TextureSampleCount.Count4)
        : base(window, GraphicsBackend.Direct3D12)
    {
        uint dxgiFactoryFlags = 0u;

#if DEBUG
        using ComPtr<ID3D12Debug1> d3d12Debug1 = default;
        if (D3D12GetDebugInterface(__uuidof<ID3D12Debug1>(), d3d12Debug1.GetVoidAddressOf()).Success)
        {
            d3d12Debug1.Get()->EnableDebugLayer();

            dxgiFactoryFlags |= DXGI_CREATE_FACTORY_DEBUG;
        }

        {
            using ComPtr<IDXGIInfoQueue> dxgiInfoQueue = default;
            if (DXGIGetDebugInterface1(0, __uuidof<IDXGIInfoQueue>(), (void**)dxgiInfoQueue.GetAddressOf()).Success)
            {
                dxgiInfoQueue.Get()->SetBreakOnSeverity(DXGI_DEBUG_ALL, InfoQueueMessageSeverity.Error, true);
                dxgiInfoQueue.Get()->SetBreakOnSeverity(DXGI_DEBUG_ALL, InfoQueueMessageSeverity.Corruption, true);
            }
        }
#endif

        HResult hr = CreateDXGIFactory2(dxgiFactoryFlags, __uuidof<IDXGIFactory4>(), _dxgiFactory.GetVoidAddressOf());

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

                // Check to see if the adapter supports Direct3D 12, but don't create the actual device yet.
                if (D3D12CreateDevice((IUnknown*)adapter.Get(), s_minFeatureLevel, __uuidof<ID3D12Device5>(), null).Success)
                {
                    break;
                }
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

                // Check to see if the adapter supports Direct3D 12, but don't create the actual device yet.
                if (D3D12CreateDevice((IUnknown*)adapter.Get(), s_minFeatureLevel, __uuidof<ID3D12Device5>(), null).Success)
                {
                    break;
                }
            }
        }

        // Create the DX12 API device object.
        hr = D3D12CreateDevice(
            (IUnknown*)adapter.Get(),
            s_minFeatureLevel,
            __uuidof<ID3D12Device>(),
            _device.GetVoidAddressOf()
           );
        ThrowIfFailed(hr);
        _featureLevel = s_minFeatureLevel;

#if DEBUG
        using ComPtr<ID3D12InfoQueue> d3dInfoQueue = default;
        if (_device.CopyTo(&d3dInfoQueue).Success)
        {
            d3dInfoQueue.Get()->SetBreakOnSeverity(MessageSeverity.Corruption, true);
            d3dInfoQueue.Get()->SetBreakOnSeverity(MessageSeverity.Error, true);

            uint disabledMessagesCount = 0;
            MessageId* disabledMessages = stackalloc MessageId[16];
            disabledMessages[disabledMessagesCount++] = MessageId.ClearRenderTargetViewMismatchingClearValue;
            disabledMessages[disabledMessagesCount++] = MessageId.ClearDepthStencilViewMismatchingClearValue;
            disabledMessages[disabledMessagesCount++] = MessageId.MapInvalidNullRange;
            disabledMessages[disabledMessagesCount++] = MessageId.UnmapInvalidNullRange;
            disabledMessages[disabledMessagesCount++] = MessageId.ExecuteCommandListsWrongSwapchainBufferReference;
            disabledMessages[disabledMessagesCount++] = MessageId.ResourceBarrierMismatchingCommandListType;
            disabledMessages[disabledMessagesCount++] = MessageId.ExecuteCommandListsGpuWrittenReadbackResourceMapped;

            InfoQueueFilter filter = new();
            filter.DenyList.NumIDs = disabledMessagesCount;
            filter.DenyList.pIDList = disabledMessages;

            // Clear out the existing filters since we're taking full control of them
            d3dInfoQueue.Get()->PushEmptyStorageFilter();

            d3dInfoQueue.Get()->AddStorageFilterEntries(&filter);
        }
#endif

        // Determine maximum supported MSAA level.
        uint samples;
        for (samples = (uint)maxSamples; samples > 1; samples /= 2)
        {
            FeatureDataMultisampleQualityLevels mqlColor = new()
            {
                Format = Format.R16G16B16A16Float,
                SampleCount = samples
            };

            FeatureDataMultisampleQualityLevels mqlDepthStencil = new()
            {
                Format = Format.D24UnormS8Uint,
                SampleCount = samples
            };

            _device.Get()->CheckFeatureSupport(Feature.MultisampleQualityLevels, ref mqlColor);
            _device.Get()->CheckFeatureSupport(Feature.MultisampleQualityLevels, ref mqlDepthStencil);
            if (mqlColor.NumQualityLevels > 0 && mqlDepthStencil.NumQualityLevels > 0)
            {
                break;
            }
        }

        CommandQueueDescription queueDesc = new(CommandListType.Direct, CommandQueuePriority.Normal);
        ThrowIfFailed(_device.Get()->CreateCommandQueue(
            &queueDesc,
            __uuidof<ID3D12CommandQueue>(),
            _graphicsQueue.GetVoidAddressOf())
            );

        DefaultContext = new D3D12CommandContext(this, CommandListType.Direct);
        SampleCount = (TextureSampleCount)samples;

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
                Format = ColorFormat.ToDxgiFormat(),
                BufferCount = (uint)NumFramesInFlight,
                BufferUsage = Usage.RenderTargetOutput,
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

            using ComPtr<IDXGISwapChain1> tempSwapChain = default;
            hr = _dxgiFactory.Get()->CreateSwapChainForHwnd(
                (IUnknown*)_graphicsQueue.Get(),
                info.info.win.window,
                &swapChainDesc,
                &fsSwapChainDesc,
                null,
                tempSwapChain.GetAddressOf()
                );
            ThrowIfFailed(hr);
            ThrowIfFailed(tempSwapChain.CopyTo(_swapChain.GetAddressOf()));

            _dxgiFactory.Get()->MakeWindowAssociation(info.info.win.window, WindowAssociationFlags.NoAltEnter);
            AfterResize();
        }
    }

    protected override void Dispose(bool disposing)
    {
        base.Dispose(disposing);

        if (disposing)
        {
            _colorTexture.Dispose();
            _swapChain.Dispose();
            DefaultContext.Dispose();
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
        if (_colorTexture is not null)
        {
        }

        SwapChainDescription1 swapChainDesc;
        ThrowIfFailed(_swapChain.Get()->GetDesc1(&swapChainDesc));

        Size = new Size((int)swapChainDesc.Width, (int)swapChainDesc.Height);

        //TextureDescription colorTextureDesc = TextureDescription.Texture2D(ColorFormat, Size.Width, Size.Height, 1, TextureUsage.RenderTarget);
        //
        //ID3D11Texture2D* d3dHandle = default;
        //ThrowIfFailed(
        //    _swapChain.Get()->GetBuffer(0, __uuidof<ID3D11Texture2D>(), (void**)&d3dHandle)
        //    );
        //_colorTexture = new D3D12Texture(this, colorTextureDesc, d3dHandle);
    }

    public override bool BeginFrame()
    {
        return true;
    }

    public override void EndFrame()
    {
        _swapChain.Get()->Present(1, 0);
    }

    protected override GraphicsBuffer CreateBufferCore(in BufferDescription description, void* initialData)
    {
        return new D3D12Buffer(this, description, initialData);
    }

    protected override Texture CreateTextureCore(in TextureDescription description, void* initialData)
    {
        return new D3D12Texture(this, description, initialData);
    }

    protected override Sampler CreateSamplerCore(in SamplerDescription description)
    {
        return new D3D12Sampler(this, description);
    }

    public override Pipeline CreateComputePipeline(in ComputePipelineDescription description)
    {
        return new D3D12Pipeline(this, description);
    }

    public override Pipeline CreateRenderPipeline(in RenderPipelineDescription description)
    {
        return new D3D12Pipeline(this, description);
    }
}
