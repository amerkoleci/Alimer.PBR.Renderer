// Copyright © Amer Koleci and Contributors.
// Licensed under the MIT License (MIT). See LICENSE in the repository root for more information.

using Vortice.Mathematics;
using Win32.Graphics.Direct3D11;
using D3DPrimitiveTopology = Win32.Graphics.Direct3D.PrimitiveTopology;
using static Win32.Graphics.Direct3D11.Apis;
using System.Runtime.CompilerServices;

namespace Alimer.Graphics.D3D11;

internal sealed unsafe class D3D11CommandContext : CommandContext
{
    private readonly D3D11GraphicsDevice _device;
    private readonly ID3D11DeviceContext1* _context;
    private ID3D11DepthStencilState* _currentDepthStencilState;
    private ID3D11RasterizerState* _currentRasterizerState;
    private D3DPrimitiveTopology _currentPrimitiveTopology;
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
    }

    protected override void Dispose(bool disposing)
    {
        base.Dispose(disposing);

        if (disposing)
        {
        }
    }

    public override void SetPipeline(Pipeline pipeline)
    {
        D3D11Pipeline d3d11Pipeline = (D3D11Pipeline)pipeline;
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

    public override void SetRenderTarget(FrameBuffer? frameBuffer = null, Color4? clearColor = default, float clearDepth = 1.0f)
    {
        Viewport viewport = default;
        Win32.RawRect scissorRect = default;

        if (frameBuffer is null)
        {
            viewport = new((float)_device.Size.Width, (float)_device.Size.Height);
            scissorRect = new(0, 0, _device.Size.Width, _device.Size.Height);


            ID3D11RenderTargetView* rtv = _device.BackBufferRTV;
            _context->OMSetRenderTargets(1, &rtv, null);

            if (clearColor.HasValue)
            {
                Color4 clearColorValue = clearColor.Value;
                _context->ClearRenderTargetView(rtv, (float*)&clearColorValue);
            }
        }
        else
        {
            viewport = new((float)frameBuffer.Size.Width, (float)frameBuffer.Size.Height);
            scissorRect = new(0, 0, frameBuffer.Size.Width, frameBuffer.Size.Height);

            ((D3D11FrameBuffer)frameBuffer).Bind(_context);
        }


        _context->RSSetViewports(1, (Win32.Numerics.Viewport*)&viewport);
        _context->RSSetScissorRects(1, &scissorRect);
    }

    public override void SetVertexBuffer(uint slot, GraphicsBuffer buffer, uint stride, uint offset = 0)
    {
        var d3dBuffer = ((D3D11Buffer)buffer).Handle;
        //uint stride = 28;
        _context->IASetVertexBuffers(0, 1u, &d3dBuffer, &stride, &offset);
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

        ID3D11UnorderedAccessView* nullUAV = default;
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
        if (_numCBVBindings > 0)
        {
            fixed (ID3D11Buffer** cbvsPtr = _constantBuffers)
            {
                _context->VSSetConstantBuffers(0, _numCBVBindings, cbvsPtr);
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
