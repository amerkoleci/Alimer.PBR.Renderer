﻿// Copyright © Amer Koleci and Contributors.
// Licensed under the MIT License (MIT). See LICENSE in the repository root for more information.

using System.Drawing;
using System.Runtime.InteropServices;
using Alimer.Bindings.SDL;
using Alimer.Graphics;
using Alimer.Graphics.D3D11;
using Vortice.Mathematics;
using static Alimer.Bindings.SDL.SDL;
using static Alimer.Bindings.SDL.SDL.SDL_EventType;
using static Alimer.Bindings.SDL.SDL.SDL_LogPriority;
using static Alimer.Bindings.SDL.SDL.SDL_WindowFlags;

namespace Alimer.PBR.Renderer;

public sealed class Application : GraphicsObject
{
    private const int _eventsPerPeep = 64;
    private static readonly unsafe SDL_Event* _events = (SDL_Event*)NativeMemory.Alloc(_eventsPerPeep, (nuint)sizeof(SDL_Event));

    private readonly GraphicsDevice _graphicsDevice;
    private readonly SDL_Window _window;
    private bool _exitRequested;
    private ViewSettings _viewSettings;
    private readonly FrameBuffer _framebuffer;
    private readonly FrameBuffer _resolveFramebuffer;
    private readonly Pipeline _skyboxPipeline;

    public Application(GraphicsBackend graphicsBackend, int width = 1200, int height = 800, int maxSamples = 16)
    {
        //SDL_GetVersion(out SDL_version version);
        //Log.Info($"SDL v{version.major}.{version.minor}.{version.patch}");

        // DPI aware on Windows
        SDL_SetHint(SDL_HINT_WINDOWS_DPI_AWARENESS, "permonitorv2");
        SDL_SetHint(SDL_HINT_WINDOWS_DPI_SCALING, true);

        // Init SDL
        if (SDL_Init(SDL_INIT_TIMER | SDL_INIT_VIDEO | SDL_INIT_EVENTS) != 0)
        {
            var error = SDL_GetError();
            throw new Exception($"Failed to start SDL2: {error}");
        }


        SDL_WindowFlags flags = SDL_WINDOW_ALLOW_HIGHDPI | SDL_WINDOW_HIDDEN | SDL_WINDOW_RESIZABLE;

        _window = SDL_CreateWindow("Physically Based Rendering (Direct3D 11)",
            SDL_WINDOWPOS_CENTERED,
            SDL_WINDOWPOS_CENTERED,
            width, height, flags);

        _graphicsDevice = GraphicsDevice.CreateDefault(_window, maxSamples);
        _viewSettings = new(0.0f, 0.0f, 150.0f, 45.0f);

        _framebuffer = AddDisposable(_graphicsDevice.CreateFrameBuffer(new Size(width, height),
            _graphicsDevice.Samples, TextureFormat.Rgba16Float, TextureFormat.Depth32FloatStencil8)
            );

        if (_graphicsDevice.Samples > 1)
        {
            _resolveFramebuffer = AddDisposable(
                _graphicsDevice.CreateFrameBuffer(new Size(width, height), 1, TextureFormat.Rgba16Float, TextureFormat.Invalid)
                );
        }
        else
        {
            _resolveFramebuffer = _framebuffer;
        }

        RenderPipelineDescription skyboxPipelineDesc = new();
        _skyboxPipeline = AddDisposable(_graphicsDevice.CreateRenderPipeline(skyboxPipelineDesc));

        // Unfiltered environment cube map (temporary).
        Texture envTextureUnfiltered = CreateTextureCube(TextureFormat.Rgba16Float, 1024, 1024);
        //createTextureUAV(envTextureUnfiltered, 0);

        // Load & convert equirectangular environment map to a cubemap texture.
        {
            ComputePipelineDescription equirectToCubePipelineDesc = new()
            {
                ComputeShader = CompileShader("equirect2cube.hlsl", "main", "cs_5_0"),
                Label = "EquirectToCube"
            };

            using Pipeline equirectToCubePipeline = _graphicsDevice.CreateComputePipeline(equirectToCubePipelineDesc);
            using Texture envTextureEquirect = CreateTexture(FromFile("environment.hdr"));

            CommandContext context = _graphicsDevice.DefaultContext;
            context.SetPipeline(equirectToCubePipeline);
            context.Dispatch(envTextureUnfiltered.Width / 32, envTextureUnfiltered.Height / 32, 6);
        }
    }

