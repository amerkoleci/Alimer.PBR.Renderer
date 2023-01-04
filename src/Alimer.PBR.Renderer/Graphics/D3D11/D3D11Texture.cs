// Copyright © Amer Koleci and Contributors.
// Licensed under the MIT License (MIT). See LICENSE in the repository root for more information.

using Win32;
using Win32.Graphics.Direct3D;
using Win32.Graphics.Direct3D11;
using Win32.Graphics.Dxgi.Common;
using static Win32.Apis;

namespace Alimer.Graphics.D3D11;

internal sealed unsafe class D3D11Texture : Texture
{
    private readonly ID3D11Resource* _handle;
    private readonly ComPtr<ID3D11ShaderResourceView> _srv = default;
    private readonly object _rtvLock = new();
    private readonly object _uavLock = new();
    private readonly Dictionary<int, ComPtr<ID3D11RenderTargetView>> _rtvs = new();
    private readonly Dictionary<int, ComPtr<ID3D11DepthStencilView>> _dsvs = new();
    private readonly Dictionary<int, ComPtr<ID3D11UnorderedAccessView>> _uavs = new();

    public D3D11Texture(D3D11GraphicsDevice device, in TextureDescription description, ID3D11Texture2D* existingHandle)
        : base(device, description)
    {
        _handle = (ID3D11Resource*)existingHandle;
        DxgiFormat = description.Format.ToDxgiFormat();
    }

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
            Width = (uint)Width,
            Height = (uint)Height,
            MipLevels = (uint)MipLevels,
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

        ID3D11Texture2D* tex2D = default;
        HResult hr = device.NativeDevice->CreateTexture2D(&d3dDesc, pInitialData, &tex2D);
        if (hr.Failure)
        {
            throw new InvalidOperationException("D3D11: Failed to create texture");
        }
        _handle = (ID3D11Resource*)tex2D;

