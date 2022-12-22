// Copyright © Amer Koleci and Contributors.
// Licensed under the MIT License (MIT). See LICENSE in the repository root for more information.

namespace Alimer.PBR.Renderer;

public static class Program
{
    /// <summary>
    /// The main entry point for the application.
    /// </summary>
    //[STAThread]
    public static void Main(string[] args)
    {
        IRenderer renderer = new D3D11Renderer();
        using Application app = new(renderer);
        app.Run();
    }
}
