// Copyright © Amer Koleci and Contributors.
// Licensed under the MIT License (MIT). See LICENSE in the repository root for more information.

using System.Drawing;
using Alimer.Bindings.SDL;
using CommunityToolkit.Diagnostics;
using Win32;
using Win32.Graphics.Direct3D;
using Win32.Graphics.Direct3D.Fxc;
using Win32.Graphics.Direct3D11;
using Win32.Graphics.Dxgi;
using Win32.Graphics.Dxgi.Common;
using static Alimer.Bindings.SDL.SDL;
using static Alimer.Bindings.SDL.SDL.SDL_WindowFlags;
using static Win32.Apis;
using static Win32.Graphics.Direct3D.Fxc.Apis;
using static Win32.Graphics.Direct3D11.Apis;
using static Win32.Graphics.Dxgi.Apis;
using InfoQueueFilter = Win32.Graphics.Direct3D11.InfoQueueFilter;
using MessageId = Win32.Graphics.Direct3D11.MessageId;

namespace Alimer.Graphics.D3D11;

public sealed unsafe class D3D11GraphicsDevice : GraphicsDevice
{
    private readonly ComPtr<IDXGIFactory2> _dxgiFactory;
    private readonly bool _isTearingSupported;
    private readonly ComPtr<ID3D11Device1> _device;
    private readonly ComPtr<ID3D11DeviceContext1> _context;
    private readonly FeatureLevel _featureLevel = FeatureLevel.Level_9_1;
    private readonly ComPtr<IDXGISwapChain1> _swapChain;
    private D3D11Texture? _colorTexture;

    public Size Size { get; private set; }
    public TextureFormat ColorFormat { get; } = TextureFormat.Bgra8Unorm;
    public ID3D11Device1* NativeDevice => _device;
    public ID3D11DeviceContext1* NativeContext => _context;

    public override CommandContext DefaultContext { get; }

    public override Texture ColorTexture => _colorTexture!;

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

        DefaultContext = new D3D11CommandContext(this);
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
                Format = ColorFormat.ToDxgiFormat(),
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
            _colorTexture.Dispose();
            _swapChain.Dispose();
            DefaultContext.Dispose();
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
        if (_colorTexture is not null)
        {
        }

        SwapChainDescription1 swapChainDesc;
        ThrowIfFailed(_swapChain.Get()->GetDesc1(&swapChainDesc));

        Size = new Size((int)swapChainDesc.Width, (int)swapChainDesc.Height);

        TextureDescription colorTextureDesc = TextureDescription.Texture2D(ColorFormat, Size.Width, Size.Height, 1, TextureUsage.RenderTarget);

        ID3D11Texture2D* d3dHandle = default;
        ThrowIfFailed(
            _swapChain.Get()->GetBuffer(0, __uuidof<ID3D11Texture2D>(), (void**)&d3dHandle)
            );
        _colorTexture = new D3D11Texture(this, colorTextureDesc, d3dHandle);
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
        return new D3D11Buffer(this, description, initialData);
    }

    protected override Texture CreateTextureCore(in TextureDescription description, void* initialData)
    {
        return new D3D11Texture(this, description, initialData);
    }

    protected override Sampler CreateSamplerCore(in SamplerDescription description)
    {
        return new D3D11Sampler(this, description);
    }

    public override Pipeline CreateComputePipeline(in ComputePipelineDescription description)
    {
        return new D3D11Pipeline(this, description);
    }

    public override Pipeline CreateRenderPipeline(in RenderPipelineDescription description)
    {
        return new D3D11Pipeline(this, description);
    }

    public static ReadOnlyMemory<byte> CompileBytecode(string shaderSource, string entryPoint, string profile)
    {
        CompileFlags shaderFlags = CompileFlags.EnableStrictness;
#if DEBUG
        shaderFlags |= CompileFlags.Debug;
        shaderFlags |= CompileFlags.SkipValidation;
#else
        shaderFlags |= CompileFlags.OptimizationLevel3;
#endif

        using ComPtr<ID3DBlob> bytecode = D3DCompile(shaderSource, entryPoint, profile, shaderFlags);
        Span<byte> result = new byte[bytecode.Get()->GetBufferSize()];
        new Span<byte>(bytecode.Get()->GetBufferPointer(), (int)bytecode.Get()->GetBufferSize()).CopyTo(result);
        return result.ToArray();
#if TODO

        var shaderSourceUtf8 = Encoding.UTF8.GetBytes(shaderSource);
        var entryPointUtf8 = Encoding.UTF8.GetBytes(entryPoint);
        var profileUtf8 = Encoding.UTF8.GetBytes(profile);

        using ComPtr<ID3DBlob> d3dBlobBytecode = default;
        using ComPtr<ID3DBlob> d3dBlobErrors = default;

        fixed (byte* sourcePtr = shaderSourceUtf8)
        fixed (byte* entryPointPtr = entryPointUtf8)
        fixed (byte* targetPtr = profileUtf8)
        {
            HResult hr = D3DCompile(
                pSrcData: sourcePtr,
                SrcDataSize: (nuint)shaderSourceUtf8.Length,
                pSourceName: null,
                pDefines: null,
                pInclude: D3D_COMPILE_STANDARD_FILE_INCLUDE,
                pEntrypoint: (sbyte*)entryPointPtr,
                pTarget: (sbyte*)targetPtr,
                Flags1: shaderFlags,
                Flags2: 0u,
                ppCode: d3dBlobBytecode.GetAddressOf(),
                ppErrorMsgs: d3dBlobErrors.GetAddressOf()
                );

            if (hr.Failure)
            {
                // Throw if an error was retrieved, then also double check the HRESULT
                if (d3dBlobErrors.Get() is not null)
                {
                    string message = new((sbyte*)d3dBlobErrors.Get()->GetBufferPointer());
                }
            }

            ThrowIfFailed(hr);

            Span<byte> result = new byte[d3dBlobBytecode.Get()->GetBufferSize()];
            new Span<byte>(d3dBlobBytecode.Get()->GetBufferPointer(), (int)d3dBlobBytecode.Get()->GetBufferSize()).CopyTo(result);
            return result.ToArray();
        }

#endif

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
