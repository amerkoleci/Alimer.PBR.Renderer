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

    public D3D11Texture(D3D11GraphicsDevice device, in Size3 size, TextureFormat format, TextureUsage usage, int sampleCount = 1)
        : base(device)
    {
        BindFlags bindFlags = BindFlags.None;
        if ((usage & TextureUsage.ShaderRead) != 0)
        {
            bindFlags |= BindFlags.ShaderResource;
        }
        if ((usage & TextureUsage.ShaderWrite) != 0)
        {
            bindFlags |= BindFlags.UnorderedAccess;
        }
        if ((usage & TextureUsage.RenderTarget) != 0)
        {
            if (format.IsDepthStencilFormat())
            {
                bindFlags |= BindFlags.DepthStencil;
            }
            else
            {
                bindFlags |= BindFlags.RenderTarget;
            }
        }

        DxgiFormat = format.ToDxgiFormat();

        Texture2DDescription d3dDesc = new()
        {
            Width = (uint)size.Width,
            Height = (uint)size.Height,
            MipLevels = 1,
            ArraySize = 1,
            Format = DxgiFormat,
            SampleDesc = new SampleDescription((uint)sampleCount, 0),
            BindFlags = bindFlags
        };

        HResult hr = device.NativeDevice->CreateTexture2D(&d3dDesc, null, _handle.GetAddressOf());
        if (hr.Failure)
        {
            throw new InvalidOperationException("D3D11: Failed to create texture");
        }

        if ((usage & TextureUsage.ShaderRead) != 0)
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
