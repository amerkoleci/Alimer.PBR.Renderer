// Copyright (c) Amer Koleci and Contributors
// Licensed under the MIT License (MIT). See LICENSE in the repository root for more information.

using System.Drawing;
using Win32;
using Win32.Graphics.Direct3D;
using Win32.Graphics.Direct3D11;
using Win32.Graphics.Dxgi;
using Win32.Graphics.Dxgi.Common;
using static Win32.Apis;
using static Win32.Graphics.Direct3D11.Apis;
using static Win32.Graphics.Dxgi.Apis;
using InfoQueueFilter = Win32.Graphics.Direct3D11.InfoQueueFilter;
using MessageId = Win32.Graphics.Direct3D11.MessageId;

namespace Alimer.Graphics.D3D11;

internal sealed unsafe class D3D11GraphicsDevice : GraphicsDevice
{
    private readonly D3D11GraphicsFactory _factory;
    private readonly ComPtr<IDXGIAdapter1> _adapter;
    private readonly ComPtr<ID3D11Device1> _device;
    private readonly ComPtr<ID3D11DeviceContext1> _context;
    private readonly FeatureLevel _featureLevel = FeatureLevel.Level_9_1;

    private readonly GraphicsDeviceLimits _limits;

    public D3D11GraphicsFactory Factory => _factory;

    public ID3D11Device1* NativeDevice => _device;
    public ID3D11DeviceContext1* NativeContext => _context;

    /// <inheritdoc />
    public override GraphicsDeviceLimits Limits => _limits;

    public override CommandContext DefaultContext { get; }

    public D3D11GraphicsDevice(D3D11GraphicsFactory factory, ComPtr<IDXGIAdapter1> adapter, in GraphicsDeviceDescription description)
        : base(in description)
    {
        _factory = factory;
        _adapter = adapter.Move();

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

        ReadOnlySpan<FeatureLevel> featureLevels =
        [
            FeatureLevel.Level_11_0
        ];

        HResult result = D3D11CreateDevice(
            (IDXGIAdapter*)_adapter.Get(),
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

        _limits = new GraphicsDeviceLimits
        {
            MaxTextureDimension1D = D3D11_REQ_TEXTURE1D_U_DIMENSION,
            MaxTextureDimension2D = D3D11_REQ_TEXTURE2D_U_OR_V_DIMENSION,
            MaxTextureDimension3D = D3D11_REQ_TEXTURE3D_U_V_OR_W_DIMENSION,
            MaxTextureDimensionCube = D3D11_REQ_TEXTURECUBE_DIMENSION,
            MaxTextureArrayLayers = D3D11_REQ_TEXTURE2D_ARRAY_AXIS_DIMENSION,
            MaxBufferSize = D3D11_REQ_RESOURCE_SIZE_IN_MEGABYTES_EXPRESSION_A_TERM * 1024u * 1024u,
            MinConstantBufferOffsetAlignment = 256, // D3D12_CONSTANT_BUFFER_DATA_PLACEMENT_ALIGNMENT,
            MaxConstantBufferBindingSize = D3D11_REQ_IMMEDIATE_CONSTANT_BUFFER_ELEMENT_COUNT * 16,
            MinStorageBufferOffsetAlignment = D3D11_RAW_UAV_SRV_BYTE_ALIGNMENT,
            MaxStorageBufferBindingSize = (1u << (int)D3D11_REQ_BUFFER_RESOURCE_TEXEL_COUNT_2_TO_EXP) - 1,
        };

        DefaultContext = new D3D11CommandContext(this);
    }

    protected override void Dispose(bool disposing)
    {
        _context.Get()->Flush();

        if (disposing)
        {
            DefaultContext.Dispose();
            _context.Dispose();
            _device.Dispose();
            _adapter.Dispose();
        }
    }

    public override TextureSampleCount QueryMaxTextureSampleCount(TextureFormat format)
    {
        Format dxgiFormat = format.ToDxgiFormat();
        if(dxgiFormat == Format.Unknown)
        {
            return TextureSampleCount.Count1;
        }

        // Determine maximum supported MSAA level.
        uint samples;
        for (samples = (uint)TextureSampleCount.Count64; samples > 1; samples /= 2)
        {
            uint qualityLevels;
            _device.Get()->CheckMultisampleQualityLevels(dxgiFormat, samples, &qualityLevels);
            if (qualityLevels > 0)
            {
                break;
            }
        }

        return (TextureSampleCount)samples;
    }

    public override bool BeginFrame()
    {
        return true;
    }

    public override void EndFrame()
    {
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

    protected override SwapChain CreateSwapChainCore(SurfaceSource surface, in SwapChainDescription description)
    {
        return new D3D11SwapChain(this, surface, description);
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
