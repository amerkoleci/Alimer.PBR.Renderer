// Copyright © Amer Koleci and Contributors.
// Licensed under the MIT License (MIT). See LICENSE in the repository root for more information.

using CommunityToolkit.Diagnostics;
using Vortice.Mathematics;
using Win32;
using Win32.Graphics.Direct3D12;
using XenoAtom.Interop;
using static Win32.Apis;
using static Win32.Graphics.Direct3D12.Apis;
using D3DPrimitiveTopology = Win32.Graphics.Direct3D.PrimitiveTopology;

namespace Alimer.Graphics.D3D12;

internal sealed unsafe class D3D12CommandContext : CommandContext
{
    private static readonly ResourceStates s_ValidComputeResourceStates =
        ResourceStates.UnorderedAccess
        | ResourceStates.NonPixelShaderResource
        | ResourceStates.CopyDest
        | ResourceStates.CopySource;

    private readonly D3D12GraphicsDevice _device;
    private readonly CommandListType _type;
    private readonly ComPtr<ID3D12CommandAllocator>[] _commandAllocators = new ComPtr<ID3D12CommandAllocator>[GraphicsDevice.NumFramesInFlight];
    private readonly ComPtr<ID3D12GraphicsCommandList> _commandList;

    private readonly ResourceBarrier[] _resourceBarriers = new ResourceBarrier[16];
    private int _numBarriersToFlush;

    private D3D12Pipeline? _currentPipeline;
    private D3DPrimitiveTopology _currentPrimitiveTopology;

    private RenderPassDescription _currentRenderPass;
    private readonly CpuDescriptorHandle[] _rtvs = new CpuDescriptorHandle[D3D12_SIMULTANEOUS_RENDER_TARGET_COUNT];
    //private ID3D11DepthStencilView* DSV = null;
    //private readonly ID3D11Buffer*[] _vertexBindings = new ID3D11Buffer*[8];
    //private uint[] _vertexOffsets = new uint[8];

    public D3D12CommandContext(D3D12GraphicsDevice device, CommandListType type)
    {
        _device = device;
        _type = type;

        for (int i = 0; i < GraphicsDevice.NumFramesInFlight; i++)
        {
            ThrowIfFailed(device.NativeDevice->CreateCommandAllocator(type,
                __uuidof<ID3D12CommandAllocator>(),
                _commandAllocators[i].GetVoidAddressOf()
                ));
        }

        ThrowIfFailed(device.NativeDevice->CreateCommandList(
            0,
            type,
            _commandAllocators[0].Get(),
            null,
            __uuidof<ID3D12GraphicsCommandList>(),
            _commandList.GetVoidAddressOf()
            ));
    }

    protected override void Dispose(bool disposing)
    {
        if (disposing)
        {
            for (int i = 0; i < GraphicsDevice.NumFramesInFlight; i++)
            {
                _commandAllocators[i].Dispose();
            }

            _commandList.Dispose();
        }
    }

    public void TransitionResource(ID3D11GpuResource resource, ResourceStates newState, bool flushImmediate = false)
    {
        ResourceStates oldState = resource.State;

        if (_type == CommandListType.Compute)
        {
            Guard.IsTrue((oldState & s_ValidComputeResourceStates) == oldState);
            Guard.IsTrue((newState & s_ValidComputeResourceStates) == newState);
        }

        if (oldState != newState)
        {
            Guard.IsTrue(_numBarriersToFlush < _resourceBarriers.Length, "Exceeded arbitrary limit on buffered barriers");
            ref ResourceBarrier barrierDesc = ref _resourceBarriers[_numBarriersToFlush++];

            barrierDesc.Type = ResourceBarrierType.Transition;
            barrierDesc.Transition.pResource = resource.Handle;
            barrierDesc.Transition.Subresource = D3D12_RESOURCE_BARRIER_ALL_SUBRESOURCES;
            barrierDesc.Transition.StateBefore = oldState;
            barrierDesc.Transition.StateAfter = newState;

            // Check to see if we already started the transition
            if (newState == resource.TransitioningState)
            {
                barrierDesc.Flags = ResourceBarrierFlags.EndOnly;
                resource.TransitioningState = (ResourceStates)(-1);
            }
            else
            {
                barrierDesc.Flags = ResourceBarrierFlags.None;
            }

            resource.State = newState;
        }
        else if (newState == ResourceStates.UnorderedAccess)
        {
            InsertUAVBarrier(resource, flushImmediate);
        }

        if (flushImmediate || _numBarriersToFlush == _resourceBarriers.Length)
        {
            FlushResourceBarriers();
        }
    }

