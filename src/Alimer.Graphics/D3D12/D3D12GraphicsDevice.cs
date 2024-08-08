// Copyright (c) Amer Koleci and Contributors.
// Licensed under the MIT License (MIT). See LICENSE in the repository root for more information.

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

internal sealed unsafe class D3D12GraphicsDevice : GraphicsDevice
{
    private readonly D3D12GraphicsFactory _factory;
    private readonly ComPtr<IDXGIAdapter1> _adapter;
    private readonly ComPtr<ID3D12Device> _device;
    private readonly GraphicsDeviceLimits _limits;

    private readonly ComPtr<ID3D12CommandQueue> _graphicsQueue;
    private readonly ComPtr<ID3D12Fence> _frameFence;
    private readonly ulong[] _fenceValues = new ulong[NumFramesInFlight];
    private uint _frameIndex;
    private ulong _frameCount;

    private readonly FeatureLevel _featureLevel = FeatureLevel.Level_11_0;
    private readonly RootSignatureVersion _rootSignatureVersion = RootSignatureVersion.V1_0;

    private readonly ComPtr<ID3D12RootSignature> _computeRootSignature;

    private readonly D3D12DescriptorAllocator _resourceAllocator;
    private readonly D3D12DescriptorAllocator _samplerAllocator;
    private readonly D3D12DescriptorAllocator _rtvAllocator;
    private readonly D3D12DescriptorAllocator _dsvAllocator;

    private readonly object _destroyLock = new();
    private readonly Queue<Tuple<ComPtr<IUnknown>, ulong>> _deferredReleases = new();
    private bool _shuttingDown;

    public D3D12GraphicsFactory Factory => _factory;
    public ID3D12Device* NativeDevice => _device;
    public ID3D12CommandQueue* GraphicsQueue => _graphicsQueue;
    public ID3D12RootSignature* ComputeRootSignature => _computeRootSignature;

    public uint FrameIndex => _frameIndex;

    /// <inheritdoc />
    public override GraphicsDeviceLimits Limits => _limits;

    public override CommandContext DefaultContext { get; }

