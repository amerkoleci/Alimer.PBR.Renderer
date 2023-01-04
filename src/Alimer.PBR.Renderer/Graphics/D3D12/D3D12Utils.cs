// Copyright © Amer Koleci and Contributors.
// Licensed under the MIT License (MIT). See LICENSE in the repository root for more information.

using Win32.Graphics.Direct3D12;
using D3DStencilOperation = Win32.Graphics.Direct3D12.StencilOperation;
using D3DCullMode = Win32.Graphics.Direct3D12.CullMode;
using D3DFillMode = Win32.Graphics.Direct3D12.FillMode;
using D3DBlendOperation = Win32.Graphics.Direct3D12.BlendOperation;
using System.Diagnostics;

namespace Alimer.Graphics.D3D12;

internal static class D3D12Utils
{
    public static readonly HeapProperties DefaultHeapProps = new(HeapType.Default);
    public static readonly HeapProperties UploadHeapProps = new(HeapType.Upload);
    public static readonly HeapProperties ReadbackeapProps = new(HeapType.Readback);

    private static readonly D3DFillMode[] s_FillModeMap = new D3DFillMode[(int)(FillMode.Wireframe + 1)] {
        D3DFillMode.Solid,
        D3DFillMode.Wireframe
    };

    private static readonly D3DCullMode[] s_cullModeMap = new D3DCullMode[(int)(CullMode.None + 1)] {
        D3DCullMode.Back,
        D3DCullMode.Front,
        D3DCullMode.None,
    };

    private static readonly Blend[] s_blendFactorMap = new Blend[(int)(BlendFactor.OneMinusBlendColor + 1)] {
        Blend.Zero,
        Blend.One,
        Blend.SrcColor,
        Blend.InverseSrcColor,
        Blend.SrcAlpha,
        Blend.InverseSrcAlpha,
        Blend.DestColor,
        Blend.InverseDestColor,
        Blend.DestAlpha,
        Blend.InverseDestAlpha,
        Blend.SrcAlphaSaturate,
        Blend.BlendFactor,
        Blend.InverseBlendFactor
    };

    private static readonly D3DBlendOperation[] s_blendOpMap = new D3DBlendOperation[(int)(BlendOperation.Max + 1)] {
        D3DBlendOperation.Add,
        D3DBlendOperation.Subtract,
        D3DBlendOperation.ReverseSubtract,
        D3DBlendOperation.Min,
        D3DBlendOperation.Max,
    };

    private static readonly D3DStencilOperation[] s_stencilOperationMap = new D3DStencilOperation[(int)(StencilOperation.DecrementWrap + 1)] {
        D3DStencilOperation.Keep,
        D3DStencilOperation.Zero,
        D3DStencilOperation.Replace,
        D3DStencilOperation.IncrementSaturate,
        D3DStencilOperation.DecrementSaturate,
        D3DStencilOperation.Invert,
        D3DStencilOperation.Increment,
        D3DStencilOperation.Decrement,
    };

    public static D3DFillMode ToD3D12(this FillMode value) => s_FillModeMap[(uint)value];
    public static D3DCullMode ToD3D12(this CullMode value) => s_cullModeMap[(uint)value];
    public static Blend ToD3D12(this BlendFactor factor) => s_blendFactorMap[(uint)factor];
    public static D3DBlendOperation ToD3D12(this BlendOperation value) => s_blendOpMap[(uint)value];
    public static Blend ToD3D12AlphaBlend(this BlendFactor factor)
    {
        switch (factor)
        {
            case BlendFactor.SourceColor:
                return Blend.SrcAlpha;
            case BlendFactor.OneMinusSourceColor:
                return Blend.InverseSrcAlpha;
            case BlendFactor.DestinationColor:
                return Blend.DestAlpha;
            case BlendFactor.OneMinusDestinationColor:
                return Blend.InverseDestAlpha;
            //case BlendFactor.Source1Color:
            //    return Blend.Src1Alpha;
            //case BlendFactor.OneMinusSource1Color:
            //    return Blend.InverseSrc1Alpha;
            // Other blend factors translate to the same D3D12 enum as the color blend factors.
            default:
                return ToD3D12(factor);
        }
    }

    public static ColorWriteEnable ToD3D12(this ColorWriteMask writeMask)
    {
        Debug.Assert((byte)ColorWriteMask.Red == (byte)ColorWriteEnable.Red);
        Debug.Assert((byte)ColorWriteMask.Green == (byte)ColorWriteEnable.Green);
        Debug.Assert((byte)ColorWriteMask.Blue == (byte)ColorWriteEnable.Blue);
        Debug.Assert((byte)ColorWriteMask.Alpha == (byte)ColorWriteEnable.Alpha);

        return (ColorWriteEnable)writeMask;
    }

    public static ComparisonFunction ToD3D12(this CompareFunction function)
    {
        switch (function)
        {
            case CompareFunction.Never: return ComparisonFunction.Never;
            case CompareFunction.Less: return ComparisonFunction.Less;
            case CompareFunction.Equal: return ComparisonFunction.Equal;
            case CompareFunction.LessEqual: return ComparisonFunction.LessEqual;
            case CompareFunction.Greater: return ComparisonFunction.Greater;
            case CompareFunction.NotEqual: return ComparisonFunction.NotEqual;
            case CompareFunction.GreaterEqual: return ComparisonFunction.GreaterEqual;
            case CompareFunction.Always: return ComparisonFunction.Always;

            default:
                return ComparisonFunction.Never;
        }
    }

    public static FilterType ToD3D12(this SamplerMinMagFilter filter)
    {
        switch (filter)
        {
            case SamplerMinMagFilter.Nearest: return FilterType.Point;
            case SamplerMinMagFilter.Linear: return FilterType.Linear;

            default:
                return FilterType.Point;
        }
    }

    public static FilterType ToD3D12(this SamplerMipFilter filter)
    {
        switch (filter)
        {
            case SamplerMipFilter.Nearest: return FilterType.Point;
            case SamplerMipFilter.Linear: return FilterType.Linear;

            default:
                return FilterType.Point;
        }
    }

    public static TextureAddressMode ToD3D12(this SamplerAddressMode filter)
    {
        switch (filter)
        {
            case SamplerAddressMode.Repeat: return TextureAddressMode.Wrap;
            case SamplerAddressMode.MirrorRepeat: return TextureAddressMode.Mirror;
            case SamplerAddressMode.ClampToEdge: return TextureAddressMode.Clamp;

            default:
                return TextureAddressMode.Wrap;
        }
    }

    public static D3DStencilOperation ToD3D12(this StencilOperation operation) => s_stencilOperationMap[(uint)operation];

    public static DepthStencilOperationDescription ToD3D12(this StencilFaceState state)
    {
        return new DepthStencilOperationDescription(
            state.FailOperation.ToD3D12(),
            state.DepthFailOperation.ToD3D12(),
            state.PassOperation.ToD3D12(),
            state.CompareFunction.ToD3D12()
            );
    }
}