    public void InsertUAVBarrier(ID3D11GpuResource resource, bool flushImmediate = false)
    {
        Guard.IsTrue(_numBarriersToFlush < _resourceBarriers.Length, "Exceeded arbitrary limit on buffered barriers");
        ref ResourceBarrier barrierDesc = ref _resourceBarriers[_numBarriersToFlush++];

        barrierDesc.Type = ResourceBarrierType.Uav;
        barrierDesc.Flags = ResourceBarrierFlags.None;
        barrierDesc.UAV.pResource = resource.Handle;

        if (flushImmediate)
            FlushResourceBarriers();
    }

    public void FlushResourceBarriers()
    {
        if (_numBarriersToFlush > 0)
        {
            _commandList.Get()->ResourceBarrier(_numBarriersToFlush, _resourceBarriers);

            _numBarriersToFlush = 0;
        }
    }

    public void Reset()
    {
        _commandList.Get()->Reset(_commandAllocators[_device.FrameIndex].Get(), null);

        _numBarriersToFlush = 0;
    }

    public override void Flush(bool waitForCompletion = false)
    {
        ThrowIfFailed(_commandList.Get()->Close());

        ID3D12GraphicsCommandList* d3d12CommandList = _commandList.Get();
        _device.GraphicsQueue->ExecuteCommandLists(1u, (ID3D12CommandList**)&d3d12CommandList);

        if (waitForCompletion)
        {
            _device.WaitForGPU();
        }

        Reset();
    }

    public override void PushDebugGroup(ReadOnlySpanUtf8 groupLabel, in Color4 color = default)
    {
        // TODO: Use Pix3 (WinPixEventRuntime)

        int bufferSize = PixHelpers.CalculateNoArgsEventSize(groupLabel);
        void* buffer = stackalloc byte[bufferSize];
        PixHelpers.FormatNoArgsEventToBuffer(buffer, PixHelpers.PixEventType.PIXEvent_BeginEvent_NoArgs, 0, groupLabel.ToString()!);
        _commandList.Get()->BeginEvent(PixHelpers.WinPIXEventPIX3BlobVersion, buffer, (uint)bufferSize);
    }

    public override void PopDebugGroup()
    {
        _commandList.Get()->EndEvent();
    }

    public override void InsertDebugMarker(string debugLabel)
    {
        int bufferSize = PixHelpers.CalculateNoArgsEventSize(debugLabel);
        void* buffer = stackalloc byte[bufferSize];
        PixHelpers.FormatNoArgsEventToBuffer(buffer, PixHelpers.PixEventType.PIXEvent_SetMarker_NoArgs, 0, debugLabel);
        _commandList.Get()->SetMarker(PixHelpers.WinPIXEventPIX3BlobVersion, buffer, (uint)bufferSize);
    }

