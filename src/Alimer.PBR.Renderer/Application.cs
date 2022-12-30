﻿// Copyright © Amer Koleci and Contributors.
// Licensed under the MIT License (MIT). See LICENSE in the repository root for more information.

using System.Drawing;
using System.Numerics;
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

    private readonly Sampler _defaultSampler;
    private readonly Sampler _computeSampler;
    //private readonly Sampler _spBRDF_Sampler;
    private readonly Pipeline _skyboxPipeline;
    private readonly Mesh _skybox;
    private readonly Texture _envTexture;

    private readonly GraphicsBuffer _triangleVertexBuffer;
    private readonly Pipeline _trianglePipeline;

    private readonly ConstantBuffer<TransformCB> _transformCB;

    public Size Size { get; private set; }

    public Application(GraphicsBackend graphicsBackend, int width = 1200, int height = 800, int maxSamples = 16)
    {
        SDL_GetVersion(out SDL_version version);
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

        Size = new Size(width, height);

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

        SamplerDescription samplerDesc = new()
        {
            AddressModeU = SamplerAddressMode.Repeat,
            AddressModeV = SamplerAddressMode.Repeat,
            AddressModeW = SamplerAddressMode.Repeat,
            MinFilter = SamplerMinMagFilter.Linear,
            MagFilter = SamplerMinMagFilter.Linear,
            MipFilter = SamplerMipFilter.Linear,
            MaxAnisotropy = 16
        };
        _defaultSampler = AddDisposable(_graphicsDevice.CreateSampler(samplerDesc));

        SamplerDescription samplerComputeSampler = new()
        {
            AddressModeU = SamplerAddressMode.Repeat,
            AddressModeV = SamplerAddressMode.Repeat,
            AddressModeW = SamplerAddressMode.Repeat,
            MinFilter = SamplerMinMagFilter.Linear,
            MagFilter = SamplerMinMagFilter.Linear,
            MipFilter = SamplerMipFilter.Linear,
            MaxAnisotropy = 1
        };
        _computeSampler = AddDisposable(_graphicsDevice.CreateSampler(samplerComputeSampler));

        _transformCB = AddDisposable(new ConstantBuffer<TransformCB>(_graphicsDevice));

        _skybox = AddDisposable(MeshFromFile("skybox.obj"));
        RenderPipelineDescription skyboxPipelineDesc = new()
        {
            VertexShader = CompileShader("skybox.hlsl", "main_vs", "vs_5_0"),
            FragmentShader = CompileShader("skybox.hlsl", "main_ps", "ps_5_0"),
            VertexDescriptor = new(new VertexLayoutDescriptor(new VertexAttributeDescriptor(VertexFormat.Float32x3, 0))),
            DepthStencil = new DepthStencilDescriptor()
            {
                DepthWriteEnabled = false,
                DepthCompare = CompareFunction.Always
            }
        };
        _skyboxPipeline = AddDisposable(_graphicsDevice.CreateRenderPipeline(skyboxPipelineDesc));

        // Unfiltered environment cube map (temporary).
        using Texture envTextureUnfiltered = CreateTextureCube(TextureFormat.Rgba16Float, 1024, 1024);

        // Load & convert equirectangular environment map to a cubemap texture.
        CommandContext context = _graphicsDevice.DefaultContext;
        {
            ComputePipelineDescription equirectToCubePipelineDesc = new()
            {
                ComputeShader = CompileShader("equirect2cube.hlsl", "main", "cs_5_0"),
                Label = "EquirectToCube"
            };

            using Pipeline equirectToCubePipeline = _graphicsDevice.CreateComputePipeline(equirectToCubePipelineDesc);
            using Texture envTextureEquirect = CreateTexture(FromFile("environment.hdr"));

            context.SetSRV(0, envTextureEquirect);
            context.SetUAV(0, envTextureUnfiltered, 0);
            context.SetSampler(0, _computeSampler);
            context.SetPipeline(equirectToCubePipeline);
            context.Dispatch(envTextureUnfiltered.Width / 32, envTextureUnfiltered.Height / 32, 6);
        }

        context.GenerateMips(envTextureUnfiltered);

        // Compute pre-filtered specular environment map.
        {

            ComputePipelineDescription spmapPipelineDesc = new()
            {
                ComputeShader = CompileShader("spmap.hlsl", "main", "cs_5_0"),
                Label = "Spmap"
            };

            using ConstantBuffer<SpecularMapFilterSettingsCB> spmapCB = new(_graphicsDevice);
            using Pipeline spmapPipeline = _graphicsDevice.CreateComputePipeline(spmapPipelineDesc);

            _envTexture = AddDisposable(CreateTextureCube(TextureFormat.Rgba16Float, 1024, 1024));

            // Copy 0th mipmap level into destination environment map.
            for (int arraySlice = 0; arraySlice < 6; ++arraySlice)
            {
                context.CopyTexture(envTextureUnfiltered, arraySlice, _envTexture, arraySlice);
            }

            context.SetSRV(0, envTextureUnfiltered);
            context.SetSampler(0, _computeSampler);
            context.SetPipeline(spmapPipeline);

            // Pre-filter rest of the mip chain.
            float deltaRoughness = 1.0f / Math.Max(_envTexture.MipLevels - 1.0f, 1.0f);
            for (int level = 1, size = 512; level < _envTexture.MipLevels; ++level, size /= 2)
            {
                int numGroups = Math.Max(1, size / 32);

                // Update cbuffer
                SpecularMapFilterSettingsCB spmapConstants = new()
                {
                    Roughness = level * deltaRoughness
                };
                spmapCB.SetData(context, spmapConstants);
                context.SetConstantBuffer(0, spmapCB.Buffer);
                context.SetUAV(0, _envTexture, level);
                context.Dispatch(numGroups, numGroups, 6);
            }
        }

        ReadOnlySpan<VertexPositionColor> triangleVertices = stackalloc VertexPositionColor[]
        {
            new VertexPositionColor(new Vector3(0.0f, 0.5f, 0.5f), Colors.Red),
            new VertexPositionColor(new Vector3(0.5f, -0.5f, 0.5f), Colors.Lime),
            new VertexPositionColor(new Vector3(-0.5f, -0.5f, 0.5f), Colors.Blue)
        };
        _triangleVertexBuffer = AddDisposable(_graphicsDevice.CreateBuffer(triangleVertices, BufferUsage.Vertex));

        RenderPipelineDescription trianglePipeline = new()
        {
            VertexShader = CompileShader("triangle.hlsl", "main_vs", "vs_5_0"),
            FragmentShader = CompileShader("triangle.hlsl", "main_ps", "ps_5_0"),
            VertexDescriptor = new(new VertexLayoutDescriptor(VertexPositionColor.Attributes)),
        };
        _trianglePipeline = AddDisposable(_graphicsDevice.CreateRenderPipeline(trianglePipeline));
    }

    private static Image FromFile(string fileName)
    {
        return Image.FromFile(Path.Combine(AppContext.BaseDirectory, "assets", "textures", fileName));
    }

    private Mesh MeshFromFile(string fileName)
    {
        return Mesh.FromFile(_graphicsDevice, Path.Combine(AppContext.BaseDirectory, "assets", "meshes", fileName));
    }

    private static ReadOnlyMemory<byte> CompileShader(string fileName, string entryPoint, string profile)
    {
        string filePath = Path.Combine(AppContext.BaseDirectory, "assets", "shaders", "hlsl", fileName);
        string shaderSource = File.ReadAllText(filePath);

        return D3D11GraphicsDevice.CompileBytecode(shaderSource, entryPoint, profile);
    }


    private Texture CreateTextureCube(TextureFormat format, int width, int height, int levels = 0)
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

        Matrix4x4 projectionMatrix = Matrix4x4.CreatePerspectiveFieldOfView(
            MathHelper.ToRadians(_viewSettings.FieldOfView), (float)Size.Width / Size.Height, 1.0f, 1000.0f);

        Matrix4x4 viewRotationMatrix = EulerAngleXY(MathHelper.ToRadians(_viewSettings.Pitch), MathHelper.ToRadians(_viewSettings.Yaw));
        //viewRotationMatrix = Matrix4x4.Transpose(viewRotationMatrix);
        //Matrix4x4 sceneRotationMatrix = EulerAngleXY(MathHelper.ToRadians(scene.pitch), glm::radians(scene.yaw));

        //context.UpdateConstantBuffer(spmapCB.Buffer, spmapConstants);
        TransformCB transformData = new()
        {
            ViewProjectionMatrix = Matrix4x4.Multiply(viewRotationMatrix, projectionMatrix),
            SkyProjectionMatrix = Matrix4x4.Multiply(viewRotationMatrix, projectionMatrix),
            SceneRotationMatrix = Matrix4x4.Identity
        };
        _transformCB.SetData(context, transformData);
        context.SetConstantBuffer(0, _transformCB.Buffer);

        // Prepare framebuffer for rendering.
        //context.SetRenderTarget(_framebuffer);

        context.SetRenderTarget(null, Colors.CornflowerBlue);

        // Draw skybox.
        context.SetPipeline(_skyboxPipeline);
        context.SetVertexBuffer(0, _skybox.VertexBuffer, (uint)VertexMesh.SizeInBytes, 0);
        context.SetIndexBuffer(_skybox.IndexBuffer, 0, IndexType.Uint16);
        context.SetSRV(0, _envTexture);
        context.SetSampler(0, _defaultSampler);
        context.DrawIndexed(_skybox.IndexCount, 1);

        // Draw a full screen triangle for postprocessing/tone mapping.
        //context.SetRenderTarget(null);
        //m_context->IASetInputLayout(nullptr);
        //m_context->VSSetShader(m_tonemapProgram.vertexShader.Get(), nullptr, 0);
        //m_context->PSSetShader(m_tonemapProgram.pixelShader.Get(), nullptr, 0);
        //m_context->PSSetShaderResources(0, 1, m_resolveFramebuffer.srv.GetAddressOf());
        //m_context->PSSetSamplers(0, 1, m_computeSampler.GetAddressOf());
        //m_context->Draw(3, 0);

        // Draw triangle
        //context.SetVertexBuffer(0, _triangleVertexBuffer);
        //context.SetPipeline(_trianglePipeline);
        //context.Draw(3);

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

    private static Matrix4x4 EulerAngleXY(float angleX, float angleY)
    {
        float cosX = MathF.Cos(angleX);
        float sinX = MathF.Sin(angleX);
        float cosY = MathF.Cos(angleY);
        float sinY = MathF.Sin(angleY);

        return new Matrix4x4(
            cosY, -sinX * -sinY, cosX * -sinY, 0.0f,
            0.0f, cosX, sinX, 0.0f,
            sinY, -sinX * cosY, cosX * cosY, 0.0f,
            0.0f, 0.0f, 0.0f, 1.0f
            );
    }


    [StructLayout(LayoutKind.Sequential, Pack = 4)]
    public struct TransformCB
    {
        public Matrix4x4 ViewProjectionMatrix;
        public Matrix4x4 SkyProjectionMatrix;
        public Matrix4x4 SceneRotationMatrix;
    }

    [StructLayout(LayoutKind.Sequential, Pack = 4)]
    public struct SpecularMapFilterSettingsCB
    {
        public float Roughness;
        private readonly Vector3 _padding;
    }
}
