// Copyright © Amer Koleci and Contributors.
// Licensed under the MIT License (MIT). See LICENSE in the repository root for more information.

using System.Numerics;
using System.Runtime.InteropServices;
using Alimer.Graphics;
using CommunityToolkit.Diagnostics;
using Vortice.Mathematics;

namespace Alimer.PBR.Renderer;

public sealed unsafe class ConstantBuffer<T> : GraphicsObject
     where T : unmanaged
{
    public readonly uint SizeInBytes;
    public readonly GraphicsBuffer Buffer;

    public ConstantBuffer(GraphicsDevice device, string? label = default)
    {
        Guard.IsNotNull(device, nameof(device));

        SizeInBytes = MathHelper.AlignUp((uint)sizeof(T), 16);
        BufferDescription description = new(SizeInBytes, BufferUsage.Constant, CpuAccessMode.Write, label);
        Buffer = device.CreateBuffer(description);
    }

    // <summary>
    /// Finalizes an instance of the <see cref="ConstantBuffer" /> class.
    /// </summary>
    ~ConstantBuffer() => Dispose(disposing: false);

    protected override void Dispose(bool disposing)
    {
        if (disposing)
        {
            Buffer.Dispose();
        }
    }

    public void SetData(CommandContext context, T data)
    {
        context.UpdateConstantBuffer(Buffer, &data, SizeInBytes);
    }
}

