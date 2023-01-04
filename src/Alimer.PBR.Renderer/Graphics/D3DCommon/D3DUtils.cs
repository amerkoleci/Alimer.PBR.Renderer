// Copyright © Amer Koleci and Contributors.
// Licensed under the MIT License (MIT). See LICENSE in the repository root for more information.

using Win32.Graphics.Dxgi.Common;
using D3DPrimitiveTopology = Win32.Graphics.Direct3D.PrimitiveTopology;

namespace Alimer.Graphics;

internal static class D3DUtils
{
    public static Format ToDxgiFormat(this TextureFormat format)
    {
        switch (format)
        {
            // 8-bit formats
            case TextureFormat.R8Unorm: return Format.R8Unorm;
            case TextureFormat.R8Snorm: return Format.R8Snorm;
            case TextureFormat.R8Uint: return Format.R8Uint;
            case TextureFormat.R8Sint: return Format.R8Sint;
            // 16-bit formats
            case TextureFormat.R16Unorm: return Format.R16Unorm;
            case TextureFormat.R16Snorm: return Format.R16Snorm;
            case TextureFormat.R16Uint: return Format.R16Uint;
            case TextureFormat.R16Sint: return Format.R16Sint;
            case TextureFormat.R16Float: return Format.R16Float;
            case TextureFormat.Rg8Unorm: return Format.R8G8Unorm;
            case TextureFormat.Rg8Snorm: return Format.R8G8Snorm;
            case TextureFormat.Rg8Uint: return Format.R8G8Uint;
            case TextureFormat.Rg8Sint: return Format.R8G8Sint;
            // Packed 16-Bit Pixel Formats
            case TextureFormat.Bgra4Unorm: return Format.B4G4R4A4Unorm;
            case TextureFormat.B5G6R5Unorm: return Format.B5G6R5Unorm;
            case TextureFormat.Bgr5A1Unorm: return Format.B5G5R5A1Unorm;
            // 32-bit formats
            case TextureFormat.R32Uint: return Format.R32Uint;
            case TextureFormat.R32Sint: return Format.R32Sint;
            case TextureFormat.R32Float: return Format.R32Float;
            case TextureFormat.Rg16Unorm: return Format.R16G16Unorm;
            case TextureFormat.Rg16Snorm: return Format.R16G16Snorm;
            case TextureFormat.Rg16Uint: return Format.R16G16Uint;
            case TextureFormat.Rg16Sint: return Format.R16G16Sint;
            case TextureFormat.Rg16Float: return Format.R16G16Float;
            case TextureFormat.Rgba8Unorm: return Format.R8G8B8A8Unorm;
            case TextureFormat.Rgba8UnormSrgb: return Format.R8G8B8A8UnormSrgb;
            case TextureFormat.Rgba8Snorm: return Format.R8G8B8A8Snorm;
            case TextureFormat.Rgba8Uint: return Format.R8G8B8A8Uint;
            case TextureFormat.Rgba8Sint: return Format.R8G8B8A8Sint;
            case TextureFormat.Bgra8Unorm: return Format.B8G8R8A8Unorm;
            case TextureFormat.Bgra8UnormSrgb: return Format.B8G8R8A8UnormSrgb;
            // Packed 32-Bit formats
            case TextureFormat.Rgb9e5Ufloat: return Format.R9G9B9E5SharedExp;
            case TextureFormat.Rgb10a2Unorm: return Format.R10G10B10A2Unorm;
            case TextureFormat.Rgb10a2Uint: return Format.R10G10B10A2Uint;
            case TextureFormat.Rg11b10Float: return Format.R11G11B10Float;
            // 64-Bit formats
            case TextureFormat.Rg32Uint: return Format.R32G32Uint;
            case TextureFormat.Rg32Sint: return Format.R32G32Sint;
            case TextureFormat.Rg32Float: return Format.R32G32Float;
            case TextureFormat.Rgba16Unorm: return Format.R16G16B16A16Unorm;
            case TextureFormat.Rgba16Snorm: return Format.R16G16B16A16Snorm;
            case TextureFormat.Rgba16Uint: return Format.R16G16B16A16Uint;
            case TextureFormat.Rgba16Sint: return Format.R16G16B16A16Sint;
            case TextureFormat.Rgba16Float: return Format.R16G16B16A16Float;
            // 128-Bit formats
            case TextureFormat.Rgba32Uint: return Format.R32G32B32A32Uint;
            case TextureFormat.Rgba32Sint: return Format.R32G32B32A32Sint;
            case TextureFormat.Rgba32Float: return Format.R32G32B32A32Float;
            // Depth-stencil formats
            case TextureFormat.Depth16Unorm: return Format.D16Unorm;
            case TextureFormat.Depth32Float: return Format.D32Float;
            case TextureFormat.Stencil8: return Format.D24UnormS8Uint;
            case TextureFormat.Depth24UnormStencil8: return Format.D24UnormS8Uint;
            case TextureFormat.Depth32FloatStencil8: return Format.D32FloatS8X24Uint;
            // Compressed BC formats
            case TextureFormat.Bc1RgbaUnorm: return Format.BC1Unorm;
            case TextureFormat.Bc1RgbaUnormSrgb: return Format.BC1UnormSrgb;
            case TextureFormat.Bc2RgbaUnorm: return Format.BC2Unorm;
            case TextureFormat.Bc2RgbaUnormSrgb: return Format.BC2UnormSrgb;
            case TextureFormat.Bc3RgbaUnorm: return Format.BC3Unorm;
            case TextureFormat.Bc3RgbaUnormSrgb: return Format.BC3UnormSrgb;
            case TextureFormat.Bc4RSnorm: return Format.BC4Unorm;
            case TextureFormat.Bc4RUnorm: return Format.BC4Snorm;
            case TextureFormat.Bc5RgUnorm: return Format.BC5Unorm;
            case TextureFormat.Bc5RgSnorm: return Format.BC5Snorm;
            case TextureFormat.Bc6hRgbSfloat: return Format.BC6HSF16;
            case TextureFormat.Bc6hRgbUfloat: return Format.BC6HUF16;
            case TextureFormat.Bc7RgbaUnorm: return Format.BC7Unorm;
            case TextureFormat.Bc7RgbaUnormSrgb: return Format.BC7UnormSrgb;

            default:
                return Format.Unknown;
        }
    }