    protected override void BeginRenderPassCore(in RenderPassDescription renderPass)
    {
        uint numRTVs = 0;
        //DSV = default;
        SizeI renderArea = new(int.MaxValue, int.MaxValue);

        if (!renderPass.Label.IsNull)
        {
            PushDebugGroup(renderPass.Label);
        }

        if (renderPass.ColorAttachments.Length > 0)
        {
            for (uint slot = 0; slot < renderPass.ColorAttachments.Length; slot++)
            {
                ref RenderPassColorAttachment attachment = ref renderPass.ColorAttachments[slot];
                Guard.IsTrue(attachment.Texture is not null);

                D3D12Texture texture = (D3D12Texture)attachment.Texture;
                int mipLevel = attachment.MipLevel;
                int slice = attachment.Slice;

                renderArea.Width = Math.Min(renderArea.Width, texture.GetWidth(mipLevel));
                renderArea.Height = Math.Min(renderArea.Height, texture.GetHeight(mipLevel));

                _rtvs[numRTVs] = texture.GetRTV(mipLevel, slice);
                TransitionResource(texture, ResourceStates.RenderTarget, true);

                switch (attachment.LoadAction)
                {
                    case LoadAction.Load:
                        break;

                    case LoadAction.Clear:
                        Color4 clearColorValue = attachment.ClearColor;
                        _commandList.Get()->ClearRenderTargetView(_rtvs[numRTVs], (float*)&clearColorValue, NumRects: 0, pRects: null);
                        break;
                    case LoadAction.Discard:
                        //_commandList.Get()->DiscardResource((ID3D11View*)_rtvs[numRTVs]);
                        break;
                }

                numRTVs++;
            }
        }

#if TODO
        if (renderPass.DepthStencilAttachment.HasValue)
        {
            RenderPassDepthStencilAttachment attachment = renderPass.DepthStencilAttachment.Value;
            Guard.IsTrue(attachment.Texture is not null);

            D3D12Texture texture = (D3D12Texture)attachment.Texture;
            int mipLevel = attachment.MipLevel;
            int slice = attachment.Slice;

            renderArea.Width = Math.Min(renderArea.Width, texture.GetWidth(mipLevel));
            renderArea.Height = Math.Min(renderArea.Height, texture.GetHeight(mipLevel));

            //DSV = texture.GetDSV(mipLevel, slice);

            ClearFlags clearFlags = ClearFlags.None;
            switch (attachment.DepthLoadAction)
            {
                case LoadAction.Load:
                    break;

                case LoadAction.Clear:
                    clearFlags |= ClearFlags.Depth;
                    break;
                case LoadAction.DontCare:
                    _context->DiscardView((ID3D11View*)DSV);
                    break;
            }

            if (texture.Format.IsDepthStencilFormat())
            {
                switch (attachment.StencilLoadAction)
                {
                    case LoadAction.Load:
                        break;

                    case LoadAction.Clear:
                        clearFlags |= ClearFlags.Stencil;
                        break;
                    case LoadAction.DontCare:
                        _context->DiscardView((ID3D11View*)DSV);
                        break;
                }
            }

            if (clearFlags != ClearFlags.None)
            {
                _context->ClearDepthStencilView(DSV, clearFlags, attachment.ClearDepth, (byte)attachment.ClearStencil);
            }
        }
#endif

        fixed (CpuDescriptorHandle* RTVS = _rtvs)
        {
            _commandList.Get()->OMSetRenderTargets(numRTVs, RTVS, RTsSingleHandleToDescriptorRange: false, pDepthStencilDescriptor: null);
        }

        Win32.Numerics.Viewport viewport = new((float)renderArea.Width, (float)renderArea.Height);
        Win32.Numerics.Rect scissorRect = new(0, 0, renderArea.Width, renderArea.Height);
        _commandList.Get()->RSSetViewports(1, &viewport);
        _commandList.Get()->RSSetScissorRects(1, &scissorRect);

        _currentRenderPass = renderPass;
    }

