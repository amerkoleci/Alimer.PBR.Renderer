// Copyright © Amer Koleci and Contributors.
// Licensed under the MIT License (MIT). See LICENSE in the repository root for more information.

using Win32.Graphics.Direct3D11;
using D3D11StencilOperation = Win32.Graphics.Direct3D11.StencilOperation;
using D3D11CullMode = Win32.Graphics.Direct3D11.CullMode;
using D3D11FillMode = Win32.Graphics.Direct3D11.FillMode;
using D3D11BlendOperation = Win32.Graphics.Direct3D11.BlendOperation;
using System.Diagnostics;

namespace Alimer.Graphics.D3D12;

internal static class D3D12Utils
{
    private static readonly D3D11FillMode[] s_FillModeMap = new D3D11FillMode[(int)(FillMode.Wireframe + 1)] {
        D3D11FillMode.Solid,
        D3D11FillMode.Wireframe
    };

    private static readonly D3D11CullMode[] s_cullModeMap = new D3D11CullMode[(int)(CullMode.None + 1)] {
        D3D11CullMode.Back,
        D3D11CullMode.Front,
        D3D11CullMode.None,
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

    private static readonly D3D11BlendOperation[] s_blendOpMap = new D3D11BlendOperation[(int)(BlendOperation.Max + 1)] {
        D3D11BlendOperation.Add,
        D3D11BlendOperation.Subtract,
        D3D11BlendOperation.ReverseSubtract,
        D3D11BlendOperation.Min,
        D3D11BlendOperation.Max,
    };

    private static readonly D3D11StencilOperation[] s_stencilOperationMap = new D3D11StencilOperation[(int)(StencilOperation.DecrementWrap + 1)] {
        D3D11StencilOperation.Keep,
        D3D11StencilOperation.Zero,
        D3D11StencilOperation.Replace,
        D3D11StencilOperation.IncrementSaturate,
        D3D11StencilOperation.DecrementSaturate,
        D3D11StencilOperation.Invert,
        D3D11StencilOperation.Increment,
        D3D11StencilOperation.Decrement,
    };

    public static D3D11FillMode ToD3D11(this FillMode value) => s_FillModeMap[(uint)value];
    public static D3D11CullMode ToD3D11(this CullMode value) => s_cullModeMap[(uint)value];
    public static Blend ToD3D11(this BlendFactor factor) => s_blendFactorMap[(uint)factor];
    public static D3D11BlendOperation ToD3D11(this BlendOperation value) => s_blendOpMap[(uint)value];
    public static Blend ToD3D11AlphaBlend(this BlendFactor factor)
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
                return ToD3D11(factor);
        }
    }

    public static ColorWriteEnable ToD3D11(this ColorWriteMask writeMask)
    {
        Debug.Assert((byte)ColorWriteMask.Red == (byte)ColorWriteEnable.Red);
        Debug.Assert((byte)ColorWriteMask.Green == (byte)ColorWriteEnable.Green);
        Debug.Assert((byte)ColorWriteMask.Blue == (byte)ColorWriteEnable.Blue);
        Debug.Assert((byte)ColorWriteMask.Alpha == (byte)ColorWriteEnable.Alpha);

        return (ColorWriteEnable)writeMask;
    }

    public static ComparisonFunction ToD3D11(this CompareFunction function)
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

    public static FilterType ToD3D11(this SamplerMinMagFilter filter)
    {
        switch (filter)
        {
            case SamplerMinMagFilter.Nearest: return FilterType.Point;
            case SamplerMinMagFilter.Linear: return FilterType.Linear;

            default:
                return FilterType.Point;
        }
    }

    public static FilterType ToD3D11(this SamplerMipFilter filter)
    {
        switch (filter)
        {
            case SamplerMipFilter.Nearest: return FilterType.Point;
            case SamplerMipFilter.Linear: return FilterType.Linear;

            default:
                return FilterType.Point;
        }
    }

    public static TextureAddressMode ToD3D11(this SamplerAddressMode filter)
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

    public static D3D11StencilOperation ToD3D11(this StencilOperation operation) => s_stencilOperationMap[(uint)operation];

    public static DepthStencilOperationDescription ToD3D11(this StencilFaceState state)
    {
        return new DepthStencilOperationDescription(
            state.FailOperation.ToD3D11(),
            state.DepthFailOperation.ToD3D11(),
            state.PassOperation.ToD3D11(),
            state.CompareFunction.ToD3D11()
            );
    }
}
