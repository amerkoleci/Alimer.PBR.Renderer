// Copyright (c) Amer Koleci and Contributors
// Licensed under the MIT License (MIT). See LICENSE in the repository root for more information.

using System.Drawing;
using Win32;
using Win32.Graphics.Direct3D11;
using Win32.Graphics.Dxgi;
using Win32.Graphics.Dxgi.Common;
using static Win32.Apis;
using static Win32.Graphics.Dxgi.Common.Apis;
using static Win32.Graphics.Dxgi.Apis;
using System.Text.Json;

namespace Alimer.Graphics.D3D11;

internal sealed unsafe class D3D11SwapChain : SwapChain
{
    private readonly ComPtr<IDXGISwapChain1> _handle;
    private D3D11Texture? _colorTexture;
    private Size _drawableSize;
    private readonly bool _isTearingSupported;
    private readonly uint _syncInterval;

    public D3D11SwapChain(D3D11GraphicsDevice device, SurfaceSource surface, in SwapChainDescription description)
        : base(device, surface, description)
    {
        _isTearingSupported = device.Factory.IsTearingSupported;
        _syncInterval = description.PresentMode.ToSyncInterval();

        SwapChainDescription1 swapChainDesc = new()
        {
            Width = 0u,
            Height = 0u,
            Format = description.Format.ToDxgiSwapChainFormat(),
            BufferCount = description.PresentMode.ToBufferCount(),
            BufferUsage = DXGI_USAGE_RENDER_TARGET_OUTPUT,
            SampleDesc = new SampleDescription(1, 0),
            Scaling = DXGI_SCALING_STRETCH,
            SwapEffect = DXGI_SWAP_EFFECT_FLIP_DISCARD,
            AlphaMode = DXGI_ALPHA_MODE_IGNORE,
            Flags = device.Factory.IsTearingSupported ? SwapChainFlags.AllowTearing : SwapChainFlags.None
        };

        SwapChainFullscreenDescription fsSwapChainDesc = new()
        {
            Windowed = !description.IsFullscreen
        };

        switch (surface)
        {
            case Win32SurfaceSource win32Surface:
                ThrowIfFailed(device.Factory.Handle->CreateSwapChainForHwnd(
                    (IUnknown*)device.NativeDevice,
                    win32Surface.Hwnd,
                    &swapChainDesc,
                    &fsSwapChainDesc,
                    null,
                    _handle.GetAddressOf())
                    );

                // This class does not support exclusive full-screen mode and prevents DXGI from responding to the ALT+ENTER shortcut
                ThrowIfFailed(device.Factory.Handle->MakeWindowAssociation(win32Surface.Hwnd, WindowAssociationFlags.NoAltEnter));
                break;

            case SwapChainPanelSurfaceSource swapChainPanelSurfaceSource:
            {
                ThrowIfFailed(device.Factory.Handle->CreateSwapChainForComposition(
                    (IUnknown*)device.NativeContext,
                    &swapChainDesc,
                    null,
                    _handle.GetAddressOf()
                    ));

                //fixed (ISwapChainPanelNative** swapChainPanelNative = _swapChainPanelNative)
                //{
                //    using ComPtr<IUnknown> swapChainPanel = default;
                //    //((IWinRTObject)owner).NativeObject.TryAs(guid, out *(nint*)swapChainPanelNative).Assert();
                //    swapChainPanel.Attach((IUnknown*)surface.Handle);
                //
                //    ThrowIfFailed(swapChainPanel.CopyTo(
                //        (Guid*)Unsafe.AsPointer(ref Unsafe.AsRef(in IID_ISwapChainPanelNativeWinUI)),
                //        (void**)swapChainPanelNative)
                //        );
                //}
                //
                //ThrowIfFailed(_swapChainPanelNative.Get()->SetSwapChain((IDXGISwapChain*)tempSwapChain.Get()));

                //Matrix3x2 transformMatrix = new()
                //{
                //    M11 = 1.0f / swapChainPanelSurface.Panel.CompositionScaleX,
                //    M22 = 1.0f / swapChainPanelSurface.Panel.CompositionScaleY
                //};
                //ThrowIfFailed(_handle.Get()->SetMatrixTransform(&transformMatrix));
            }
            break;
        }

        AfterReset();
    }

    protected override void Dispose(bool disposing)
    {
        if (disposing)
        {
            _colorTexture!.Dispose();
            _handle.Dispose();
        }
    }

    protected override void OnLabelChanged(string newLabel)
    {
        //_handle.Get()->SetDebugName(newLabel);
    }

    private void AfterReset()
    {
        if (_colorTexture is not null)
        {
            _colorTexture.Dispose();
        }

        SwapChainDescription1 swapChainDesc;
        ThrowIfFailed(_handle.Get()->GetDesc1(&swapChainDesc));

        _drawableSize = new((int)swapChainDesc.Width, (int)swapChainDesc.Height);

        TextureDescription colorTextureDesc = TextureDescription.Texture2D(
            ColorFormat,
            (int)swapChainDesc.Width,
            (int)swapChainDesc.Height,
            1,
            TextureUsage.RenderTarget);

        ID3D11Texture2D* d3dHandle = default;
        ThrowIfFailed(
            _handle.Get()->GetBuffer(0, __uuidof<ID3D11Texture2D>(), (void**)&d3dHandle)
            );
        _colorTexture = new D3D11Texture(Device, colorTextureDesc, d3dHandle);
    }
    public override Size DrawableSize => _drawableSize;

    protected override void ResizeBackBuffer()
    {
        AfterReset();
    }

    public override Texture? GetCurrentTexture() => _colorTexture;
    public override void Present()
    {
        HResult hr = HResult.Fail;
        if (IsFullscreen)
        {
            // Recommended to always use tearing if supported when using a sync interval of 0.
            hr = _handle.Get()->Present(0, DXGI_PRESENT_ALLOW_TEARING);
        }
        else
        {
            hr = _handle.Get()->Present(_syncInterval, 0);
        }

        // If the device was removed either by a disconnection or a driver upgrade, we
        // must recreate all device resources.
        if (hr == DXGI_ERROR_DEVICE_REMOVED || hr == DXGI_ERROR_DEVICE_RESET)
        {
#if DEBUG
            //char buff[64] = { };
            //sprintf_s(buff, "Device Lost on Present: Reason code 0x%08X\n",
            //    static_cast < unsigned int > ((hr == DXGI_ERROR_DEVICE_REMOVED) ? m_d3dDevice->GetDeviceRemovedReason() : hr));
            //OutputDebugStringA(buff);
#endif
            //HandleDeviceLost();
        }
        else
        {
            ThrowIfFailed(hr);

            //if (!m_dxgiFactory->IsCurrent())
            //{
            //    UpdateColorSpace();
            //}
        }
    }
}
