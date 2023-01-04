// Copyright © Amer Koleci and Contributors.
// Licensed under the MIT License (MIT). See LICENSE in the repository root for more information.

using Win32;
using Win32.Graphics.Direct3D11;
using static Win32.Apis;
using D3DPrimitiveTopology = Win32.Graphics.Direct3D.PrimitiveTopology;

namespace Alimer.Graphics.D3D12;

internal sealed unsafe class D3D12Pipeline : Pipeline
{
    private readonly ComPtr<ID3D11VertexShader> _vs = default;
    private readonly ComPtr<ID3D11PixelShader> _ps = default;
    private readonly ComPtr<ID3D11InputLayout> _inputLayout = default;
    private readonly uint _numVertexBindings = 0;
    private readonly uint[] _strides = new uint[8];

    private readonly ComPtr<ID3D11BlendState> _blendState = default;
    private readonly ComPtr<ID3D11RasterizerState> _rasterizerState = default;
    private readonly ComPtr<ID3D11DepthStencilState> _depthStencilState = default;

    private readonly ComPtr<ID3D11ComputeShader> _cs = default;

    public D3D12Pipeline(D3D12GraphicsDevice device, in ComputePipelineDescription description)
        : base(device, description)
    {
        //if (device.NativeDevice->CreateComputeShader(description.ComputeShader.Span, null, _cs.GetAddressOf()).Failure)
        //{
        //    throw new InvalidOperationException("Failed to create compute shader from compiled bytecode");
        //}
    }

