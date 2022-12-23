// Copyright © Amer Koleci and Contributors.
// Licensed under the MIT License (MIT). See LICENSE in the repository root for more information.

using Win32.Graphics.Direct3D;
using Win32.Graphics.Direct3D11;

namespace Alimer.Graphics.D3D11;

internal sealed unsafe class D3D11CommandContext : CommandContext
{
    private readonly D3D11GraphicsDevice _device;
    private readonly ID3D11DeviceContext1* _context;
    private ID3D11RasterizerState* _currentRasterizerState;
    private PrimitiveTopology _currentPrimitiveTopology;

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
