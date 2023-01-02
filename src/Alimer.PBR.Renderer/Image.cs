// Copyright © Amer Koleci and Contributors.
// Licensed under the MIT License (MIT). See LICENSE in the repository root for more information.

using System.Runtime.CompilerServices;
using Alimer.Graphics;
using StbImageSharp;

namespace Alimer.PBR.Renderer;

public sealed class Image
{
    private Image(int width, int height, TextureFormat format, Memory<byte> data)
    {
        Width = width;
        Height = height;
        Format = format;
        Data = data;
        BytesPerPixel = format.BytesPerPixels();
    }

    public int Width { get; }
    public int Height { get; }
    public TextureFormat Format { get; }
    public Memory<byte> Data { get; }

    public int BytesPerPixel { get; }
    public int Pitch => Width * BytesPerPixel;

    public static Image FromFile(string filePath, int channels = 4)
    {
        using FileStream stream = new(filePath, FileMode.Open);
        return FromStream(stream, channels);
    }

    public static unsafe Image FromStream(Stream stream, int channels = 4)
    {
        StbImage.stbi__context s = new(stream);
        bool isHdr = StbImage.stbi__hdr_test(s) == 1;
        StbImage.stbi__rewind(s);

        if (isHdr)
        {
            ImageResultFloat imageResultFloat = ImageResultFloat.FromStream(stream, (ColorComponents)channels);
            byte[] imageData = new byte[imageResultFloat.Data.Length * sizeof(float)];
            fixed (float* srcDataPtr = imageResultFloat.Data)
            fixed (byte* destDataPtr = imageData)
            {
                Unsafe.CopyBlockUnaligned(destDataPtr, srcDataPtr, (uint)imageData.Length);
            }

            return new(imageResultFloat.Width, imageResultFloat.Height, TextureFormat.Rgba32Float, imageData);
        }
        else
        {
            ImageResult imageResult = ImageResult.FromStream(stream, (ColorComponents)channels);
            TextureFormat format = TextureFormat.Rgba8Unorm;
            switch(imageResult.Comp)
            {
                case ColorComponents.Grey:
                    format = TextureFormat.R8Unorm;
                    break;
                case ColorComponents.GreyAlpha:
                    format = TextureFormat.Rg8Unorm;
                    break;
                case ColorComponents.RedGreenBlue:
                    throw new NotSupportedException("RGB images are not supported");
                case ColorComponents.RedGreenBlueAlpha:
                    format = TextureFormat.Rgba8Unorm;
                    break;
            }
            return new(imageResult.Width, imageResult.Height, format, imageResult.Data);
        }
    }
}
