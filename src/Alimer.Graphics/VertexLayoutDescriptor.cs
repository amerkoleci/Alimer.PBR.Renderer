// Copyright (c) Amer Koleci and Contributors.
// Licensed under the MIT License (MIT). See LICENSE in the repository root for more information.

namespace Alimer.Graphics;

public readonly record struct VertexLayoutDescriptor
{
    public VertexLayoutDescriptor(params VertexAttributeDescriptor[] attributes)
    {
        Attributes = attributes;

        uint computedStride = 0;
        for (int i = 0; i < attributes.Length; i++)
        {
            uint elementSize = attributes[i].Format.GetSizeInBytes();
            if (attributes[i].Offset != 0)
            {
                computedStride = attributes[i].Offset + elementSize;
            }
            else
            {
                computedStride += elementSize;
            }
        }

        Stride = computedStride;
        StepMode = VertexStepMode.Vertex;
    }

    public VertexLayoutDescriptor(uint stride, params VertexAttributeDescriptor[] attributes)
    {
        Attributes = attributes;
        Stride = stride;
        StepMode = VertexStepMode.Vertex;
    }

    public VertexLayoutDescriptor(uint stride, VertexStepMode stepMode, params VertexAttributeDescriptor[] attributes)
    {
        Attributes = attributes;
        Stride = stride;
        StepMode = stepMode;
    }

    public VertexAttributeDescriptor[] Attributes { get; init; }
    public uint Stride { get; init; }
    public VertexStepMode StepMode { get; init; }
}
