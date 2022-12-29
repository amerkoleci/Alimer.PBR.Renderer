// Copyright © Amer Koleci and Contributors.
// Licensed under the MIT License (MIT). See LICENSE in the repository root for more information.

using Vortice.Mathematics;
using Win32;
using Win32.Graphics.Direct3D;
using Win32.Graphics.Direct3D11;
using Win32.Graphics.Dxgi.Common;
using D3DPrimitiveTopology = Win32.Graphics.Direct3D.PrimitiveTopology;
using static Win32.Apis;

namespace Alimer.Graphics.D3D11;

internal sealed unsafe class D3D11Pipeline : Pipeline
{
    private readonly ComPtr<ID3D11ComputeShader> _cs = default;
    private readonly ComPtr<ID3D11DepthStencilState> _depthStencilState = default;
    private readonly ComPtr<ID3D11RasterizerState> _rasterizerState = default;

    public D3D11Pipeline(D3D11GraphicsDevice device, in ComputePipelineDescription description)
        : base(device, description)
    {
        fixed (byte* pShaderBytecode = description.ComputeShader.Span)
        {
            if (device.NativeDevice->CreateComputeShader(
                pShaderBytecode,
                (nuint)description.ComputeShader.Length,
                null, _cs.GetAddressOf()).Failure)
            {
                throw new InvalidOperationException("Failed to create compute shader from compiled bytecode");
            }
        }
    }

    public D3D11Pipeline(D3D11GraphicsDevice device, in RenderPipelineDescription description)
        : base(device, description)
    {
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

    public ID3D11ComputeShader* CS => _cs;
    public ID3D11RasterizerState* RasterizerState => _rasterizerState;
    public D3DPrimitiveTopology PrimitiveTopology { get; }

    protected override void Dispose(bool disposing)
    {
        base.Dispose(disposing);

        if (disposing)
        {
            _cs.Dispose();
            _depthStencilState.Dispose();
            _rasterizerState.Dispose();
        }
    }
}
