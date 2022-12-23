// Copyright © Amer Koleci and Contributors.
// Licensed under the MIT License (MIT). See LICENSE in the repository root for more information.

using System.Drawing;
using Vortice.Mathematics;
using Win32;
using Win32.Graphics.Direct3D11;
using static Win32.Apis;

namespace Alimer.Graphics.D3D11;

internal sealed unsafe class D3D11FrameBuffer : FrameBuffer
{
    private readonly D3D11Texture? _colorTexture;
    private readonly D3D11Texture? _depthStencilTexture;
    private readonly ComPtr<ID3D11RenderTargetView> _rtv = default;
    private readonly ComPtr<ID3D11DepthStencilView> _dsv = default;

    public D3D11FrameBuffer(D3D11GraphicsDevice device, Size size, int sampleCount, TextureFormat colorFormat, TextureFormat depthStencilFormat)
        : base(device)
    {
        if (colorFormat != TextureFormat.Invalid)
        {
            TextureUsage usage = TextureUsage.RenderTarget;
            if (sampleCount <= 1)
            {
                usage |= TextureUsage.ShaderRead;
            }
            _colorTexture = new D3D11Texture(device, new Size3(size.Width, size.Height, 1), colorFormat, usage, sampleCount);

            RenderTargetViewDescription rtvDesc = new(sampleCount > 1 ? RtvDimension.Texture2DMs : RtvDimension.Texture2D, _colorTexture.DxgiFormat);
            ThrowIfFailed(device.NativeDevice->CreateRenderTargetView(_colorTexture.Handle, &rtvDesc, _rtv.GetAddressOf()));
        }

        if (depthStencilFormat != TextureFormat.Invalid)
        {
            _depthStencilTexture = new D3D11Texture(device, new Size3(size.Width, size.Height, 1), depthStencilFormat, TextureUsage.RenderTarget, sampleCount);

            DepthStencilViewDescription dsvDesc = new(sampleCount > 1 ? DsvDimension.Texture2DMs : DsvDimension.Texture2D, _depthStencilTexture.DxgiFormat);
            ThrowIfFailed(device.NativeDevice->CreateDepthStencilView(_depthStencilTexture.Handle, &dsvDesc, _dsv.GetAddressOf()));
        }
    }

    protected override void Dispose(bool disposing)
    {
        base.Dispose(disposing);

        if (disposing)
        {
            _dsv.Dispose();
            _rtv.Dispose();
            _colorTexture?.Dispose();
            _depthStencilTexture?.Dispose();
        }
    }
}