    public D3D12Pipeline(D3D12GraphicsDevice device, in RenderPipelineDescription description)
        : base(device, description)
    {
        //if (device.NativeDevice->CreateVertexShader(description.VertexShader.Span, null, _vs.GetAddressOf()).Failure)
        //{
        //    throw new InvalidOperationException("Failed to create vertex shader from compiled bytecode");
        //}

        //if (device.NativeDevice->CreatePixelShader(description.FragmentShader.Span, null, _ps.GetAddressOf()).Failure)
        //{
        //    throw new InvalidOperationException("Failed to create pixel shader from compiled bytecode");
        //}

        //if (description.VertexDescriptor.Layouts != null &&
        //    description.VertexDescriptor.Layouts.Length > 0)
        //{
        //    ReadOnlySpan<byte> semanticName = "ATTRIBUTE"u8;
        //    int d3d11InputElementIndex = 0;
        //    Span<InputElementDescription> d3d11InputElementDescs = stackalloc InputElementDescription[16];

        //    for (uint slot = 0; slot < description.VertexDescriptor.Layouts.Length; slot++)
        //    {
        //        ref VertexLayoutDescriptor layout = ref description.VertexDescriptor.Layouts[slot];

        //        for (int i = 0; i < layout.Attributes.Length; i++)
        //        {
        //            ref VertexAttributeDescriptor attribute = ref layout.Attributes[i];

        //            d3d11InputElementDescs[d3d11InputElementIndex] = new InputElementDescription
        //            {
        //                SemanticName = (sbyte*)UnsafeUtilities.GetPointer(semanticName),
        //                SemanticIndex = (uint)i,
        //                Format = attribute.Format.ToDxgiFormat(),
        //                InputSlot = slot,
        //                AlignedByteOffset = attribute.Offset
        //            };
        //            if (layout.StepMode == VertexStepMode.Instance)
        //            {
        //                d3d11InputElementDescs[d3d11InputElementIndex].InputSlotClass = InputClassification.PerInstanceData;
        //                d3d11InputElementDescs[d3d11InputElementIndex].InstanceDataStepRate = 1u;
        //            }
        //            else
        //            {
        //                d3d11InputElementDescs[d3d11InputElementIndex].InputSlotClass = InputClassification.PerVertexData;
        //                d3d11InputElementDescs[d3d11InputElementIndex].InstanceDataStepRate = 0u;
        //            }

        //            d3d11InputElementIndex++;
        //        }

        //        _numVertexBindings = Math.Max(slot + 1, _numVertexBindings);
        //        _strides[slot] = layout.Stride;
        //    }

        //    fixed (InputElementDescription* pInputElements = d3d11InputElementDescs)
        //    fixed (byte* pShaderBytecode = description.VertexShader.Span)
        //    {
        //        ThrowIfFailed(device.NativeDevice->CreateInputLayout(
        //            pInputElements, (uint)d3d11InputElementIndex,
        //            pShaderBytecode, (nuint)description.VertexShader.Length,
        //            _inputLayout.GetAddressOf())
        //        );
        //    }
        //}

        //BlendDescription blendDesc = new()
        //{
        //    AlphaToCoverageEnable = description.BlendState.AlphaToCoverageEnabled,
        //    IndependentBlendEnable = true,
        //};
        //for (int i = 0; i < description.BlendState.RenderTargets.Length; i++)
        //{
        //    ref RenderTargetBlendState renderTarget = ref description.BlendState.RenderTargets[i];

        //    blendDesc.RenderTarget[i].BlendEnable = Utilities.BlendEnabled(renderTarget);
        //    blendDesc.RenderTarget[i].SrcBlend = renderTarget.SourceColorBlendFactor.ToD3D11();
        //    blendDesc.RenderTarget[i].DestBlend = renderTarget.DestinationColorBlendFactor.ToD3D11();
        //    blendDesc.RenderTarget[i].BlendOp = renderTarget.BlendOperation.ToD3D11();
        //    blendDesc.RenderTarget[i].SrcBlendAlpha = renderTarget.SourceAlphaBlendFactor.ToD3D11AlphaBlend();
        //    blendDesc.RenderTarget[i].DestBlendAlpha = renderTarget.DestinationAlphaBlendFactor.ToD3D11AlphaBlend();
        //    blendDesc.RenderTarget[i].BlendOpAlpha = renderTarget.AlphaBlendOperation.ToD3D11();
        //    blendDesc.RenderTarget[i].RenderTargetWriteMask = renderTarget.WriteMask.ToD3D11();
        //}
        //ThrowIfFailed(device.NativeDevice->CreateBlendState(&blendDesc, _blendState.GetAddressOf()));

        //RasterizerDescription rasterizerDesc = new()
        //{
        //    FillMode = description.RasterizerState.FillMode.ToD3D11(),
        //    CullMode = description.RasterizerState.CullMode.ToD3D11(),
        //    FrontCounterClockwise = (description.RasterizerState.FrontFace == FrontFaceWinding.CounterClockwise),
        //    DepthBias = 0,
        //    DepthBiasClamp = 0.0f,
        //    SlopeScaledDepthBias = 0.0f,
        //    DepthClipEnable = true,
        //    ScissorEnable = true,
        //    MultisampleEnable = true,
        //    AntialiasedLineEnable = false
        //};
        //ThrowIfFailed(device.NativeDevice->CreateRasterizerState(&rasterizerDesc, _rasterizerState.GetAddressOf()));

        //DepthStencilDescription depthStencilDesc = new()
        //{
        //    DepthEnable = description.DepthStencilState.DepthCompare != CompareFunction.Always || description.DepthStencilState.DepthWriteEnabled,
        //    DepthWriteMask = description.DepthStencilState.DepthWriteEnabled ? DepthWriteMask.All : DepthWriteMask.Zero,
        //    DepthFunc = description.DepthStencilState.DepthCompare.ToD3D11(),
        //    StencilEnable = Utilities.StencilTestEnabled(description.DepthStencilState),
        //    StencilReadMask = description.DepthStencilState.StencilReadMask,
        //    StencilWriteMask = description.DepthStencilState.StencilWriteMask,
        //    FrontFace = description.DepthStencilState.FrontFace.ToD3D11(),
        //    BackFace = description.DepthStencilState.BackFace.ToD3D11()
        //};
        //ThrowIfFailed(device.NativeDevice->CreateDepthStencilState(&depthStencilDesc, _depthStencilState.GetAddressOf()));

        PrimitiveTopology = description.PrimitiveTopology.ToD3DPrimitiveTopology();
    }

    public ID3D11VertexShader* VS => _vs;
    public ID3D11PixelShader* PS => _ps;
    public ID3D11InputLayout* InputLayout => _inputLayout;
    public uint NumVertexBindings => _numVertexBindings;
    public uint* Strides => UnsafeUtilities.GetPointer(_strides.AsSpan());

    public ID3D11BlendState* BlendState => _blendState;
    public ID3D11RasterizerState* RasterizerState => _rasterizerState;
    public ID3D11DepthStencilState* DepthStencilState => _depthStencilState;
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
            _inputLayout.Dispose();
            _blendState.Dispose();
            _rasterizerState.Dispose();
            _depthStencilState.Dispose();
        }
    }
}
