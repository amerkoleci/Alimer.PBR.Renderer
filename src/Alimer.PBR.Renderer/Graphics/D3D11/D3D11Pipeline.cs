// Copyright © Amer Koleci and Contributors.
// Licensed under the MIT License (MIT). See LICENSE in the repository root for more information.

using Win32;
using Win32.Graphics.Direct3D11;
using static Win32.Apis;
using D3DPrimitiveTopology = Win32.Graphics.Direct3D.PrimitiveTopology;

namespace Alimer.Graphics.D3D11;

internal sealed unsafe class D3D11Pipeline : Pipeline
{
    private readonly ComPtr<ID3D11VertexShader> _vs = default;
    private readonly ComPtr<ID3D11PixelShader> _ps = default;

    private readonly ComPtr<ID3D11ComputeShader> _cs = default;
    private readonly ComPtr<ID3D11DepthStencilState> _depthStencilState = default;
    private readonly ComPtr<ID3D11RasterizerState> _rasterizerState = default;

    public D3D11Pipeline(D3D11GraphicsDevice device, in ComputePipelineDescription description)
        : base(device, description)
    {
        if (device.NativeDevice->CreateComputeShader(description.ComputeShader.Span, null, _cs.GetAddressOf()).Failure)
        {
            throw new InvalidOperationException("Failed to create compute shader from compiled bytecode");
        }
    }

    public D3D11Pipeline(D3D11GraphicsDevice device, in RenderPipelineDescription description)
        : base(device, description)
    {
        if (device.NativeDevice->CreateVertexShader(description.VertexShader.Span, null, _vs.GetAddressOf()).Failure)
        {
            throw new InvalidOperationException("Failed to create vertex shader from compiled bytecode");
        }

        if (device.NativeDevice->CreatePixelShader(description.FragmentShader.Span, null, _ps.GetAddressOf()).Failure)
        {
            throw new InvalidOperationException("Failed to create pixel shader from compiled bytecode");
        }

        //ThrowIfFailed(device.NativeDevice->CreateInputLayout(&depthStencilDesc, _depthStencilState.GetAddressOf()));

        DepthStencilDescription depthStencilDesc = new();
        depthStencilDesc.DepthEnable = (description.DepthStencilState.DepthCompare == CompareFunction.Always && !description.DepthStencilState.DepthWriteEnabled) ? false : true;
        depthStencilDesc.DepthWriteMask = description.DepthStencilState.DepthWriteEnabled ? DepthWriteMask.All : DepthWriteMask.Zero;
        depthStencilDesc.DepthFunc = description.DepthStencilState.DepthCompare.ToD3D11();
        ThrowIfFailed(device.NativeDevice->CreateDepthStencilState(&depthStencilDesc, _depthStencilState.GetAddressOf()));

        RasterizerDescription rasterizerDesc = new();
        rasterizerDesc.FillMode = FillMode.Solid;
        rasterizerDesc.CullMode = CullMode.Back;
        rasterizerDesc.FrontCounterClockwise = true;
        rasterizerDesc.DepthClipEnable = true;
        ThrowIfFailed(device.NativeDevice->CreateRasterizerState(&rasterizerDesc, _rasterizerState.GetAddressOf()));

        PrimitiveTopology = description.PrimitiveTopology.ToD3D11();
    }

    public ID3D11VertexShader* VS => _vs;
    public ID3D11PixelShader* PS => _ps;

    public ID3D11RasterizerState* RasterizerState => _rasterizerState;
    public D3DPrimitiveTopology PrimitiveTopology { get; }
    public ID3D11ComputeShader* CS => _cs;

    protected override void Dispose(bool disposing)
    {
        base.Dispose(disposing);

        if (disposing)
        {
            _cs.Dispose();

            _vs.Dispose();
            _ps.Dispose();
            _depthStencilState.Dispose();
            _rasterizerState.Dispose();
        }
    }
}
