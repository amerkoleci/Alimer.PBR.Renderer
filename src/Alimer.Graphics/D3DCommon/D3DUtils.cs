// Copyright (c) Amer Koleci and Contributors
// Licensed under the MIT License (MIT). See LICENSE in the repository root for more information.

using Win32;
using Win32.Graphics.Dxgi.Common;
using D3DPrimitiveTopology = Win32.Graphics.Direct3D.PrimitiveTopology;
using static Win32.Graphics.Dxgi.Common.Apis;

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
            case TextureFormat.RGB10A2Unorm: return Format.R10G10B10A2Unorm;
            case TextureFormat.RGB10A2Uint: return Format.R10G10B10A2Uint;
            case TextureFormat.RG11B10Ufloat: return Format.R11G11B10Float;
            case TextureFormat.RGB9E5Ufloat: return Format.R9G9B9E5SharedExp;
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
            case VertexFormat.UByte2: return DXGI_FORMAT_R8G8_UINT;
            case VertexFormat.UByte4: return DXGI_FORMAT_R8G8B8A8_UINT;
            case VertexFormat.Byte2: return DXGI_FORMAT_R8G8_SINT;
            case VertexFormat.Byte4: return DXGI_FORMAT_R8G8B8A8_SINT;
            case VertexFormat.UByte2Normalized: return DXGI_FORMAT_R8G8_UNORM;
            case VertexFormat.UByte4Normalized: return DXGI_FORMAT_R8G8B8A8_UNORM;
            case VertexFormat.Byte2Normalized: return DXGI_FORMAT_R8G8_SNORM;
            case VertexFormat.Byte4Normalized: return DXGI_FORMAT_R8G8B8A8_SNORM;

            case VertexFormat.UShort2: return DXGI_FORMAT_R16G16_UINT;
            case VertexFormat.UShort4: return DXGI_FORMAT_R16G16B16A16_UINT;
            case VertexFormat.Short2: return DXGI_FORMAT_R16G16_SINT;
            case VertexFormat.Short4: return DXGI_FORMAT_R16G16B16A16_SINT;
            case VertexFormat.UShort2Normalized: return DXGI_FORMAT_R16G16_UNORM;
            case VertexFormat.UShort4Normalized: return DXGI_FORMAT_R16G16B16A16_UNORM;
            case VertexFormat.Short2Normalized: return DXGI_FORMAT_R16G16_SNORM;
            case VertexFormat.Short4Normalized: return DXGI_FORMAT_R16G16B16A16_SNORM;
            case VertexFormat.Half2: return DXGI_FORMAT_R16G16_FLOAT;
            case VertexFormat.Half4: return DXGI_FORMAT_R16G16B16A16_FLOAT;

            case VertexFormat.Float: return DXGI_FORMAT_R32_FLOAT;
            case VertexFormat.Float2: return DXGI_FORMAT_R32G32_FLOAT;
            case VertexFormat.Float3: return DXGI_FORMAT_R32G32B32_FLOAT;
            case VertexFormat.Float4: return DXGI_FORMAT_R32G32B32A32_FLOAT;

            case VertexFormat.UInt: return DXGI_FORMAT_R32_UINT;
            case VertexFormat.UInt2: return DXGI_FORMAT_R32G32_UINT;
            case VertexFormat.UInt3: return DXGI_FORMAT_R32G32B32_UINT;
            case VertexFormat.UInt4: return DXGI_FORMAT_R32G32B32A32_UINT;

            case VertexFormat.Int: return DXGI_FORMAT_R32_SINT;
            case VertexFormat.Int2: return DXGI_FORMAT_R32G32_SINT;
            case VertexFormat.Int3: return DXGI_FORMAT_R32G32B32_SINT;
            case VertexFormat.Int4: return DXGI_FORMAT_R32G32B32A32_SINT;

            case VertexFormat.UInt1010102Normalized: return DXGI_FORMAT_R10G10B10A2_UNORM;
            case VertexFormat.RG11B10Float: return DXGI_FORMAT_R11G11B10_FLOAT;
            case VertexFormat.RGB9E5Float: return DXGI_FORMAT_R9G9B9E5_SHAREDEXP;

            default:
                return DXGI_FORMAT_UNKNOWN;
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

    public static unsafe uint GetRefCount(IUnknown* @interface)
    {
        @interface->AddRef();
        return @interface->Release();
    }
}
