// Copyright © Amer Koleci and Contributors.
// Licensed under the MIT License (MIT). See LICENSE in the repository root for more information.

using Win32.Graphics.Direct3D11;
using D3DPrimitiveTopology = Win32.Graphics.Direct3D.PrimitiveTopology;

namespace Alimer.Graphics.D3D11;

internal sealed unsafe class D3D11CommandContext : CommandContext
{
    private readonly D3D11GraphicsDevice _device;
    private readonly ID3D11DeviceContext1* _context;
    private ID3D11RasterizerState* _currentRasterizerState;
    private D3DPrimitiveTopology _currentPrimitiveTopology;
    private readonly GraphicsBuffer[] _constantBuffers = new GraphicsBuffer[4];
    private readonly ID3D11SamplerState*[] _samplers = new ID3D11SamplerState*[16];
    private readonly ID3D11ShaderResourceView*[] _srvs = new ID3D11ShaderResourceView*[16];
    private readonly ID3D11UnorderedAccessView*[] _uavs = new ID3D11UnorderedAccessView*[16];
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
            _context->CSSetShader(d3d11Pipeline.CS, null, 0);
            //_context->CSSetUnorderedAccessViews(0, _numUAVBindings, null, null);

            _context->VSSetShader(d3d11Pipeline.VS, null, 0);
            _context->PSSetShader(d3d11Pipeline.PS, null, 0);

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

    public override void SetRenderTarget(FrameBuffer? frameBuffer = null)
    {
        if (frameBuffer is null)
        {
            ID3D11RenderTargetView* rtv = _device.BackBufferRTV;
            _context->OMSetRenderTargets(1, &rtv, null);
        }
        else
        {
            ((D3D11FrameBuffer)frameBuffer).Bind(_context);
        }
    }

    public override void SetConstantBuffer(int index, GraphicsBuffer buffer)
    {
        _constantBuffers[index] = buffer;
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

    public override void SetUAV(int index, Texture texture)
    {
        _uavs[index] = ((D3D11Texture)texture).GetUAV(0);
        _numUAVBindings = Math.Max((uint)index + 1, _numUAVBindings);
    }

    private void PrepareDispatch()
    {
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
    }

    public override void Dispatch(int groupCountX, int groupCountY, int groupCountZ)
    {
        PrepareDispatch();
        _context->Dispatch((uint)groupCountX, (uint)groupCountY, (uint)groupCountZ);
    }

    public override void Draw(int vertexCount, int instanceCount = 1, int firstVertex = 0, int firstInstance = 0)
    {
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
        if (instanceCount > 1)
        {
            _context->DrawIndexedInstanced((uint)indexCount, (uint)instanceCount, (uint)firstIndex, baseVertex, (uint)firstInstance);
        }
        else
        {
            _context->DrawIndexed((uint)indexCount, (uint)firstIndex, baseVertex);
        }
    }
}