    private static Image FromFile(string fileName)
    {
        return Image.FromFile(Path.Combine(AppContext.BaseDirectory, "assets", "textures", fileName));
    }

    private static ReadOnlyMemory<byte> CompileShader(string fileName, string entryPoint, string profile)
    {
        string filePath = Path.Combine(AppContext.BaseDirectory, "assets", "shaders", "hlsl", fileName);
        string shaderSource = File.ReadAllText(filePath);

        return D3D11GraphicsDevice.CompileBytecode(shaderSource, entryPoint, profile);
    }


    private Texture CreateTextureCube(TextureFormat format, int width, int height,  int levels = 0)
    {
        TextureDescription desc = TextureDescription.TextureCube(format, width, height, levels, TextureUsage.ShaderRead | TextureUsage.ShaderWrite);
        return _graphicsDevice.CreateTexture(desc);
    }

    private Texture CreateTexture(Image image)
    {
        TextureDescription desc = TextureDescription.Texture2D(image.Format, image.Width, image.Height, 1, 1, TextureUsage.ShaderRead | TextureUsage.ShaderWrite);
        return _graphicsDevice.CreateTexture(image.Data.Span, desc);
    }

    protected override void Dispose(bool disposing)
    {
        base.Dispose(disposing);

        SDL_DestroyWindow(_window);
        _graphicsDevice.Dispose();

        SDL_Quit();
    }

    public void Run()
    {
        SDL_ShowWindow(_window);

        while (!_exitRequested)
        {
            PollSDLEvents();
            OnTick();
        }
    }

    private void OnTick()
    {
        if (!_graphicsDevice.BeginFrame())
            return;

        CommandContext context = _graphicsDevice.DefaultContext;

        // Prepare framebuffer for rendering.
        context.SetRenderTarget(_framebuffer);

        // Draw skybox.
        context.SetPipeline(_skyboxPipeline);

        // Draw a full screen triangle for postprocessing/tone mapping.
        context.SetRenderTarget(null);
        //m_context->IASetInputLayout(nullptr);
        //m_context->VSSetShader(m_tonemapProgram.vertexShader.Get(), nullptr, 0);
        //m_context->PSSetShader(m_tonemapProgram.pixelShader.Get(), nullptr, 0);
        //m_context->PSSetShaderResources(0, 1, m_resolveFramebuffer.srv.GetAddressOf());
        //m_context->PSSetSamplers(0, 1, m_computeSampler.GetAddressOf());
        //m_context->Draw(3, 0);


        _graphicsDevice.EndFrame();
    }

    private unsafe void PollSDLEvents()
    {
        SDL_PumpEvents();
        int eventsRead;

        do
        {
            eventsRead = SDL_PeepEvents(_events, _eventsPerPeep, SDL_eventaction.SDL_GETEVENT, SDL_EventType.SDL_FIRSTEVENT, SDL_EventType.SDL_LASTEVENT);
            for (int i = 0; i < eventsRead; i++)
            {
                HandleSDLEvent(_events[i]);
            }
        } while (eventsRead == _eventsPerPeep);
    }

    private void HandleSDLEvent(SDL_Event evt)
    {
        switch (evt.type)
        {
            case SDL_QUIT:
            case SDL_APP_TERMINATING:
                _exitRequested = true;
                break;

            case SDL_WINDOWEVENT:
                HandleWindowEvent(evt);
                break;
        }
    }

    private void HandleWindowEvent(in SDL_Event evt)
    {
    }
}