    public D3D12GraphicsDevice(D3D12GraphicsFactory factory, ComPtr<IDXGIAdapter1> adapter, in GraphicsDeviceDescription description)
        : base(in description)
    {
        _factory = factory;
        _adapter = adapter.Move();

        // Create the DX12 API device object.
        HResult hr = D3D12CreateDevice(
            (IUnknown*)adapter.Get(),
            D3D12GraphicsFactory.MinFeatureLevel,
            __uuidof<ID3D12Device>(),
            _device.GetVoidAddressOf()
           );
        ThrowIfFailed(hr);

#if DEBUG
        using ComPtr<ID3D12InfoQueue> d3dInfoQueue = default;
        if (_device.CopyTo(&d3dInfoQueue).Success)
        {
            d3dInfoQueue.Get()->SetBreakOnSeverity(MessageSeverity.Corruption, true);
            d3dInfoQueue.Get()->SetBreakOnSeverity(MessageSeverity.Error, true);

            MessageSeverity* enabledSeverities = stackalloc MessageSeverity[5];

            // These severities should be seen all the time
            uint enabledSeveritiesCount = 0;
            enabledSeverities[enabledSeveritiesCount++] = MessageSeverity.Corruption;
            enabledSeverities[enabledSeveritiesCount++] = MessageSeverity.Error;
            enabledSeverities[enabledSeveritiesCount++] = MessageSeverity.Warning;
            enabledSeverities[enabledSeveritiesCount++] = MessageSeverity.Message;

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
            filter.AllowList.NumSeverities = enabledSeveritiesCount;
            filter.AllowList.pSeverityList = enabledSeverities;
            filter.DenyList.NumIDs = disabledMessagesCount;
            filter.DenyList.pIDList = disabledMessages;

            // Clear out the existing filters since we're taking full control of them
            d3dInfoQueue.Get()->PushEmptyStorageFilter();

            d3dInfoQueue.Get()->AddStorageFilterEntries(&filter);
        }
#endif

        _limits = new GraphicsDeviceLimits
        {
            MaxTextureDimension1D = D3D12_REQ_TEXTURE1D_U_DIMENSION,
            MaxTextureDimension2D = D3D12_REQ_TEXTURE2D_U_OR_V_DIMENSION,
            MaxTextureDimension3D = D3D12_REQ_TEXTURE3D_U_V_OR_W_DIMENSION,
            MaxTextureDimensionCube = D3D12_REQ_TEXTURECUBE_DIMENSION,
            MaxTextureArrayLayers = D3D12_REQ_TEXTURE2D_ARRAY_AXIS_DIMENSION,
            MaxBufferSize = D3D12_REQ_RESOURCE_SIZE_IN_MEGABYTES_EXPRESSION_A_TERM * 1024u * 1024u,
            MinConstantBufferOffsetAlignment = D3D12_CONSTANT_BUFFER_DATA_PLACEMENT_ALIGNMENT,
            MaxConstantBufferBindingSize = D3D12_REQ_IMMEDIATE_CONSTANT_BUFFER_ELEMENT_COUNT * 16,
            MinStorageBufferOffsetAlignment = D3D12_RAW_UAV_SRV_BYTE_ALIGNMENT,
            MaxStorageBufferBindingSize = (1u << (int)D3D12_REQ_BUFFER_RESOURCE_TEXEL_COUNT_2_TO_EXP) - 1,
        };

        CommandQueueDescription queueDesc = new(CommandListType.Direct, CommandQueuePriority.Normal);
        ThrowIfFailed(_device.Get()->CreateCommandQueue(
            &queueDesc,
            __uuidof<ID3D12CommandQueue>(),
            _graphicsQueue.GetVoidAddressOf())
            );

        ThrowIfFailed(_device.Get()->CreateFence(
            0,
            FenceFlags.None,
            __uuidof<ID3D12Fence>(),
            _frameFence.GetVoidAddressOf())
            );

        DefaultContext = new D3D12CommandContext(this, CommandListType.Direct);
        //SampleCount = (TextureSampleCount)samples;

        // Create CPU descriptor allocators
        _resourceAllocator = new D3D12DescriptorAllocator(NativeDevice, DescriptorHeapType.CbvSrvUav, 4096);
        _samplerAllocator = new D3D12DescriptorAllocator(NativeDevice, DescriptorHeapType.Sampler, 256);
        _rtvAllocator = new D3D12DescriptorAllocator(NativeDevice, DescriptorHeapType.Rtv, 512);
        _dsvAllocator = new D3D12DescriptorAllocator(NativeDevice, DescriptorHeapType.Dsv, 256);

        // Init caps
        {
            ReadOnlySpan<FeatureLevel> featureLevels =
            [
                FeatureLevel.Level_12_2,
                FeatureLevel.Level_12_1,
                FeatureLevel.Level_11_1,
                FeatureLevel.Level_11_0
            ];
            _featureLevel = _device.Get()->CheckMaxSupportedFeatureLevel(featureLevels);
            _rootSignatureVersion = _device.Get()->CheckHighestRootSignatureVersionl();
        }

        // Create universal compute root signature.
        {
            StaticSamplerDescription computeSamplerDesc = new(0, Filter.MinMagMipLinear);

            DescriptorRange1* descriptorRanges = stackalloc DescriptorRange1[2];
            descriptorRanges[0] = new DescriptorRange1(DescriptorRangeType.Srv, 1, 0, 0, DescriptorRangeFlags.DataStatic);
            descriptorRanges[1] = new DescriptorRange1(DescriptorRangeType.Uav, 1, 0, 0, DescriptorRangeFlags.DataStaticWhileSetAtExecute);

            RootParameter1* rootParameters = stackalloc RootParameter1[3];
            rootParameters[0].InitAsDescriptorTable(1, &descriptorRanges[0]);
            rootParameters[1].InitAsDescriptorTable(1, &descriptorRanges[1]);
            rootParameters[2].InitAsConstants(1, 0);

            VersionedRootSignatureDescription signatureDesc = new();
            signatureDesc.Init_1_1(3, rootParameters, 1, &computeSamplerDesc);

            _computeRootSignature = CreateRootSignature(signatureDesc);
        }
    }

    protected override void Dispose(bool disposing)
    {
        if (disposing)
        {
            WaitForGPU();

            _shuttingDown = true;

            _frameCount = ulong.MaxValue;
            ProcessDeletionQueue();
            _frameCount = 0;


            _resourceAllocator.Shutdown();
            _samplerAllocator.Shutdown();
            _rtvAllocator.Shutdown();
            _dsvAllocator.Shutdown();
            _frameFence.Dispose();

            _computeRootSignature.Dispose();

            DefaultContext.Dispose();

            _graphicsQueue.Dispose();
            _device.Dispose();
        }
    }

    public void DeferDestroy(IUnknown* resource)
    {
        if (resource == null)
        {
            return;
        }

        if (_shuttingDown)
        {
            resource->Release();
            //SafeRelease(allocation);
            return;
        }

        lock (_destroyLock)
        {
            _deferredReleases.Enqueue(Tuple.Create(new ComPtr<IUnknown>(resource), _frameCount));
            //if (allocation != nullptr)
            //{
            //    deferredAllocations.push_back(std::make_pair(allocation, frameCount));
            //}
        }
    }

    public void ProcessDeletionQueue()
    {
        lock (_destroyLock)
        {
            while (_deferredReleases.Count > 0)
            {
                var pair = _deferredReleases.Peek();
                if (pair.Item2 + (ulong)NumFramesInFlight < _frameCount)
                {
                    pair = _deferredReleases.Dequeue();
                    //pair.Item1.Dispose();
                }
                else
                {
                    break;
                }
            }
        }
    }

