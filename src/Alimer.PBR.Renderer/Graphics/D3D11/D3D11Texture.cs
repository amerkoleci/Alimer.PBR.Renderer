// Copyright © Amer Koleci and Contributors.
// Licensed under the MIT License (MIT). See LICENSE in the repository root for more information.

using Vortice.Mathematics;
using Win32;
using Win32.Graphics.Direct3D;
using Win32.Graphics.Direct3D11;
using Win32.Graphics.Dxgi.Common;
using static Win32.Apis;

namespace Alimer.Graphics.D3D11;

internal sealed unsafe class D3D11Texture : Texture
{
    private readonly ComPtr<ID3D11Texture2D> _handle;
    private readonly ComPtr<ID3D11ShaderResourceView> _srv = default;

    public D3D11Texture(D3D11GraphicsDevice device, in TextureDescription description, void* initialData = default)
        : base(device, description)
    {
        BindFlags bindFlags = BindFlags.None;
        if ((description.Usage & TextureUsage.ShaderRead) != 0)
        {
            bindFlags |= BindFlags.ShaderResource;
        }
        if ((description.Usage & TextureUsage.ShaderWrite) != 0)
        {
            bindFlags |= BindFlags.UnorderedAccess;
        }
        if ((description.Usage & TextureUsage.RenderTarget) != 0)
        {
            if (description.Format.IsDepthStencilFormat())
            {
                bindFlags |= BindFlags.DepthStencil;
            }
            else
            {
                bindFlags |= BindFlags.RenderTarget;
            }
        }

        DxgiFormat = description.Format.ToDxgiFormat();

        Texture2DDescription d3dDesc = new()
        {
            Width = (uint)description.Width,
            Height = (uint)description.Height,
            MipLevels = (uint)description.MipLevels,
            ArraySize = (uint)description.DepthOrArrayLayers,
            Format = DxgiFormat,
            SampleDesc = new SampleDescription((uint)description.SampleCount, 0),
            BindFlags = bindFlags
        };

        SubresourceData* pInitialData = default;
        SubresourceData subresourceData = default;
        if (initialData != null)
        {
            subresourceData.pSysMem = initialData;
            subresourceData.SysMemPitch = (uint)(description.Width * description.Format.BytesPerPixels());
            pInitialData = &subresourceData;
        }

        HResult hr = device.NativeDevice->CreateTexture2D(&d3dDesc, pInitialData, _handle.GetAddressOf());
        if (hr.Failure)
        {
            throw new InvalidOperationException("D3D11: Failed to create texture");
        }

        if ((description.Usage & TextureUsage.ShaderRead) != 0)
        {
            ShaderResourceViewDescription srvDesc = new();
            srvDesc.Format = DxgiFormat;
            srvDesc.ViewDimension = SrvDimension.Texture2D;
            srvDesc.Texture2D.MostDetailedMip = 0;
            srvDesc.Texture2D.MipLevels = 1;

            ThrowIfFailed(device.NativeDevice->CreateShaderResourceView(Handle, &srvDesc, _srv.GetAddressOf()));
        }
    }

    public ID3D11Resource* Handle => (ID3D11Resource*)_handle.Get();
    public Format DxgiFormat { get; }

    protected override void Dispose(bool disposing)
    {
        base.Dispose(disposing);

        if (disposing)
        {
            _srv.Dispose();
            _handle.Dispose();
        }
    }
}