    public static Format ToDxgiFormat(this VertexFormat format)
    {
        switch (format)
        {
            case VertexFormat.Uint8x2: return Format.R8G8Uint;
            case VertexFormat.Uint8x4: return Format.R8G8B8A8Uint;
            case VertexFormat.Sint8x2: return Format.R8G8Sint;
            case VertexFormat.Sint8x4: return Format.R8G8B8A8Sint;
            case VertexFormat.Unorm8x2: return Format.R8G8Unorm;
            case VertexFormat.Unorm8x4: return Format.R8G8B8A8Unorm;
            case VertexFormat.Snorm8x2: return Format.R8G8Snorm;
            case VertexFormat.Snorm8x4: return Format.R8G8B8A8Snorm;

            case VertexFormat.Uint16x2: return Format.R16G16Uint;
            case VertexFormat.Uint16x4: return Format.R16G16B16A16Uint;
            case VertexFormat.Sint16x2: return Format.R16G16Sint;
            case VertexFormat.Sint16x4: return Format.R16G16B16A16Sint;
            case VertexFormat.Unorm16x2: return Format.R16G16Unorm;
            case VertexFormat.Unorm16x4: return Format.R16G16B16A16Unorm;
            case VertexFormat.Snorm16x2: return Format.R16G16Snorm;
            case VertexFormat.Snorm16x4: return Format.R16G16B16A16Snorm;
            case VertexFormat.Float16x2: return Format.R16G16Float;
            case VertexFormat.Float16x4: return Format.R16G16B16A16Float;

            case VertexFormat.Float32: return Format.R32Float;
            case VertexFormat.Float32x2: return Format.R32G32Float;
            case VertexFormat.Float32x3: return Format.R32G32B32Float;
            case VertexFormat.Float32x4: return Format.R32G32B32A32Float;

            case VertexFormat.Uint32: return Format.R32Uint;
            case VertexFormat.Uint32x2: return Format.R32G32Uint;
            case VertexFormat.Uint32x3: return Format.R32G32B32Uint;
            case VertexFormat.Uint32x4: return Format.R32G32B32A32Uint;

            case VertexFormat.Sint32: return Format.R32Sint;
            case VertexFormat.Sint32x2: return Format.R32G32Sint;
            case VertexFormat.Sint32x3: return Format.R32G32B32Sint;
            case VertexFormat.Sint32x4: return Format.R32G32B32A32Sint;

            case VertexFormat.RGB10A2Unorm: return Format.R10G10B10A2Unorm;

            default:
                return Format.Unknown;
        }
    }

    public static Format ToDxgiFormat(this IndexType indexType)
    {
        switch (indexType)
        {
            case IndexType.Uint16: return Format.R16Uint;
            case IndexType.Uint32: return Format.R32Uint;
           
            default:
                return Format.R16Uint;
        }
    }

    public static D3DPrimitiveTopology ToD3DPrimitiveTopology(this PrimitiveTopology value)
    {
        switch (value)
        {
            case PrimitiveTopology.PointList: return D3DPrimitiveTopology.PointList;
            case PrimitiveTopology.LineList: return D3DPrimitiveTopology.LineList;
            case PrimitiveTopology.LineStrip: return D3DPrimitiveTopology.LineStrip;
            case PrimitiveTopology.TriangleList: return D3DPrimitiveTopology.TriangleList;
            case PrimitiveTopology.TriangleStrip: return D3DPrimitiveTopology.TriangleStrip;

            default:
                return D3DPrimitiveTopology.PointList;
        }
    }
}
