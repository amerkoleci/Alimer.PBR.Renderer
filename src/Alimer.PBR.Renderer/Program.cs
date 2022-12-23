// Copyright © Amer Koleci and Contributors.
// Licensed under the MIT License (MIT). See LICENSE in the repository root for more information.

using Alimer.Graphics;

namespace Alimer.PBR.Renderer;

public static class Program
{
    /// <summary>
    /// The main entry point for the application.
    /// </summary>
    //[STAThread]
    public static void Main(string[] args)
    {
        using Application app = new(GraphicsBackend.Direct3D11);
        app.Run();
    }
}
