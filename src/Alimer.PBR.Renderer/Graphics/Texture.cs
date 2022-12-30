// Copyright © Amer Koleci and Contributors.
// Licensed under the MIT License (MIT). See LICENSE in the repository root for more information.

namespace Alimer.Graphics;

public abstract class Texture : GraphicsResource
{
    protected Texture(GraphicsDevice device, in TextureDescription description)
        : base(device, description.Label)
    {
        int arrayMultiplier = 1;
        if (description.Dimension == TextureDimension.TextureCube)
        {
            arrayMultiplier = 6;
        }

        Dimension = description.Dimension;
        Format = description.Format;
        Width = description.Width;
        Height = description.Height;
        Depth = description.Dimension == TextureDimension.Texture3D ? description.DepthOrArrayLayers : 1;
        ArrayLayers = description.Dimension != TextureDimension.Texture3D ? description.DepthOrArrayLayers * arrayMultiplier : 1;
        MipLevels = description.MipLevels;
        Usage = description.Usage;
        SampleCount = description.SampleCount;
    }

    /// <summary>
    /// Gets the texture dimension.
    /// </summary>
    public TextureDimension Dimension { get; }

    /// <summary>
    /// Gets the texture format.
    /// </summary>
    public TextureFormat Format { get; }

    /// <summary>
    /// Gets the texture width, in texels.
    /// </summary>
    public int Width { get; }

    /// <summary>
    /// Gets the texture height, in texels.
    /// </summary>
    public int Height { get; }

    /// <summary>
    /// Gets the texture depth, in texels.
    /// </summary>
    public int Depth { get; }

    /// <summary>
    /// Gets the texture total number of array layers.
    /// </summary>
    public int ArrayLayers { get; }

    /// <summary>
    /// Gets the texture total number of mipmap levels.
    /// </summary>
    public int MipLevels { get; protected set; }

    /// <summary>
    /// Gets the texture <see cref="TextureUsage"/>.
    /// </summary>
    public TextureUsage Usage { get; }

    /// <summary>
    /// Gets the texture sample count.
    /// </summary>
    public int SampleCount { get; }

    /// <summary>
    /// Get a mip-level width.
    /// </summary>
    /// <param name="mipLevel"></param>
    /// <returns></returns>
    public int GetWidth(int mipLevel = 0)
    {
        return (mipLevel == 0) || (mipLevel < MipLevels) ? Math.Max(1, Width >> mipLevel) : 1;
    }

    /// <summary>
    /// Get a mip-level height.
    /// </summary>
    /// <param name="mipLevel"></param>
    /// <returns></returns>
    public int GetHeight(int mipLevel = 0)
    {
        return (mipLevel == 0) || (mipLevel < MipLevels) ? Math.Max(1, Height >> mipLevel) : 1;
    }

    /// <summary>
    /// Get a mip-level depth.
    /// </summary>
    /// <param name="mipLevel"></param>
    /// <returns></returns>
    public int GetDepth(int mipLevel = 0)
    {
        if (Dimension != TextureDimension.Texture3D)
            return 1;

        return (mipLevel == 0) || (mipLevel < MipLevels) ? Math.Max(1, Depth >> mipLevel) : 1;
    }
}
