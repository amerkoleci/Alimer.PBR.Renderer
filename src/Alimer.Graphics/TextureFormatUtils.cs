// Copyright (c) Amer Koleci and Contributors.
// Licensed under the MIT License (MIT). See LICENSE in the repository root for more information.

namespace Alimer.Graphics;

public static class TextureFormatUtils
{
    public static int BytesPerPixels(this TextureFormat format)
    {
        switch (format)
        {
            // 8-bit formats
            case TextureFormat.R8Unorm:
            case TextureFormat.R8Snorm:
            case TextureFormat.R8Uint:
            case TextureFormat.R8Sint:
                return 1;
            // 16-bit formats
            case TextureFormat.R16Unorm:
            case TextureFormat.R16Snorm:
            case TextureFormat.R16Uint:
            case TextureFormat.R16Sint:
            case TextureFormat.R16Float:
            case TextureFormat.Rg8Unorm:
            case TextureFormat.Rg8Snorm:
            case TextureFormat.Rg8Uint:
            case TextureFormat.Rg8Sint:
                return 2;

            // Packed 16-Bit Pixel Formats
            case TextureFormat.Bgra4Unorm:
            case TextureFormat.B5G6R5Unorm:
            case TextureFormat.Bgr5A1Unorm:
                return 2;

            // 32-bit formats
            case TextureFormat.R32Uint:
            case TextureFormat.R32Sint:
            case TextureFormat.R32Float:
            case TextureFormat.Rg16Unorm:
            case TextureFormat.Rg16Snorm:
            case TextureFormat.Rg16Uint:
            case TextureFormat.Rg16Sint:
            case TextureFormat.Rg16Float:
            case TextureFormat.Rgba8Unorm:
            case TextureFormat.Rgba8UnormSrgb:
            case TextureFormat.Rgba8Snorm:
            case TextureFormat.Rgba8Uint:
            case TextureFormat.Rgba8Sint:
            case TextureFormat.Bgra8Unorm:
            case TextureFormat.Bgra8UnormSrgb:
                return 4;

            // Packed 32-Bit formats
            case TextureFormat.RGB10A2Unorm:
            case TextureFormat.RGB10A2Uint:
            case TextureFormat.RG11B10Ufloat:
            case TextureFormat.RGB9E5Ufloat:
                return 4;

            // 64-Bit formats
            case TextureFormat.Rg32Uint:
            case TextureFormat.Rg32Sint:
            case TextureFormat.Rg32Float:
            case TextureFormat.Rgba16Unorm:
            case TextureFormat.Rgba16Snorm:
            case TextureFormat.Rgba16Uint:
            case TextureFormat.Rgba16Sint:
            case TextureFormat.Rgba16Float:
                return 8;

            case TextureFormat.Rgba32Uint:
            case TextureFormat.Rgba32Sint:
            case TextureFormat.Rgba32Float:
                return 16;

            // Depth-stencil formats
            case TextureFormat.Depth16Unorm:
                return 2;
            case TextureFormat.Depth32Float:
                return 4;
            case TextureFormat.Stencil8:
                return 1;
            case TextureFormat.Depth24UnormStencil8:
                return 4;
            case TextureFormat.Depth32FloatStencil8:
                return 8;

            // Compressed BC formats
            case TextureFormat.Bc1RgbaUnorm:
            case TextureFormat.Bc1RgbaUnormSrgb:
            case TextureFormat.Bc4RSnorm:
            case TextureFormat.Bc4RUnorm:
                return 8;

            case TextureFormat.Bc2RgbaUnorm:
            case TextureFormat.Bc2RgbaUnormSrgb:
            case TextureFormat.Bc3RgbaUnorm:
            case TextureFormat.Bc3RgbaUnormSrgb:
            case TextureFormat.Bc5RgUnorm:
            case TextureFormat.Bc5RgSnorm:
            case TextureFormat.Bc6hRgbSfloat:
            case TextureFormat.Bc6hRgbUfloat:
            case TextureFormat.Bc7RgbaUnorm:
            case TextureFormat.Bc7RgbaUnormSrgb:
                return 16;

            default:
                return 1;
        }
    }

    /// <summary>
    /// Check if the format has a depth component.
    /// </summary>
    /// <param name="format">The <see cref="TextureFormat"/> to check.</param>
    /// <returns>True if format has depth component, false otherwise.</returns>
    public static bool IsDepthFormat(this TextureFormat format)
    {
        switch (format)
        {
            case TextureFormat.Depth16Unorm:
            case TextureFormat.Depth32Float:
            case TextureFormat.Depth24UnormStencil8:
            case TextureFormat.Depth32FloatStencil8:
                return true;
            default:
                return false;
        }
    }

    /// <summary>
    /// Check if the format has a stencil component.
    /// </summary>
    /// <param name="format">The <see cref="TextureFormat"/> to check.</param>
    /// <returns>True if format has stencil component, false otherwise.</returns>
    public static bool IsStencilFormat(TextureFormat format)
    {
        switch (format)
        {
            case TextureFormat.Stencil8:
            case TextureFormat.Depth24UnormStencil8:
            case TextureFormat.Depth32FloatStencil8:
                return true;
            default:
                return false;
        }
    }

    /// <summary>
    /// Check if the format has depth or stencil components.
    /// </summary>
    /// <param name="format">The <see cref="TextureFormat"/> to check.</param>
    /// <returns>True if format has depth or stencil component, false otherwise.</returns>
    public static bool IsDepthStencilFormat(this TextureFormat format)
    {
        switch (format)
        {
            case TextureFormat.Depth16Unorm:
            case TextureFormat.Depth32Float:
            case TextureFormat.Stencil8:
            case TextureFormat.Depth24UnormStencil8:
            case TextureFormat.Depth32FloatStencil8:
                return true;
            default:
                return false;
        }
    }
}
