// Copyright (c) Amer Koleci and Contributors
// Licensed under the MIT License (MIT). See LICENSE in the repository root for more information.

using Vortice.Mathematics;
using Win32;
using Win32.Graphics.Direct3D11;
using D3D11BufferDesc = Win32.Graphics.Direct3D11.BufferDescription;
using static Win32.Graphics.Direct3D11.Apis;

namespace Alimer.Graphics.D3D11;

internal sealed unsafe class D3D11Buffer : GraphicsBuffer
{
    private readonly ComPtr<ID3D11Buffer> _handle;

    public D3D11Buffer(D3D11GraphicsDevice device, in BufferDescription description, void* initialData = default)
        : base(device, description)
    {
        uint size = (uint)description.Size;
        BindFlags bindFlags = BindFlags.None;
        Usage d3dUsage = D3D11_USAGE_DEFAULT;
        CpuAccessFlags cpuAccessFlags = 0;
        ResourceMiscFlags miscFlags = ResourceMiscFlags.None;

        if ((description.Usage & BufferUsage.Constant) != BufferUsage.None)
        {
            size = MathHelper.AlignUp(size, device.Limits.MinConstantBufferOffsetAlignment);
            bindFlags = D3D11_BIND_CONSTANT_BUFFER;
            d3dUsage = D3D11_USAGE_DYNAMIC;
            cpuAccessFlags = D3D11_CPU_ACCESS_WRITE;
            IsDynamic = true;
        }
        else
        {
            switch (description.CpuAccess)
            {
                case CpuAccessMode.None:
                    d3dUsage = D3D11_USAGE_DEFAULT;
                    cpuAccessFlags = 0;
                    break;

                case CpuAccessMode.Read:
                    d3dUsage = D3D11_USAGE_STAGING;
                    cpuAccessFlags = D3D11_CPU_ACCESS_READ;
                    break;

                case CpuAccessMode.Write:
                    d3dUsage = D3D11_USAGE_DYNAMIC;
                    cpuAccessFlags =  D3D11_CPU_ACCESS_WRITE;
                    break;
            }

            if ((description.Usage & BufferUsage.Vertex) != BufferUsage.None)
            {
                bindFlags |= BindFlags.VertexBuffer;
            }

            if ((description.Usage & BufferUsage.Index) != BufferUsage.None)
            {
                bindFlags |= BindFlags.IndexBuffer;
            }

            if ((description.Usage & BufferUsage.ShaderRead) != BufferUsage.None
                && d3dUsage != D3D11_USAGE_STAGING)
            {
                bindFlags |= BindFlags.ShaderResource;
            }

            if ((description.Usage & BufferUsage.ShaderWrite) != BufferUsage.None)
            {
                bindFlags |= D3D11_BIND_UNORDERED_ACCESS;
            }

            if ((description.Usage & BufferUsage.Indirect) != BufferUsage.None)
            {
                miscFlags |= D3D11_RESOURCE_MISC_DRAWINDIRECT_ARGS;
            }
        }

        D3D11BufferDesc d3dDesc = new(size, bindFlags, d3dUsage, cpuAccessFlags, miscFlags);

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

        if (!string.IsNullOrEmpty(description.Label))
        {
            _handle.Get()->SetDebugName(description.Label);
        }
    }

    public ID3D11Buffer* Handle => _handle.Get();
    public bool IsDynamic { get; }

    protected override void Dispose(bool disposing)
    {
        if (disposing)
        {
            _handle.Dispose();
        }
    }

    protected override void OnLabelChanged(string newLabel)
    {
        Handle->SetDebugName(newLabel);
    }
}
