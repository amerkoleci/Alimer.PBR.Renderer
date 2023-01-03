// Copyright © Amer Koleci and Contributors.
// Licensed under the MIT License (MIT). See LICENSE in the repository root for more information.

using Vortice.Mathematics;
using Win32.Graphics.Direct3D11;
using D3DPrimitiveTopology = Win32.Graphics.Direct3D.PrimitiveTopology;
using static Win32.Apis;
using static Win32.Graphics.Direct3D11.Apis;
using System.Runtime.CompilerServices;
using CommunityToolkit.Diagnostics;
using Win32;
using System.Drawing;

namespace Alimer.Graphics.D3D11;

internal sealed unsafe class D3D11CommandContext : CommandContext
{
    private readonly D3D11GraphicsDevice _device;
    private readonly ID3D11DeviceContext1* _context;
    private readonly ComPtr<ID3DUserDefinedAnnotation> _annotation;

    private D3D11Pipeline? _currentPipeline;
    private ID3D11DepthStencilState* _currentDepthStencilState;
    private ID3D11RasterizerState* _currentRasterizerState;
    private D3DPrimitiveTopology _currentPrimitiveTopology;

    private RenderPassDescriptor _currentRenderPass;
    private readonly ID3D11RenderTargetView*[] _rtvs = new ID3D11RenderTargetView*[D3D11_SIMULTANEOUS_RENDER_TARGET_COUNT];
    private ID3D11DepthStencilView* DSV = null;

    private readonly ID3D11Buffer*[] _vertexBindings = new ID3D11Buffer*[8];
    private uint[] _vertexOffsets = new uint[8];

    private readonly ID3D11Buffer*[] _constantBuffers = new ID3D11Buffer*[4];
    private readonly ID3D11SamplerState*[] _samplers = new ID3D11SamplerState*[16];
    private readonly ID3D11ShaderResourceView*[] _srvs = new ID3D11ShaderResourceView*[16];
    private readonly ID3D11UnorderedAccessView*[] _uavs = new ID3D11UnorderedAccessView*[16];
    private uint _numCBVBindings;
    private uint _numSamplerBindings;
    private uint _numSRVBindings;
    private uint _numUAVBindings;

    public D3D11CommandContext(D3D11GraphicsDevice device)
    {
        _device = device;
        _context = device.NativeContext;
        _context->QueryInterface(__uuidof<ID3DUserDefinedAnnotation>(), _annotation.GetVoidAddressOf());
    }

    protected override void Dispose(bool disposing)
    {
        base.Dispose(disposing);

        if (disposing)
        {
            _annotation.Dispose();
        }
    }

    public override void PushDebugGroup(string groupLabel)
    {
        fixed (char* groupLabelPtr = groupLabel)
        {
            _annotation.Get()->BeginEvent((ushort*)groupLabelPtr);
        }
    }

    public override void PopDebugGroup()
    {
        _annotation.Get()->EndEvent();
    }

    public override void InsertDebugMarker(string debugLabel)
    {
        fixed (char* debugLabelPtr = debugLabel)
        {
            _annotation.Get()->SetMarker((ushort*)debugLabelPtr);
        }
    }