    protected override void EndRenderPassCore()
    {
        if (_currentRenderPass.ColorAttachments.Length > 0)
        {
            for (int index = 0; index < _currentRenderPass.ColorAttachments.Length; index++)
            {
                ref RenderPassColorAttachment attachment = ref _currentRenderPass.ColorAttachments[index];
                Guard.IsTrue(attachment.Texture is not null);

                D3D12Texture texture = (D3D12Texture)attachment.Texture;
                int mipLevel = attachment.MipLevel;
                int slice = attachment.Slice;
                uint srcSubResource = D3D12CalcSubresource((uint)mipLevel, (uint)slice, 0, (uint)texture.MipLevels, (uint)texture.ArrayLayers);

                switch (attachment.StoreAction)
                {
                    case StoreAction.Store:
                        if (attachment.ResolveTexture is not null)
                        {
                            D3D12Texture resolveTexture = (D3D12Texture)attachment.ResolveTexture!;
                            uint dstSubResource = D3D12CalcSubresource((uint)attachment.ResolveMipLevel, (uint)attachment.ResolveSlice, 0, (uint)resolveTexture.MipLevels, (uint)resolveTexture.ArrayLayers);

                            ResourceStates currentTextureState = texture.State;
                            ResourceStates currentResolveState = ResourceStates.PixelShaderResource | ResourceStates.NonPixelShaderResource; //  resolveTexture.State;

                            TransitionResource(texture, ResourceStates.ResolveSource, false);
                            TransitionResource(resolveTexture, ResourceStates.ResolveDest, false);
                            FlushResourceBarriers();

                            _commandList.Get()->ResolveSubresource(resolveTexture.Handle, dstSubResource, texture.Handle, srcSubResource, resolveTexture.DxgiFormat);

                            TransitionResource(texture, currentTextureState, false);
                            TransitionResource(resolveTexture, currentResolveState, false);
                            FlushResourceBarriers();
                        }
                        else
                        {
                            if (texture.IsSwapChain)
                            {
                                TransitionResource(texture, ResourceStates.Present, true);
                            }
                        }

                        break;

                    case StoreAction.Discard:
                        //_commandList.Get()->DiscardView((ID3D11View*)_rtvs[index]);
                        break;
                }
            }
        }


#if TODO
        if (_currentRenderPass.DepthStencilAttachment.HasValue)
        {
            RenderPassDepthStencilAttachment attachment = _currentRenderPass.DepthStencilAttachment.Value;
            Guard.IsTrue(attachment.Texture is not null);

            ClearFlags clearFlags = ClearFlags.None;
            switch (attachment.DepthStoreAction)
            {
                case StoreAction.Store:
                    break;

                case StoreAction.DontCare:
                    _context->DiscardView((ID3D11View*)DSV);
                    break;
            }

            if (attachment.Texture.Format.IsDepthStencilFormat())
            {
                switch (attachment.StencilStoreAction)
                {
                    case StoreAction.Store:
                        break;

                    case StoreAction.DontCare:
                        _context->DiscardView((ID3D11View*)DSV);
                        break;
                }
            }

            if (clearFlags != ClearFlags.None)
            {
                _context->ClearDepthStencilView(DSV, clearFlags, attachment.ClearDepth, (byte)attachment.ClearStencil);
            }
        }

        _context->OMSetRenderTargets(0, null, null); 
#endif

        if (!_currentRenderPass.Label.IsNull)
        {
            PopDebugGroup();
        }
    }

    public override void SetPipeline(Pipeline pipeline)
    {
        D3D12Pipeline d3d12Pipeline = (D3D12Pipeline)pipeline;
        if (_currentPipeline == d3d12Pipeline)
            return;

        _currentPipeline = d3d12Pipeline;

        if (d3d12Pipeline.PipelineType == PipelineType.Render)
        {
            //_commandList.Get()->SetGraphicsRootSignature(d3d12Pipeline.RootSignature);

            if (_currentPrimitiveTopology != d3d12Pipeline.PrimitiveTopology)
            {
                _currentPrimitiveTopology = d3d12Pipeline.PrimitiveTopology;
                //_commandList.Get()->IASetPrimitiveTopology(d3d12Pipeline.PrimitiveTopology);
            }
        }
        else if (d3d12Pipeline.PipelineType == PipelineType.Compute)
        {
            _commandList.Get()->SetComputeRootSignature(d3d12Pipeline.RootSignature);
            _commandList.Get()->SetPipelineState(d3d12Pipeline.Handle);
        }

        //_commandList.Get()->SetPipelineState(d3d12Pipeline.Handle);
    }

    public override void SetVertexBuffer(uint slot, GraphicsBuffer buffer, uint offset = 0)
    {
        //var d3dBuffer = ((D3D12Buffer)buffer);
        //
        //if (_vertexBindings[slot] != d3dBuffer.Handle || _vertexOffsets[slot] != offset)
        //{
        //    _vertexBindings[slot] = d3dBuffer.Handle;
        //    _vertexOffsets[slot] = offset;
        //}
    }

