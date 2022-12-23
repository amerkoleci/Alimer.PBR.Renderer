// Copyright © Amer Koleci and Contributors.
// Licensed under the MIT License (MIT). See LICENSE in the repository root for more information.

namespace Alimer.Graphics;

public static class TextureFormatUtils
{
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
