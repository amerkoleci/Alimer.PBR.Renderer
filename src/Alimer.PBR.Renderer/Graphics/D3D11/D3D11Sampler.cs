// Copyright © Amer Koleci and Contributors.
// Licensed under the MIT License (MIT). See LICENSE in the repository root for more information.

using Win32;
using Win32.Graphics.Direct3D11;
using D3D11SamplerDesc = Win32.Graphics.Direct3D11.SamplerDescription;
using static Alimer.Graphics.D3D11.D3D11Utils;

namespace Alimer.Graphics.D3D11;

internal sealed unsafe class D3D11Sampler : Sampler
{
    private readonly ComPtr<ID3D11SamplerState> _handle;

    public D3D11Sampler(D3D11GraphicsDevice device, in SamplerDescription description)
        : base(device, description)
    {
        FilterType minFilter = description.MinFilter.ToD3D11();
        FilterType magFilter = description.MagFilter.ToD3D11();
        FilterType mipmapFilter = description.MipFilter.ToD3D11();

        FilterReductionType reduction = description.Compare != CompareFunction.Never ? FilterReductionType.Comparison : FilterReductionType.Standard;

        D3D11SamplerDesc d3dDesc = new();

        // https://docs.microsoft.com/en-us/windows/win32/api/d3d12/ns-d3d12-d3d12_sampler_desc
        d3dDesc.MaxAnisotropy = Math.Min(Math.Max(description.MaxAnisotropy, 1u), 16u);
        if (d3dDesc.MaxAnisotropy > 1)
        {
            d3dDesc.Filter = D3D11_ENCODE_ANISOTROPIC_FILTER(reduction);
        }
        else
        {
            d3dDesc.Filter = D3D11_ENCODE_BASIC_FILTER(minFilter, magFilter, mipmapFilter, reduction);
        }

        d3dDesc.AddressU = description.AddressModeU.ToD3D11();
        d3dDesc.AddressV = description.AddressModeV.ToD3D11();
        d3dDesc.AddressW = description.AddressModeW.ToD3D11();
        d3dDesc.MipLODBias = 0.0f;
        if (description.Compare != CompareFunction.Never)
        {
            d3dDesc.ComparisonFunc = description.Compare.ToD3D11();
        }
        else
        {
            // Still set the function so it's not garbage.
            d3dDesc.ComparisonFunc = ComparisonFunction.Never;
        }
        d3dDesc.MinLOD = description.LodMinClamp;
        d3dDesc.MaxLOD = description.LodMaxClamp;

        HResult hr = device.NativeDevice->CreateSamplerState(&d3dDesc, _handle.GetAddressOf());
        if (hr.Failure)
        {
            throw new InvalidOperationException("D3D11: Failed to create sampler state");
        }
    }

    public ID3D11SamplerState* Handle => _handle.Get();

    protected override void Dispose(bool disposing)
    {
        base.Dispose(disposing);

        if (disposing)
        {
            _handle.Dispose();
        }
    }
}
