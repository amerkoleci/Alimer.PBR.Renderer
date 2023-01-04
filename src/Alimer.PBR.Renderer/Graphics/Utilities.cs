// Copyright © Amer Koleci and Contributors.
// Licensed under the MIT License (MIT). See LICENSE in the repository root for more information.

using CommunityToolkit.Diagnostics;

namespace Alimer.Graphics;

public static class Utilities
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

    public static bool BlendEnabled(in RenderTargetBlendState state)
    {
        return
            state.BlendOperation != BlendOperation.Add ||
            state.SourceColorBlendFactor != BlendFactor.One ||
            state.DestinationColorBlendFactor != BlendFactor.Zero ||
            state.AlphaBlendOperation != BlendOperation.Add ||
            state.SourceAlphaBlendFactor != BlendFactor.One ||
            state.DestinationAlphaBlendFactor != BlendFactor.Zero;
    }

    public static bool StencilTestEnabled(in DepthStencilState state)
    {
        return
            state.BackFace.CompareFunction != CompareFunction.Always ||
            state.BackFace.FailOperation != StencilOperation.Keep ||
            state.BackFace.DepthFailOperation != StencilOperation.Keep ||
            state.BackFace.PassOperation != StencilOperation.Keep ||
            state.FrontFace.CompareFunction != CompareFunction.Always ||
            state.FrontFace.FailOperation != StencilOperation.Keep ||
            state.FrontFace.DepthFailOperation != StencilOperation.Keep ||
            state.FrontFace.PassOperation != StencilOperation.Keep;
    }

    public static int CalculateMipLevels(int width, int height, int mipLevels)
    {
        if (mipLevels > 1)
        {
            int maxMips = CountMips(width, height);
            if (mipLevels > maxMips)
                return maxMips;

            return mipLevels;
        }
        else if (mipLevels == 0)
        {
            return CountMips(width, height);
        }
        else
        {
            return 1;
        }
    }

    public static int CalculateMipLevels3D(int width, int height, int depth, int mipLevels)
    {
        if (mipLevels > 1)
        {
            int maxMips = CountMips3D(width, height, depth);
            if (mipLevels > maxMips)
                return maxMips;

            return mipLevels;
        }
        else if (mipLevels == 0)
        {
            return CountMips3D(width, height, depth);
        }
        else
        {
            return 1;
        }
    }


    private static int CountMips(int width, int height)
    {
        int mipLevels = 1;

        while (height > 1 || width > 1)
        {
            if (height > 1)
                height >>= 1;

            if (width > 1)
                width >>= 1;

            ++mipLevels;
        }

        return mipLevels;
    }

    private static int CountMips3D(int width, int height, int depth)
    {
        int mipLevels = 1;

        while (height > 1 || width > 1 || depth > 1)
        {
            if (height > 1)
                height >>= 1;

            if (width > 1)
                width >>= 1;

            if (depth > 1)
                depth >>= 1;

            ++mipLevels;
        }

        return mipLevels;
    }
}
