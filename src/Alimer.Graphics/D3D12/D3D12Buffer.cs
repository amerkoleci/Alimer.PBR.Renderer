// Copyright (c) Amer Koleci and Contributors.
// Licensed under the MIT License (MIT). See LICENSE in the repository root for more information.

using Vortice.Mathematics;
using Win32;
using Win32.Graphics.Direct3D12;
using static Win32.Apis;
using static Win32.Graphics.Direct3D12.Apis;

namespace Alimer.Graphics.D3D12;

internal sealed unsafe class D3D12Buffer : GraphicsBuffer, ID3D11GpuResource
{
    private readonly ComPtr<ID3D12Resource> _handle;
    private readonly bool _immutableState;
    private readonly void* _pMappedData;

    public D3D12Buffer(D3D12GraphicsDevice device, in BufferDescription description, void* initialData = default)
        : base(device, description)
    {
        ulong size = description.Size;
        if ((description.Usage & BufferUsage.Constant) != BufferUsage.None)
        {
            size = MathHelper.AlignUp(size, D3D12_CONSTANT_BUFFER_DATA_PLACEMENT_ALIGNMENT);
        }

        ResourceFlags resourceFlags = ResourceFlags.None;

        if ((description.Usage & BufferUsage.ShaderWrite) != BufferUsage.None)
        {
            resourceFlags |= ResourceFlags.AllowUnorderedAccess;
        }

        if (!((description.Usage & BufferUsage.ShaderRead) == BufferUsage.None)/* &&
            !((description.Usage & BufferUsage.RayTracing) == BufferUsage.None)*/)
        {
            resourceFlags |= ResourceFlags.DenyShaderResource;
        }


        HeapProperties heapProps = D3D12Utils.DefaultHeapProps;
        State = ResourceStates.Common;
        if (description.CpuAccess == CpuAccessMode.Read)
        {
            heapProps = D3D12Utils.ReadbackHeapProps;
            State = D3D12_RESOURCE_STATE_COPY_DEST;
            resourceFlags |= ResourceFlags.DenyShaderResource;

            _immutableState = true;
        }
        else if (description.CpuAccess == CpuAccessMode.Write)
        {
            heapProps = D3D12Utils.UploadHeapProps;
            State = D3D12_RESOURCE_STATE_GENERIC_READ;

            _immutableState = true;
        }
        else
        {
            _immutableState = false;
            //State = ConvertResourceStates(desc.initialState);
        }

        ResourceDescription resourceDesc = ResourceDescription.Buffer(size, resourceFlags);

        //SubresourceData* pInitialData = default;
        //SubresourceData subresourceData = default;
        //if (initialData != null)
        //{
        //    subresourceData.pSysMem = initialData;
        //    pInitialData = &subresourceData;
        //}

        HResult hr = device.NativeDevice->CreateCommittedResource(
           &heapProps,
           HeapFlags.None,
           &resourceDesc,
           State,
           null,
           __uuidof<ID3D12Resource>(),
           _handle.GetVoidAddressOf()
           );
        if (hr.Failure)
        {
            throw new InvalidOperationException("D3D12: Failed to create buffer");
        }

        if (!string.IsNullOrEmpty(description.Label))
        {
            _handle.Get()->SetName(description.Label);
        }

        ulong allocatedSize = default;
        device.NativeDevice->GetCopyableFootprints(&resourceDesc, 0, 1, 0, null, null, null, &allocatedSize);
        AllocatedSize = allocatedSize;
        GpuVirtualAddress = _handle.Get()->GetGPUVirtualAddress();

        if (description.CpuAccess == CpuAccessMode.Read)
        {
            void* pMappedData;
            ThrowIfFailed(_handle.Get()->Map(0, null, &pMappedData));
            _pMappedData = pMappedData;
        }
        else if (description.CpuAccess == CpuAccessMode.Write)
        {
            void* pMappedData;
            Win32.Graphics.Direct3D12.Range readRange = default;
            ThrowIfFailed(_handle.Get()->Map(0, &readRange, &pMappedData));
            _pMappedData = pMappedData;
        }
    }

    public ID3D12Resource* Handle => _handle;
    public ResourceStates State { get; set; }
    public ResourceStates TransitioningState { get; set; } = (ResourceStates)(-1);

    public ulong AllocatedSize { get; }
    public ulong GpuVirtualAddress { get; }

    protected override void Dispose(bool disposing)
    {
        if (disposing)
        {
            ((D3D12GraphicsDevice)Device).DeferDestroy((IUnknown*)_handle.Get());
            _handle.Dispose();
        }
    }

    protected override void OnLabelChanged(string newLabel)
    {
        _handle.Get()->SetName(newLabel);
    }
}
