// Copyright © Amer Koleci and Contributors.
// Licensed under the MIT License (MIT). See LICENSE in the repository root for more information.

using Win32;
using Win32.Graphics.Direct3D12;
using Win32.Graphics.Dxgi.Common;
using static Win32.Apis;

namespace Alimer.Graphics.D3D12;

internal sealed unsafe class D3D12Texture : Texture
{
    private readonly ID3D12Resource* _handle;
    private readonly CpuDescriptorHandle _srv = default;
    private readonly object _rtvLock = new();
    private readonly object _uavLock = new();
    private readonly Dictionary<int, CpuDescriptorHandle> _rtvs = new();
    private readonly Dictionary<int, CpuDescriptorHandle> _dsvs = new();
    private readonly Dictionary<int, CpuDescriptorHandle> _uavs = new();

    public D3D12Texture(D3D12GraphicsDevice device, in TextureDescription description, ID3D12Resource* existingHandle)
        : base(device, description)
    {
        _handle = existingHandle;
        DxgiFormat = description.Format.ToDxgiFormat();
    }

    public D3D12Texture(D3D12GraphicsDevice device, in TextureDescription description, void* initialData = default)
        : base(device, description)
    {
        HeapProperties heapProps = D3D12Utils.DefaultHeapProps;
        ResourceFlags resourceFlags = ResourceFlags.None;
        int sampleCount = (int)description.SampleCount;


        if ((description.Usage & TextureUsage.ShaderWrite) != 0)
        {
            resourceFlags |= ResourceFlags.AllowUnorderedAccess;
        }

        if ((description.Usage & TextureUsage.RenderTarget) != 0)
        {
            if (description.Format.IsDepthStencilFormat())
            {
                resourceFlags |= ResourceFlags.AllowDepthStencil;

                if ((description.Usage & TextureUsage.ShaderRead) == 0)
                {
                    resourceFlags |= ResourceFlags.DenyShaderResource;
                }
            }
            else
            {
                resourceFlags |= ResourceFlags.AllowRenderTarget;
            }
        }

        int arrayMultiplier = 1;
        if (description.Dimension == TextureDimension.TextureCube)
        {
            arrayMultiplier = 6;
        }

        if (description.MipLevels == 0)
        {
            resourceFlags |= ResourceFlags.AllowRenderTarget;
        }

        DxgiFormat = description.Format.ToDxgiFormat();


        ResourceDescription desc = ResourceDescription.Tex2D(DxgiFormat,
            (ulong)description.Width,
            (uint)description.Height,
            (ushort)description.DepthOrArrayLayers,
            (ushort)description.MipLevels,
            (uint)sampleCount,
            0,
            resourceFlags
            );

        ID3D12Resource* handle = default;
        HResult hr = device.NativeDevice->CreateCommittedResource(
            &heapProps,
            HeapFlags.None,
            &desc,
            ResourceStates.Common,
            null,
            __uuidof<ID3D12Resource>(),
            (void**)&handle
            );

        if (hr.Failure)
        {
            throw new InvalidOperationException("D3D11: Failed to create texture");
        }
        _handle = handle;

        if (!string.IsNullOrEmpty(description.Label))
        {
            _handle->SetName(description.Label);
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


            //ThrowIfFailed(device.NativeDevice->CreateShaderResourceView(Handle, &srvDesc, _srv.GetAddressOf()));
        }
    }

    protected override void Dispose(bool disposing)
    {
        base.Dispose(disposing);

        if (disposing)
        {
            //foreach (KeyValuePair<int, ComPtr<ID3D11RenderTargetView>> kvp in _rtvs)
            //{
            //    kvp.Value.Dispose();
            //}

            //foreach (KeyValuePair<int, ComPtr<ID3D11DepthStencilView>> kvp in _dsvs)
            //{
            //    kvp.Value.Dispose();
            //}

            //foreach (KeyValuePair<int, ComPtr<ID3D11UnorderedAccessView>> kvp in _uavs)
            //{
            //    kvp.Value.Dispose();
            //}

            _rtvs.Clear();
            _dsvs.Clear();
            _uavs.Clear();
            //_srv.Dispose();
            _handle->Release();
        }
    }

    protected override void OnLabelChanged(string newLabel)
    {
        Handle->SetName(newLabel);
    }

    public Format DxgiFormat { get; }
    public ID3D12Resource* Handle => _handle;
    public CpuDescriptorHandle SRV => _srv;

    internal CpuDescriptorHandle GetRTV(int mipLevel, int slice)
    {
        lock (_rtvLock)
        {
            int hashCode = HashCode.Combine(mipLevel, slice);

            if (!_rtvs.TryGetValue(hashCode, out CpuDescriptorHandle rtv))
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

                //ThrowIfFailed(((D3D12GraphicsDevice)Device).NativeDevice->CreateRenderTargetView(Handle, &viewDesc, rtv.GetAddressOf()));
                _rtvs.Add(hashCode, rtv);
            }

            return rtv;
        }
    }

    internal CpuDescriptorHandle GetDSV(int mipLevel, int slice)
    {
        lock (_rtvLock)
        {
            int hashCode = HashCode.Combine(mipLevel, slice);

            if (!_dsvs.TryGetValue(hashCode, out CpuDescriptorHandle dsv))
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

                //ThrowIfFailed(((D3D12GraphicsDevice)Device).NativeDevice->CreateDepthStencilView(Handle, &viewDesc, dsv.GetAddressOf()));
                _dsvs.Add(hashCode, dsv);
            }

            return dsv;
        }
    }

    internal CpuDescriptorHandle GetUAV(int mipLevel)
    {
        lock (_uavLock)
        {
            if (!_uavs.TryGetValue(mipLevel, out CpuDescriptorHandle uav))
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


                //ThrowIfFailed(((D3D12GraphicsDevice)Device).NativeDevice->CreateUnorderedAccessView(Handle, &uavDesc, uav.GetAddressOf()));
                _uavs.Add(mipLevel, uav);
            }

            return uav;
        }
    }
}
