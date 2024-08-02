// Copyright (c) Amer Koleci and Contributors
// Licensed under the MIT License (MIT). See LICENSE in the repository root for more information.

using System.Runtime.CompilerServices;
using System.Runtime.InteropServices;
using Win32;
using Win32.Graphics.Direct3D;

namespace Alimer.Graphics;

internal unsafe struct D3DIncludeHandler : D3DIncludeHandler.Interface
{
    public static string? IncludeDirectory { get; set; }

    /// <summary>
    /// The shared method table pointer for all <see cref="D3DIncludeHandler"/> instances.
    /// </summary>
    private static readonly void** Vtbl = InitVtbl();

    /// <summary>
    /// Builds the custom method table pointer for <see cref="D3DIncludeHandler"/>.
    /// </summary>
    /// <returns>The method table pointer for <see cref="D3DIncludeHandler"/>.</returns>
    private static void** InitVtbl()
    {
        void** lpVtbl = (void**)RuntimeHelpers.AllocateTypeAssociatedMemory(typeof(D3DIncludeHandler), sizeof(void*) * 2);

        lpVtbl[0] = (delegate* unmanaged<D3DIncludeHandler*, IncludeType, byte*, void*, void**, uint*, int>)&Open;
        lpVtbl[1] = (delegate* unmanaged<D3DIncludeHandler*, void*, int>)&Close;

        return lpVtbl;
    }

    /// <summary>
    /// The method table pointer for the current instance.
    /// </summary>
    private void** lpVtbl;

    /// <inheritdoc cref="ID3DInclude.Open"/>
	[MethodImpl(MethodImplOptions.AggressiveInlining)]
    //[VtblIndex(0)]
    public HResult Open(IncludeType IncludeType, byte* pFileName, void* pParentData, void** ppData, uint* pBytes)
    {
        return ((delegate* unmanaged<D3DIncludeHandler*, IncludeType, byte*, void*, void**, uint*, int>)(lpVtbl[0]))((D3DIncludeHandler*)Unsafe.AsPointer(ref this), IncludeType, pFileName, pParentData, ppData, pBytes);
    }

    /// <inheritdoc cref="ID3DInclude.Close"/>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    //[VtblIndex(1)]
    public HResult Close(void* pData)
    {
        return ((delegate* unmanaged[Stdcall]<D3DIncludeHandler*, void*, int>)(lpVtbl[1]))((D3DIncludeHandler*)Unsafe.AsPointer(ref this), pData);
    }


    /// <summary>
    /// Creates and initializes a new <see cref="D3DIncludeHandler"/> instance.
    /// </summary>
    /// <returns>A pointer to a new <see cref="ID3DIncludeForD2DHelpers"/> instance.</returns>
    public static D3DIncludeHandler* Create()
    {
        D3DIncludeHandler* @this = (D3DIncludeHandler*)NativeMemory.Alloc((nuint)sizeof(D3DIncludeHandler));
        @this->lpVtbl = Vtbl;

        return @this;
    }

    /// <inheritdoc cref="ID3DInclude.Open"/>
    [UnmanagedCallersOnly]
    public static int Open(D3DIncludeHandler* @this, IncludeType IncludeType, byte* pFileName, void* pParentData, void** ppData, uint* pBytes)
    {
        string fileName = StringUtilities.GetString(pFileName)!;

        string fullPath = Path.Combine(AppContext.BaseDirectory, fileName);
        if (!string.IsNullOrEmpty(IncludeDirectory))
        {
            fullPath = Path.Combine(IncludeDirectory, fileName);
        }

        if (File.Exists(fullPath))
        {
            Span<byte> fileData = File.ReadAllBytes(fullPath);
            *ppData = (byte*)Unsafe.AsPointer(ref MemoryMarshal.GetReference(fileData));
            *pBytes = (uint)fileData.Length;

            return HResult.Ok;
        }

        return HResult.False;
    }

    /// <inheritdoc cref="ID3DInclude.Close"/>
    [UnmanagedCallersOnly]
    public static int Close(D3DIncludeHandler* @this, void* pData)
    {
        return HResult.Ok;
    }

    public interface Interface : ID3DInclude.Interface
    {
    }
}
