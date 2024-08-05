// Copyright (c) Amer Koleci and Contributors.
// Licensed under the MIT License (MIT). See LICENSE in the repository root for more information.

namespace Alimer.Graphics;

public static class Utilities
{
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