    protected override void BeginRenderPassCore(in RenderPassDescriptor renderPass)
    {
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

                D3D11Texture texture = (D3D11Texture)attachment.Texture;
                int mipLevel = attachment.MipLevel;
                int slice = attachment.Slice;

                renderArea.Width = Math.Min(renderArea.Width, texture.GetWidth(mipLevel));
                renderArea.Height = Math.Min(renderArea.Height, texture.GetHeight(mipLevel));

                _rtvs[numRTVs] = texture.GetRTV(mipLevel, slice);

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

            D3D11Texture texture = (D3D11Texture)attachment.Texture;
            int mipLevel = attachment.MipLevel;
            int slice = attachment.Slice;

            renderArea.Width = Math.Min(renderArea.Width, texture.GetWidth(mipLevel));
            renderArea.Height = Math.Min(renderArea.Height, texture.GetHeight(mipLevel));

            DSV = texture.GetDSV(mipLevel, slice);

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

                D3D11Texture texture = (D3D11Texture)attachment.Texture;
                int mipLevel = attachment.MipLevel;
                int slice = attachment.Slice;
                uint srcSubResource = D3D11CalcSubresource((uint)mipLevel, (uint)slice, (uint)texture.MipLevels);

                switch (attachment.StoreAction)
                {
                    case StoreAction.Store:
                        if (attachment.ResolveTexture is not null)
                        {
                            D3D11Texture resolveTexture = (D3D11Texture)attachment.ResolveTexture!;
                            uint dstSubResource = D3D11CalcSubresource((uint)attachment.ResolveMipLevel, (uint)attachment.ResolveSlice, (uint)resolveTexture.MipLevels);
                            _context->ResolveSubresource(resolveTexture.Handle, dstSubResource, texture.Handle, srcSubResource, resolveTexture.DxgiFormat);
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

        if (!string.IsNullOrEmpty(_currentRenderPass.Label))
        {
            PopDebugGroup();
        }
    }

    public override void SetPipeline(Pipeline pipeline)
    {
        D3D11Pipeline d3d11Pipeline = (D3D11Pipeline)pipeline;
        if (_currentPipeline == d3d11Pipeline)
            return;

        _currentPipeline = d3d11Pipeline;

        if (d3d11Pipeline.PipelineType == PipelineType.Render)
        {
            _context->CSSetShader(null);

            _context->VSSetShader(d3d11Pipeline.VS);
            _context->PSSetShader(d3d11Pipeline.PS);
            _context->IASetInputLayout(d3d11Pipeline.InputLayout);

            if (_currentDepthStencilState != d3d11Pipeline.DepthStencilState)
            {
                _currentDepthStencilState = d3d11Pipeline.DepthStencilState;
                _context->OMSetDepthStencilState(_currentDepthStencilState, 0);
            }

            if (_currentRasterizerState != d3d11Pipeline.RasterizerState)
            {
                _currentRasterizerState = d3d11Pipeline.RasterizerState;
                _context->RSSetState(_currentRasterizerState);
            }

            if (_currentPrimitiveTopology != d3d11Pipeline.PrimitiveTopology)
            {
                _currentPrimitiveTopology = d3d11Pipeline.PrimitiveTopology;
                _context->IASetPrimitiveTopology(d3d11Pipeline.PrimitiveTopology);
            }
        }
        else if (d3d11Pipeline.PipelineType == PipelineType.Compute)
        {
            _context->CSSetShader(d3d11Pipeline.CS, null, 0);
        }
    }

    public override void SetVertexBuffer(uint slot, GraphicsBuffer buffer, uint offset = 0)
    {
        var d3dBuffer = ((D3D11Buffer)buffer);

        if (_vertexBindings[slot] != d3dBuffer.Handle || _vertexOffsets[slot] != offset)
        {
            _vertexBindings[slot] = d3dBuffer.Handle;
            _vertexOffsets[slot] = offset;
        }
    }

    public override void SetIndexBuffer(GraphicsBuffer buffer, uint offset, IndexType indexType)
    {
        var d3dBuffer = ((D3D11Buffer)buffer).Handle;
        _context->IASetIndexBuffer(d3dBuffer, indexType.ToDxgiFormat(), offset);
    }

    public override void SetConstantBuffer(int index, GraphicsBuffer buffer)
    {
        _constantBuffers[index] = ((D3D11Buffer)buffer).Handle;
        _numCBVBindings = Math.Max((uint)index + 1, _numCBVBindings);
    }

    public override void SetSampler(int index, Sampler sampler)
    {
        _samplers[index] = ((D3D11Sampler)sampler).Handle;
        _numSamplerBindings = Math.Max((uint)index + 1, _numSamplerBindings);
    }

    public override void SetSRV(int index, Texture texture)
    {
        _srvs[index] = ((D3D11Texture)texture).SRV;
        _numSRVBindings = Math.Max((uint)index + 1, _numSRVBindings);
    }

    public override void SetUAV(int index, Texture texture, int mipLevel)
    {
        _uavs[index] = ((D3D11Texture)texture).GetUAV(mipLevel);
        _numUAVBindings = Math.Max((uint)index + 1, _numUAVBindings);
    }

    public override unsafe void UpdateConstantBuffer(GraphicsBuffer source, void* data, uint size)
    {
        var d3dSource = (D3D11Buffer)source;
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
    }

    public override void CopyTexture(Texture source, Texture destination)
    {
        var d3dSource = (D3D11Texture)source;
        var d3dDest = (D3D11Texture)destination;

        _context->CopyResource(d3dDest.Handle, d3dSource.Handle);
    }

    public override void CopyTexture(Texture source, int sourceArraySlice, Texture destination, int destArraySlice)
    {
        var d3dSource = (D3D11Texture)source;
        var srcSubresource = D3D11CalcSubresource(0u, (uint)sourceArraySlice, (uint)source.MipLevels);
        var d3dDest = (D3D11Texture)destination;
        var dstSubresource = D3D11CalcSubresource(0u, (uint)destArraySlice, (uint)source.MipLevels);

        _context->CopySubresourceRegion(d3dDest.Handle, dstSubresource, 0, 0, 0, d3dSource.Handle, srcSubresource, null);

    }

    public override void GenerateMips(Texture texture)
    {
        _context->GenerateMips(((D3D11Texture)texture).SRV);
    }

    public override void Dispatch(int groupCountX, int groupCountY, int groupCountZ)
    {
        PrepareDispatch();
        _context->Dispatch((uint)groupCountX, (uint)groupCountY, (uint)groupCountZ);

        ID3D11Buffer* nullBuffer = default;
        ID3D11UnorderedAccessView* nullUAV = default;
        _context->CSSetConstantBuffers(0, 1, &nullBuffer);
        _context->CSSetUnorderedAccessViews(0, 1, &nullUAV, null);
    }

    public override void Draw(int vertexCount, int instanceCount = 1, int firstVertex = 0, int firstInstance = 0)
    {
        PrepareDraw();

        if (instanceCount > 1)
        {
            _context->DrawInstanced((uint)vertexCount, (uint)instanceCount, (uint)firstVertex, (uint)firstInstance);
        }
        else
        {
            _context->Draw((uint)vertexCount, (uint)firstVertex);
        }
    }

    public override void DrawIndexed(int indexCount, int instanceCount = 1, int firstIndex = 0, int baseVertex = 0, int firstInstance = 0)
    {
        PrepareDraw();

        if (instanceCount > 1)
        {
            _context->DrawIndexedInstanced((uint)indexCount, (uint)instanceCount, (uint)firstIndex, baseVertex, (uint)firstInstance);
        }
        else
        {
            _context->DrawIndexed((uint)indexCount, (uint)firstIndex, baseVertex);
        }
    }

    private void PrepareDispatch()
    {
        if (_numCBVBindings > 0)
        {
            fixed (ID3D11Buffer** cbvsPtr = _constantBuffers)
            {
                _context->CSSetConstantBuffers(0, _numCBVBindings, cbvsPtr);
            }
        }

        if (_numSRVBindings > 0)
        {
            fixed (ID3D11ShaderResourceView** srvPtr = _srvs)
            {
                _context->CSSetShaderResources(0, _numSRVBindings, srvPtr);
            }
        }

        if (_numUAVBindings > 0)
        {
            fixed (ID3D11UnorderedAccessView** uavPtr = _uavs)
            {
                _context->CSSetUnorderedAccessViews(0, _numUAVBindings, uavPtr, null);
            }
        }

        if (_numSamplerBindings > 0)
        {
            fixed (ID3D11SamplerState** samplerPtr = _samplers)
            {
                _context->CSSetSamplers(0, _numSamplerBindings, samplerPtr);
            }
        }

        _numCBVBindings = 0;
        _numSRVBindings = 0;
        _numUAVBindings = 0;
        _numSamplerBindings = 0;
    }

    private void PrepareDraw()
    {
        if (_currentPipeline!.NumVertexBindings > 0)
        {
            fixed (ID3D11Buffer** vbo = _vertexBindings)
            fixed (uint* offsets = _vertexOffsets)
            {
                _context->IASetVertexBuffers(0, _currentPipeline.NumVertexBindings, vbo, _currentPipeline.Strides, offsets);
            }
        }

        if (_numCBVBindings > 0)
        {
            fixed (ID3D11Buffer** cbvsPtr = _constantBuffers)
            {
                _context->VSSetConstantBuffers(0, _numCBVBindings, cbvsPtr);
                _context->PSSetConstantBuffers(0, _numCBVBindings, cbvsPtr);
            }
        }

        if (_numSRVBindings > 0)
        {
            fixed (ID3D11ShaderResourceView** srvPtr = _srvs)
            {
                _context->PSSetShaderResources(0, _numSRVBindings, srvPtr);
            }
        }

        //if (_numUAVBindings > 0)
        //{
        //    fixed (ID3D11UnorderedAccessView** uavPtr = _uavs)
        //    {
        //        _context->PSSetUnorderedAccessViews(0, _numUAVBindings, uavPtr, null);
        //    }
        //}

        if (_numSamplerBindings > 0)
        {
            fixed (ID3D11SamplerState** samplerPtr = _samplers)
            {
                _context->PSSetSamplers(0, _numSamplerBindings, samplerPtr);
            }
        }

        _numCBVBindings = 0;
        _numSRVBindings = 0;
        _numUAVBindings = 0;
        _numSamplerBindings = 0;
    }
}