        if (!string.IsNullOrEmpty(description.Label))
        {
            _handle->SetDebugName(description.Label);
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
            foreach (KeyValuePair<int, ComPtr<ID3D11RenderTargetView>> kvp in _rtvs)
            {
                kvp.Value.Dispose();
            }

            foreach (KeyValuePair<int, ComPtr<ID3D11DepthStencilView>> kvp in _dsvs)
            {
                kvp.Value.Dispose();
            }

            foreach (KeyValuePair<int, ComPtr<ID3D11UnorderedAccessView>> kvp in _uavs)
            {
                kvp.Value.Dispose();
            }

            _rtvs.Clear();
            _dsvs.Clear();
            _uavs.Clear();
            _srv.Dispose();
            _handle->Release();
        }
    }

    protected override void OnLabelChanged(string newLabel)
    {
        Handle->SetDebugName(newLabel);
    }

    public Format DxgiFormat { get; }
    public ID3D11Resource* Handle => _handle;
    public ID3D11ShaderResourceView* SRV => _srv;

    internal ID3D11RenderTargetView* GetRTV(int mipLevel, int slice)
    {
        lock (_rtvLock)
        {
            int hashCode = HashCode.Combine(mipLevel, slice);

            if (!_rtvs.TryGetValue(hashCode, out ComPtr<ID3D11RenderTargetView> rtv))
            {
                RenderTargetViewDescription viewDesc = new()
                {
                    Format = DxgiFormat
                };

                switch (Dimension)
                {
                    case TextureDimension.Texture2D:
                        if (SampleCount > TextureSampleCount.Count1)
                        {
                            if (ArrayLayers > 1)
                            {
                                viewDesc.ViewDimension = RtvDimension.Texture2DMsArray;
                                if (slice != -1)
                                {
                                    viewDesc.Texture2DMSArray.ArraySize = 1;
                                    viewDesc.Texture2DMSArray.FirstArraySlice = (uint)slice;
                                }
                                else
                                {
                                    viewDesc.Texture2DMSArray.ArraySize = (uint)ArrayLayers;
                                }
                            }
                            else
                            {
                                viewDesc.ViewDimension = RtvDimension.Texture2DMs;
                            }
                        }
                        else
                        {
                            if (ArrayLayers > 1)
                            {
                                viewDesc.ViewDimension = RtvDimension.Texture2DArray;
                                viewDesc.Texture2DArray.MipSlice = (uint)mipLevel;
                                if (slice != -1)
                                {
                                    viewDesc.Texture2DArray.ArraySize = 1;
                                    viewDesc.Texture2DArray.FirstArraySlice = (uint)slice;
                                }
                                else
                                {
                                    viewDesc.Texture2DArray.ArraySize = (uint)ArrayLayers;
                                }
                            }
                            else
                            {
                                viewDesc.ViewDimension = RtvDimension.Texture2D;
                                viewDesc.Texture2D.MipSlice = (uint)mipLevel;
                            }
                        }

                        break;
                }

                ThrowIfFailed(((D3D11GraphicsDevice)Device).NativeDevice->CreateRenderTargetView(Handle, &viewDesc, rtv.GetAddressOf()));
                _rtvs.Add(hashCode, rtv);
            }

            return rtv.Get();
        }
    }

    internal ID3D11DepthStencilView* GetDSV(int mipLevel, int slice)
    {
        lock (_rtvLock)
        {
            int hashCode = HashCode.Combine(mipLevel, slice);

            if (!_dsvs.TryGetValue(hashCode, out ComPtr<ID3D11DepthStencilView> dsv))
            {
                DepthStencilViewDescription viewDesc = new()
                {
                    Format = DxgiFormat
                };

                switch (Dimension)
                {
                    case TextureDimension.Texture2D:
                        if (SampleCount > TextureSampleCount.Count1)
                        {
                            if (ArrayLayers > 1)
                            {
                                viewDesc.ViewDimension = DsvDimension.Texture2DMsArray;
                                if (slice != -1)
                                {
                                    viewDesc.Texture2DMSArray.ArraySize = 1;
                                    viewDesc.Texture2DMSArray.FirstArraySlice = (uint)slice;
                                }
                                else
                                {
                                    viewDesc.Texture2DMSArray.ArraySize = (uint)ArrayLayers;
                                }
                            }
                            else
                            {
                                viewDesc.ViewDimension = DsvDimension.Texture2DMs;
                            }
                        }
                        else
                        {
                            if (ArrayLayers > 1)
                            {
                                viewDesc.ViewDimension = DsvDimension.Texture2DArray;
                                viewDesc.Texture2DArray.MipSlice = (uint)mipLevel;
                                if (slice != -1)
                                {
                                    viewDesc.Texture2DArray.ArraySize = 1;
                                    viewDesc.Texture2DArray.FirstArraySlice = (uint)slice;
                                }
                                else
                                {
                                    viewDesc.Texture2DArray.ArraySize = (uint)ArrayLayers;
                                }
                            }
                            else
                            {
                                viewDesc.ViewDimension = DsvDimension.Texture2D;
                                viewDesc.Texture2D.MipSlice = (uint)mipLevel;
                            }
                        }

                        break;
                }

                ThrowIfFailed(((D3D11GraphicsDevice)Device).NativeDevice->CreateDepthStencilView(Handle, &viewDesc, dsv.GetAddressOf()));
                _dsvs.Add(hashCode, dsv);
            }

            return dsv.Get();
        }
    }

    internal ID3D11UnorderedAccessView* GetUAV(int mipLevel)
    {
        lock (_uavLock)
        {
            if (!_uavs.TryGetValue(mipLevel, out ComPtr<ID3D11UnorderedAccessView> uav))
            {
                UnorderedAccessViewDescription uavDesc = new()
                {
                    Format = DxgiFormat
                };

                if (ArrayLayers == 1)
                {
                    uavDesc.ViewDimension = UavDimension.Texture2D;
                    uavDesc.Texture2D.MipSlice = (uint)mipLevel;
                }
                else
                {
                    uavDesc.ViewDimension = UavDimension.Texture2DArray;
                    uavDesc.Texture2DArray.MipSlice = (uint)mipLevel;
                    uavDesc.Texture2DArray.FirstArraySlice = 0;
                    uavDesc.Texture2DArray.ArraySize = (uint)ArrayLayers;
                }


                ThrowIfFailed(((D3D11GraphicsDevice)Device).NativeDevice->CreateUnorderedAccessView(Handle, &uavDesc, uav.GetAddressOf()));
                _uavs.Add(mipLevel, uav);
            }

            return uav.Get();
        }
    }
}
