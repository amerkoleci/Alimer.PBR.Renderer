﻿// Copyright © Amer Koleci and Contributors.
// Licensed under the MIT License (MIT). See LICENSE in the repository root for more information.

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
    private readonly object _uavLock = new object();
    private readonly Dictionary<uint, ComPtr<ID3D11UnorderedAccessView>> _uavs = new();

    public D3D11Texture(D3D11GraphicsDevice device, in TextureDescription description, void* initialData = default)
        : base(device, description)
    {
        BindFlags bindFlags = BindFlags.None;
        ResourceMiscFlags miscFlags = ResourceMiscFlags.None;
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

        int arrayMultiplier = 1;
        if (description.Dimension == TextureDimension.TextureCube)
        {
            arrayMultiplier = 6;
            miscFlags |= ResourceMiscFlags.TextureCube;
        }

        if (description.MipLevels == 0)
        {
            bindFlags |= BindFlags.RenderTarget;
            miscFlags |= ResourceMiscFlags.GenerateMips;
        }

        DxgiFormat = description.Format.ToDxgiFormat();

        Texture2DDescription d3dDesc = new()
        {
            Width = (uint)description.Width,
            Height = (uint)description.Height,
            MipLevels = (uint)description.MipLevels,
            ArraySize = (uint)(description.DepthOrArrayLayers * arrayMultiplier),
            Format = DxgiFormat,
            SampleDesc = new SampleDescription((uint)description.SampleCount, 0),
            BindFlags = bindFlags,
            MiscFlags = miscFlags
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
            if (description.Dimension == TextureDimension.TextureCube)
            {
                srvDesc.ViewDimension = SrvDimension.TextureCube;
                srvDesc.Texture2D.MostDetailedMip = 0;
                srvDesc.Texture2D.MipLevels = unchecked((uint)-1);
            }
            else
            {
                srvDesc.ViewDimension = SrvDimension.Texture2D;
                srvDesc.Texture2D.MostDetailedMip = 0;
                srvDesc.Texture2D.MipLevels = 1;
            }


            ThrowIfFailed(device.NativeDevice->CreateShaderResourceView(Handle, &srvDesc, _srv.GetAddressOf()));
        }
    }

    protected override void Dispose(bool disposing)
    {
        base.Dispose(disposing);

        if (disposing)
        {
            foreach (KeyValuePair<uint, ComPtr<ID3D11UnorderedAccessView>> kvp in _uavs)
            {
                kvp.Value.Dispose();
            }

            _uavs.Clear();
            _srv.Dispose();
            _handle.Dispose();
        }
    }

    public Format DxgiFormat { get; }
    public ID3D11Resource* Handle => (ID3D11Resource*)_handle.Get();
    public ID3D11ShaderResourceView* SRV => _srv;

    internal ID3D11UnorderedAccessView* GetUAV(uint mipSlice)
    {
        lock (_uavLock)
        {
            if (!_uavs.TryGetValue(mipSlice, out ComPtr<ID3D11UnorderedAccessView> uav))
            {
                UnorderedAccessViewDescription uavDesc = new();
                uavDesc.Format = DxgiFormat;
                if (ArrayLayers == 1)
                {
                    uavDesc.ViewDimension = UavDimension.Texture2D;
                    uavDesc.Texture2D.MipSlice = mipSlice;
                }
                else
                {
                    uavDesc.ViewDimension = UavDimension.Texture2DArray;
                    uavDesc.Texture2DArray.MipSlice = mipSlice;
                    uavDesc.Texture2DArray.FirstArraySlice = 0;
                    uavDesc.Texture2DArray.ArraySize = (uint)ArrayLayers;
                }


                ThrowIfFailed(((D3D11GraphicsDevice)Device).NativeDevice->CreateUnorderedAccessView(Handle, &uavDesc, uav.GetAddressOf()));
                _uavs.Add(mipSlice, uav);
            }

            return uav.Get();
        }
    }

}