    public override void SetIndexBuffer(GraphicsBuffer buffer, uint offset, IndexType indexType)
    {
        //var d3dBuffer = ((D3D12Buffer)buffer).Handle;
        //_context->IASetIndexBuffer(d3dBuffer, indexType.ToDxgiFormat(), offset);
    }

    public override void SetConstantBuffer(int index, GraphicsBuffer buffer)
    {
        //_constantBuffers[index] = ((D3D12Buffer)buffer).Handle;
        // _numCBVBindings = Math.Max((uint)index + 1, _numCBVBindings);
    }

    public override void SetSampler(int index, Sampler sampler)
    {
        //_samplers[index] = ((D3D12Sampler)sampler).Handle;
        //_numSamplerBindings = Math.Max((uint)index + 1, _numSamplerBindings);
    }

    public override void SetSRV(int index, Texture texture)
    {
        //_srvs[index] = ((D3D12Texture)texture).SRV;
        //_numSRVBindings = Math.Max((uint)index + 1, _numSRVBindings);
    }

    public override void SetUAV(int index, Texture texture, int mipLevel)
    {
        //_uavs[index] = ((D3D12Texture)texture).GetUAV(mipLevel);
        //_numUAVBindings = Math.Max((uint)index + 1, _numUAVBindings);
    }

    public override unsafe void UpdateConstantBuffer(GraphicsBuffer source, void* data, uint size)
    {
#if TODO
        var d3dSource = (D3D12Buffer)source;
        if (d3dSource.IsDynamic)
        {
            MappedSubresource mappedResource;
            if (_context->Map((ID3D11Resource*)d3dSource.Handle, 0, MapMode.WriteDiscard, 0, &mappedResource).Success)
            {
                Unsafe.CopyBlock(mappedResource.pData, data, size);
                //*static_cast<T*>(mappedResource.pData) = value;

                _context->Unmap((ID3D11Resource*)d3dSource.Handle, 0);
            }
        }
        else
        {
            _context->UpdateSubresource((ID3D11Resource*)d3dSource.Handle, 0, null, data, 0, 0);
        } 
#endif
    }

    public override void CopyTexture(Texture source, Texture destination)
    {
        var d3dSource = (D3D12Texture)source;
        var d3dDest = (D3D12Texture)destination;

        //_context->CopyResource(d3dDest.Handle, d3dSource.Handle);
    }

    public override void CopyTexture(Texture source, int sourceArraySlice, Texture destination, int destArraySlice)
    {
        //var d3dSource = (D3D12Texture)source;
        //var srcSubresource = D3D11CalcSubresource(0u, (uint)sourceArraySlice, (uint)source.MipLevels);
        //var d3dDest = (D3D12Texture)destination;
        //var dstSubresource = D3D11CalcSubresource(0u, (uint)destArraySlice, (uint)source.MipLevels);
        // _context->CopySubresourceRegion(d3dDest.Handle, dstSubresource, 0, 0, 0, d3dSource.Handle, srcSubresource, null);
    }

    public override void GenerateMips(Texture texture)
    {
        //_context->GenerateMips(((D3D12Texture)texture).SRV);
    }

    public override void Dispatch(int groupCountX, int groupCountY, int groupCountZ)
    {
        PrepareDispatch();
        //_commandList.Get()->Dispatch((uint)groupCountX, (uint)groupCountY, (uint)groupCountZ);
    }

    public override void Draw(int vertexCount, int instanceCount = 1, int firstVertex = 0, int firstInstance = 0)
    {
        PrepareDraw();

        // _commandList.Get()->DrawInstanced((uint)vertexCount, (uint)instanceCount, (uint)firstVertex, (uint)firstInstance);
    }

    public override void DrawIndexed(int indexCount, int instanceCount = 1, int firstIndex = 0, int baseVertex = 0, int firstInstance = 0)
    {
        PrepareDraw();

        //_commandList.Get()->DrawIndexedInstanced((uint)indexCount, (uint)instanceCount, (uint)firstIndex, baseVertex, (uint)firstInstance);
    }

    private void PrepareDispatch()
    {

    }

    private void PrepareDraw()
    {

    }
}
