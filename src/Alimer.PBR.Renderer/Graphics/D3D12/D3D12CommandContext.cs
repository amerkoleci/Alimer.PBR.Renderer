﻿// Copyright © Amer Koleci and Contributors.
// Licensed under the MIT License (MIT). See LICENSE in the repository root for more information.

using Vortice.Mathematics;
using Win32.Graphics.Direct3D12;
using D3DPrimitiveTopology = Win32.Graphics.Direct3D.PrimitiveTopology;
using static Win32.Apis;
using static Win32.Graphics.Direct3D12.Apis;
using System.Runtime.CompilerServices;
using CommunityToolkit.Diagnostics;
using Win32;
using System.Drawing;

namespace Alimer.Graphics.D3D12;

internal sealed unsafe class D3D12CommandContext : CommandContext
{
    private readonly D3D12GraphicsDevice _device;
    private readonly ComPtr<ID3D12CommandAllocator>[] _commandAllocators = new ComPtr<ID3D12CommandAllocator>[GraphicsDevice.NumFramesInFlight];
    private readonly ComPtr<ID3D12GraphicsCommandList> _commandList;

    private D3D12Pipeline? _currentPipeline;
    private D3DPrimitiveTopology _currentPrimitiveTopology;

    private RenderPassDescriptor _currentRenderPass;
    //private readonly ID3D11RenderTargetView*[] _rtvs = new ID3D11RenderTargetView*[D3D11_SIMULTANEOUS_RENDER_TARGET_COUNT];
    //private ID3D11DepthStencilView* DSV = null;
    //private readonly ID3D11Buffer*[] _vertexBindings = new ID3D11Buffer*[8];
    //private uint[] _vertexOffsets = new uint[8];

    public D3D12CommandContext(D3D12GraphicsDevice device, CommandListType type)
    {
        _device = device;

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
        base.Dispose(disposing);

        if (disposing)
        {
            for (int i = 0; i < GraphicsDevice.NumFramesInFlight; i++)
            {
                _commandAllocators[i].Dispose();
            }

            _commandList.Dispose();
        }
    }

    public override void PushDebugGroup(string groupLabel)
    {
        fixed (char* groupLabelPtr = groupLabel)
        {
            //_annotation.Get()->BeginEvent((ushort*)groupLabelPtr);
        }
    }

    public override void PopDebugGroup()
    {
        //_annotation.Get()->EndEvent();
    }

    public override void InsertDebugMarker(string debugLabel)
    {
        fixed (char* debugLabelPtr = debugLabel)
        {
            //_annotation.Get()->SetMarker((ushort*)debugLabelPtr);
        }
    }

    protected override void BeginRenderPassCore(in RenderPassDescriptor renderPass)
    {
#if TODO
        uint numRTVs = 0;
        DSV = default;
        Size renderArea = new(int.MaxValue, int.MaxValue);

        if (!string.IsNullOrEmpty(renderPass.Label))
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

                //_rtvs[numRTVs] = texture.GetRTV(mipLevel, slice);

                switch (attachment.LoadAction)
                {
                    case LoadAction.Load:
                        break;

                    case LoadAction.Clear:
                        Color4 clearColorValue = attachment.ClearColor;
                        _context->ClearRenderTargetView(_rtvs[numRTVs], (float*)&clearColorValue);
                        break;
                    case LoadAction.DontCare:
                        _context->DiscardView((ID3D11View*)_rtvs[numRTVs]);
                        break;
                }

                numRTVs++;
            }
        }

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

        fixed (ID3D11RenderTargetView** RTVS = _rtvs)
        {
            _context->OMSetRenderTargets(numRTVs, RTVS, DSV);
        }

        Win32.Numerics.Viewport viewport = new((float)renderArea.Width, (float)renderArea.Height);
        RawRect scissorRect = new(0, 0, renderArea.Width, renderArea.Height);

        _context->RSSetViewports(1, &viewport);
        _context->RSSetScissorRects(1, &scissorRect); 
#endif

        _currentRenderPass = renderPass;
    }

    protected override void EndRenderPassCore()
    {
#if TODO
        if (_currentRenderPass.ColorAttachments.Length > 0)
        {
            for (int index = 0; index < _currentRenderPass.ColorAttachments.Length; index++)
            {
                ref RenderPassColorAttachment attachment = ref _currentRenderPass.ColorAttachments[index];
                Guard.IsTrue(attachment.Texture is not null);

                D3D12Texture texture = (D3D12Texture)attachment.Texture;
                int mipLevel = attachment.MipLevel;
                int slice = attachment.Slice;
                uint srcSubResource = D3D12CalcSubresource((uint)mipLevel, (uint)slice, (uint)texture.MipLevels);

                switch (attachment.StoreAction)
                {
                    case StoreAction.Store:
                        if (attachment.ResolveTexture is not null)
                        {
                            D3D12Texture resolveTexture = (D3D12Texture)attachment.ResolveTexture!;
                            uint dstSubResource = D3D11CalcSubresource((uint)attachment.ResolveMipLevel, (uint)attachment.ResolveSlice, (uint)resolveTexture.MipLevels);
                            //_context->ResolveSubresource(resolveTexture.Handle, dstSubResource, texture.Handle, srcSubResource, resolveTexture.DxgiFormat);
                        }
                        break;

                    case StoreAction.DontCare:
                        _context->DiscardView((ID3D11View*)_rtvs[index]);
                        break;
                }
            }
        }

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

        if (!string.IsNullOrEmpty(_currentRenderPass.Label))
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
