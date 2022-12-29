// Copyright © Amer Koleci and Contributors.
// Licensed under the MIT License (MIT). See LICENSE in the repository root for more information.

using Vortice.Mathematics;
using Win32;
using Win32.Graphics.Direct3D11;
using D3D11BufferDesc = Win32.Graphics.Direct3D11.BufferDescription;

namespace Alimer.Graphics.D3D11;

internal sealed unsafe class D3D11Buffer : GraphicsBuffer
{
    private readonly ComPtr<ID3D11Buffer> _handle;

    public D3D11Buffer(D3D11GraphicsDevice device, in BufferDescription description, void* initialData = default)
        : base(device, description)
    {
        uint size = (uint)description.Size;
        BindFlags bindFlags = BindFlags.None;
        Usage usage = Usage.Default;
        CpuAccessFlags cpuAccessFlags = CpuAccessFlags.None;

        if ((description.Usage & BufferUsage.Constant) != BufferUsage.None)
        {
            size = MathHelper.AlignUp(size, 64u);
            bindFlags = BindFlags.ConstantBuffer;
            usage = Usage.Dynamic;
            cpuAccessFlags = CpuAccessFlags.Write;
        }
        else
        {
            if ((description.Usage & BufferUsage.Vertex) != BufferUsage.None)
            {
                bindFlags |= BindFlags.VertexBuffer;
            }

            if ((description.Usage & BufferUsage.Index) != BufferUsage.None)
            {
                bindFlags |= BindFlags.IndexBuffer;
            }

        }

        D3D11BufferDesc d3dDesc = new(size, bindFlags, usage, cpuAccessFlags);

        SubresourceData* pInitialData = default;
        SubresourceData subresourceData = default;
        if (initialData != null)
        {
            subresourceData.pSysMem = initialData;
            pInitialData = &subresourceData;
        }

        HResult hr = device.NativeDevice->CreateBuffer(&d3dDesc, pInitialData, _handle.GetAddressOf());
        if (hr.Failure)
        {
            throw new InvalidOperationException("D3D11: Failed to create buffer");
        }
    }

    public ID3D11Buffer* Handle => _handle.Get();

    protected override void Dispose(bool disposing)
    {
        base.Dispose(disposing);

        if (disposing)
        {
            _handle.Dispose();
        }
    }
}
