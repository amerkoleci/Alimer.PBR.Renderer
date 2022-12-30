// Copyright © Amer Koleci and Contributors.
// Licensed under the MIT License (MIT). See LICENSE in the repository root for more information.

using System.Diagnostics;
using System.Numerics;
using System.Runtime.CompilerServices;
using System.Runtime.InteropServices;
using CommunityToolkit.Diagnostics;

namespace Alimer.Graphics;

internal static unsafe class Utilities
{
    private static readonly VertexFormatInfo[] s_vertexFormatInfos = new VertexFormatInfo[]
    {
        new(VertexFormat.Undefined, 0, 0, 0,    VertexFormatBaseType.Float),
        new(VertexFormat.Uint8x2,     2, 2, 1,  VertexFormatBaseType.Uint),
        new(VertexFormat.Uint8x4,     4, 4, 1,  VertexFormatBaseType.Uint),
        new(VertexFormat.Sint8x2,     2, 2, 1,  VertexFormatBaseType.Sint),
        new(VertexFormat.Sint8x4,     4, 4, 1,  VertexFormatBaseType.Sint),
        new(VertexFormat.Unorm8x2,    2, 2, 1,  VertexFormatBaseType.Float),
        new(VertexFormat.Unorm8x4,    4, 4, 1,  VertexFormatBaseType.Float),
        new(VertexFormat.Snorm8x2,    2, 2, 1,  VertexFormatBaseType.Float),
        new(VertexFormat.Snorm8x4,    4, 4, 1,  VertexFormatBaseType.Float),
        new(VertexFormat.Uint16x2,    4, 2, 2,  VertexFormatBaseType.Uint),
        new (VertexFormat.Uint16x4,    8, 4, 2,  VertexFormatBaseType.Uint),
        new (VertexFormat.Sint16x2,    4, 2, 2,  VertexFormatBaseType.Sint),
        new (VertexFormat.Sint16x4,    8, 4, 2,  VertexFormatBaseType.Sint),
        new (VertexFormat.Unorm16x2,   4, 2, 2, VertexFormatBaseType.Float),
        new (VertexFormat.Unorm16x4,   8, 4, 2,  VertexFormatBaseType.Float),
        new (VertexFormat.Snorm16x2,   4, 2, 2,  VertexFormatBaseType.Float),
        new (VertexFormat.Snorm16x4,   8, 4, 2,  VertexFormatBaseType.Float),
        new (VertexFormat.Float16x2,   4, 2, 2,  VertexFormatBaseType.Float),
        new (VertexFormat.Float16x4,   8, 4, 2,  VertexFormatBaseType.Float),
        new (VertexFormat.Float32,     4, 1, 4,  VertexFormatBaseType.Float),
        new (VertexFormat.Float32x2,   8, 2, 4,  VertexFormatBaseType.Float),
        new (VertexFormat.Float32x3,   12, 3, 4, VertexFormatBaseType.Float),
        new (VertexFormat.Float32x4,   16, 4, 4, VertexFormatBaseType.Float),
        new (VertexFormat.Uint32,      4, 1, 4, VertexFormatBaseType.Uint),
        new (VertexFormat.Uint32x2,    8, 2, 4, VertexFormatBaseType.Uint),
        new (VertexFormat.Uint32x3,    12, 3, 4, VertexFormatBaseType.Uint),
        new (VertexFormat.Uint32x4,    16, 4, 4, VertexFormatBaseType.Uint),
        new (VertexFormat.Sint32,      4, 1, 4, VertexFormatBaseType.Sint),
        new (VertexFormat.Sint32x2,    8, 2, 4, VertexFormatBaseType.Sint),
        new (VertexFormat.Sint32x3,    12, 3, 4, VertexFormatBaseType.Sint),
        new (VertexFormat.Sint32x4,    16, 4, 4, VertexFormatBaseType.Sint),
        new (VertexFormat.RGB10A2Unorm, 5, 4, 4, VertexFormatBaseType.Float),
    };

    public static ref readonly VertexFormatInfo GetFormatInfo(this VertexFormat format)
    {
        if (format >= VertexFormat.Count)
        {
            return ref s_vertexFormatInfos[0]; // Undefines
        }

        Guard.IsTrue(s_vertexFormatInfos[(uint)format].Format == format);
        return ref s_vertexFormatInfos[(uint)format];
    }

    public static uint GetSizeInBytes(this VertexFormat format) => GetFormatInfo(format).ByteSize;


    /// <summary>Determines whether a given value is a power of two.</summary>
    /// <param name="value">The value to check.</param>
    /// <returns><c>true</c> if <paramref name="value" /> is a power of two; otherwise, <c>false</c>.</returns>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public static bool IsPow2(ulong value)
    {
#if NET6_0_OR_GREATER
        return BitOperations.IsPow2(value);
#else
        return (value & (value - 1)) == 0 && value != 0;
#endif
    }

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public static ulong AlignUp(ulong address, ulong alignment)
    {
        Debug.Assert(IsPow2(alignment));

        return (address + (alignment - 1)) & ~(alignment - 1);
    }

    public static int DivideByMultiple(int value, int alignment)
    {
        return (value + alignment - 1) / alignment;
    }

    /// <inheritdoc cref="Unsafe.AsPointer{T}(ref T)" />
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public static T* AsPointer<T>(ref T source) where T : unmanaged => (T*)Unsafe.AsPointer(ref source);

    /// <inheritdoc cref="Unsafe.AsRef{T}(in T)" />
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public static ref T AsRef<T>(in T source) => ref Unsafe.AsRef(in source);

    /// <inheritdoc cref="MemoryMarshal.GetReference{T}(ReadOnlySpan{T})" />
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public static ref readonly T GetReference<T>(this ReadOnlySpan<T> span) => ref MemoryMarshal.GetReference(span);

    /// <inheritdoc cref="MemoryMarshal.GetReference{T}(ReadOnlySpan{T})" />
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public static ref readonly T GetReference<T>(this ReadOnlySpan<T> span, int index) => ref Unsafe.Add(ref MemoryMarshal.GetReference(span), index);

    /// <summary>Returns a pointer to the element of the span at index zero.</summary>
    /// <typeparam name="T">The type of items in <paramref name="span" />.</typeparam>
    /// <param name="span">The span from which the pointer is retrieved.</param>
    /// <returns>A pointer to the item at index zero of <paramref name="span" />.</returns>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public static T* GetPointer<T>(this ReadOnlySpan<T> span) where T : unmanaged => AsPointer(ref AsRef(in span.GetReference()));

    public static bool StencilTestEnabled(in DepthStencilDescriptor depthStencil)
    {
        return
            depthStencil.BackFaceStencil.StencilCompareFunction != CompareFunction.Always ||
            depthStencil.BackFaceStencil.StencilFailureOperation != StencilOperation.Keep ||
            depthStencil.BackFaceStencil.DepthFailureOperation != StencilOperation.Keep ||
            depthStencil.BackFaceStencil.DepthStencilPassOperation != StencilOperation.Keep ||
            depthStencil.FrontFaceStencil.StencilCompareFunction != CompareFunction.Always ||
            depthStencil.FrontFaceStencil.StencilFailureOperation != StencilOperation.Keep ||
            depthStencil.FrontFaceStencil.DepthFailureOperation != StencilOperation.Keep ||
            depthStencil.FrontFaceStencil.DepthStencilPassOperation != StencilOperation.Keep;
    }

}
