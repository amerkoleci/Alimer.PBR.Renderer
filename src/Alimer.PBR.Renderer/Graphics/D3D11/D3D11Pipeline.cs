// Copyright © Amer Koleci and Contributors.
// Licensed under the MIT License (MIT). See LICENSE in the repository root for more information.

using Vortice.Mathematics;
using Win32;
using Win32.Graphics.Direct3D;
using Win32.Graphics.Direct3D11;
using Win32.Graphics.Dxgi.Common;
using static Win32.Apis;

namespace Alimer.Graphics.D3D11;

internal sealed unsafe class D3D11Pipeline : Pipeline
{
    private readonly ComPtr<ID3D11ComputeShader> _cs = default;
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
        RasterizerDescription rasterizerDesc = new();
        rasterizerDesc.FillMode = FillMode.Solid;
        rasterizerDesc.CullMode = CullMode.Back;
        rasterizerDesc.FrontCounterClockwise = true;
        rasterizerDesc.DepthClipEnable = true;
        ThrowIfFailed(device.NativeDevice->CreateRasterizerState(&rasterizerDesc, _rasterizerState.GetAddressOf()));

        PrimitiveTopology = PrimitiveTopology.TriangleList;
    }

    public ID3D11ComputeShader* CS => _cs;
    public ID3D11RasterizerState* RasterizerState => _rasterizerState;
    public PrimitiveTopology PrimitiveTopology { get; }

    protected override void Dispose(bool disposing)
    {
        base.Dispose(disposing);

        if (disposing)
        {
            _cs.Dispose();
            _rasterizerState.Dispose();
        }
    }
}