    public CpuDescriptorHandle AllocateDescriptor(DescriptorHeapType type)
    {
        switch (type)
        {
            case DescriptorHeapType.CbvSrvUav:
                return _resourceAllocator.Allocate();
            case DescriptorHeapType.Sampler:
                return _samplerAllocator.Allocate();
            case DescriptorHeapType.Rtv:
                return _rtvAllocator.Allocate();
            case DescriptorHeapType.Dsv:
                return _dsvAllocator.Allocate();
            default:
                throw new Exception();
        }
    }

    public void FreeDescriptor(DescriptorHeapType type, in CpuDescriptorHandle handle)
    {
        if (handle.ptr == 0)
            return;

        switch (type)
        {
            case DescriptorHeapType.CbvSrvUav:
                _resourceAllocator.Free(handle);
                break;
            case DescriptorHeapType.Sampler:
                _samplerAllocator.Free(handle);
                break;
            case DescriptorHeapType.Rtv:
                _rtvAllocator.Free(handle);
                break;
            case DescriptorHeapType.Dsv:
                _dsvAllocator.Free(handle);
                break;
            default:
                break;
        }
    }

    public uint GetDescriptorSize(DescriptorHeapType type)
    {
        switch (type)
        {
            //case DescriptorHeapType.CbvSrvUav:
            //    return _resourceAllocator.DescriptorSize;
            //case DescriptorHeapType.Sampler:
            //    return _samplerAllocator.DescriptorSize;
            case DescriptorHeapType.Rtv:
                return _rtvAllocator.DescriptorSize;
            case DescriptorHeapType.Dsv:
                return _dsvAllocator.DescriptorSize;
            default:
                return 0;
        }
    }

    public ComPtr<ID3D12RootSignature> CreateRootSignature(VersionedRootSignatureDescription desc)
    {
        RootSignatureFlags standardFlags =
            RootSignatureFlags.DenyDomainShaderRootAccess |
            RootSignatureFlags.DenyGeometryShaderRootAccess |
            RootSignatureFlags.DenyHullShaderRootAccess;

        switch (desc.Version)
        {
            case RootSignatureVersion.V1_0:
                desc.Desc_1_0.Flags |= standardFlags;
                break;
            case RootSignatureVersion.V1_1:
                desc.Desc_1_1.Flags |= standardFlags;
                break;
        }


        using ComPtr<ID3DBlob> signatureBlob = default;
        using ComPtr<ID3DBlob> errorBlob = default;
        if (D3D12SerializeVersionedRootSignature(&desc, _rootSignatureVersion, signatureBlob.GetAddressOf(), errorBlob.GetAddressOf()).Failure)
        {
            throw new InvalidOperationException("Failed to serialize root signature");
        }

        using ComPtr<ID3D12RootSignature> rootSignature = default;
        if (_device.Get()->CreateRootSignature(0,
            signatureBlob.Get()->GetBufferPointer(),
            signatureBlob.Get()->GetBufferSize(),
            __uuidof<ID3D12RootSignature>(),
            rootSignature.GetVoidAddressOf()).Failure)
        {
            throw new InvalidOperationException("Failed to create root signature");
        }

        return rootSignature.Move();
    }

    public override TextureSampleCount QueryMaxTextureSampleCount(TextureFormat format)
    {
        Format dxgiFormat = format.ToDxgiFormat();
        if (dxgiFormat == Format.Unknown)
        {
            return TextureSampleCount.Count1;
        }

        // Determine maximum supported MSAA level.
        uint samples;
        for (samples = (uint)TextureSampleCount.Count64; samples > 1; samples /= 2)
        {
            FeatureDataMultisampleQualityLevels mqlColor = new()
            {
                Format = dxgiFormat,
                SampleCount = samples
            };

            _device.Get()->CheckFeatureSupport(Feature.MultisampleQualityLevels, ref mqlColor);

            if (mqlColor.NumQualityLevels > 0)
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
#if TODO
        //_swapChain.Get()->Present(1, 0);

        ulong prevFrameFenceValue = _fenceValues[_frameIndex];
        _frameIndex = _swapChain.Get()->GetCurrentBackBufferIndex();
        ref ulong currentFrameFenceValue = ref _fenceValues[_frameIndex];

        _graphicsQueue.Get()->Signal(_frameFence.Get(), prevFrameFenceValue);

        if (_frameFence.Get()->GetCompletedValue() < currentFrameFenceValue)
        {
            _frameFence.Get()->SetEventOnCompletion(currentFrameFenceValue, Win32.Handle.Null);
        }

        currentFrameFenceValue = prevFrameFenceValue + 1;
        _frameCount++;

        ProcessDeletionQueue(); 
#endif
    }

    public void WaitForGPU()
    {
        ref ulong fenceValue = ref _fenceValues[_frameIndex];
        ++fenceValue;

        _graphicsQueue.Get()->Signal(_frameFence.Get(), fenceValue);
        _frameFence.Get()->SetEventOnCompletion(fenceValue, Win32.Handle.Null);
        ProcessDeletionQueue();
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

    protected override SwapChain CreateSwapChainCore(SurfaceSource surface, in SwapChainDescription description) => throw new NotImplementedException();
}
