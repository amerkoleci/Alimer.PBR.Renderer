﻿// Copyright © Amer Koleci and Contributors.
// Licensed under the MIT License (MIT). See LICENSE in the repository root for more information.

using System.Diagnostics.CodeAnalysis;
using System.Drawing;
using System.Numerics;
using System.Runtime.CompilerServices;
using System.Runtime.InteropServices;
using Alimer.Bindings.SDL;
using Alimer.Graphics;
using Alimer.Graphics.D3D11;
using Vortice.Mathematics;
using static Alimer.Bindings.SDL.SDL;
using static Alimer.Bindings.SDL.SDL.SDL_EventType;
using static Alimer.Bindings.SDL.SDL.SDL_WindowFlags;

namespace Alimer.PBR.Renderer;

public sealed class Application : GraphicsObject
{
    const float OrbitSpeed = 1.0f;
    const float ZoomSpeed = 4.0f;

    private const int _eventsPerPeep = 64;
    private static readonly unsafe SDL_Event* _events = (SDL_Event*)NativeMemory.Alloc(_eventsPerPeep, (nuint)sizeof(SDL_Event));

    private readonly GraphicsDevice _graphicsDevice;
    private readonly SDL_Window _window;
    private bool _exitRequested;
    private ViewSettings _viewSettings;
    private float _scenePitch = 0.0f;
    private float _sceneYaw = 0.0f;
    private const int _numLights = 3;
    private readonly Light[] _lights = new Light[_numLights];
    private enum InputMode
    {
        None,
        RotatingView,
        RotatingScene,
    }
    private InputMode _mode;
    private int _prevCursorX;
    private int _prevCursorY;


    private readonly Texture _fboColorTexture;
    private readonly Texture _fboDepthStencilTexture;
    private readonly Texture _fboResolveColorTexture;

    private readonly Sampler _defaultSampler;
    private readonly Sampler _computeSampler;
    private readonly Texture _spBRDF_LUT;
    private readonly Sampler _spBRDF_Sampler;

    private readonly Pipeline _pbrPipeline;
    private readonly Pipeline _skyboxPipeline;
    private readonly Pipeline _tonemapPipeline;

    private readonly Mesh _pbrMesh;
    private readonly Texture _albedoTexture;
    private readonly Texture _normalTexture;
    private readonly Texture _metalnessTexture;
    private readonly Texture _roughnessTexture;

    private readonly Mesh _skybox;
    private readonly Texture _envTexture;
    private readonly Texture _irmapTexture;


    private readonly ConstantBuffer<TransformCB> _transformCB;
    private readonly ConstantBuffer<ShadingCB> _shadingCB;

    public Size Size { get; private set; }

    public Application(GraphicsBackend graphicsBackend, int width = 1024, int height = 800, int maxSamples = 16)
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

        _lights[0].Direction = Vector3.Normalize(new Vector3(-1.0f, 0.0f, 0.0f));
        _lights[0].Radiance = Vector3.One;

        _lights[1].Direction = Vector3.Normalize(new Vector3(1.0f, 0.0f, 0.0f));
        _lights[1].Radiance = Vector3.One;

        _lights[2].Direction = Vector3.Normalize(new Vector3(0.0f, -1.0f, 0.0f));
        _lights[2].Radiance = Vector3.One;

        TextureUsage colorTextureUsage = TextureUsage.RenderTarget;
        if (_graphicsDevice.Samples <= 1)
        {
            colorTextureUsage |= TextureUsage.ShaderRead;
        }

        TextureDescription colorTextureDesc = TextureDescription.Texture2D(TextureFormat.Rgba16Float, width, height, 1, colorTextureUsage, _graphicsDevice.Samples);
        TextureDescription depthStencilTextureDesc = TextureDescription.Texture2D(TextureFormat.Depth32FloatStencil8, width, height, 1, TextureUsage.RenderTarget, _graphicsDevice.Samples);

        _fboColorTexture = AddDisposable(_graphicsDevice.CreateTexture(colorTextureDesc));
        _fboDepthStencilTexture = AddDisposable(_graphicsDevice.CreateTexture(depthStencilTextureDesc));

        if (_graphicsDevice.Samples > 1)
        {
            TextureDescription resolveColorTextureDesc = TextureDescription.Texture2D(TextureFormat.Rgba16Float, width, height, 1,
                TextureUsage.ShaderRead | TextureUsage.RenderTarget, 1);
            _fboResolveColorTexture = AddDisposable(_graphicsDevice.CreateTexture(resolveColorTextureDesc));
        }
        else
        {
            _fboResolveColorTexture = _fboColorTexture;
        }

        SamplerDescription samplerDesc = new()
        {
            Label = "Default Sampler",
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
            Label = "Compute Sampler",
            AddressModeU = SamplerAddressMode.Repeat,
            AddressModeV = SamplerAddressMode.Repeat,
            AddressModeW = SamplerAddressMode.Repeat,
            MinFilter = SamplerMinMagFilter.Linear,
            MagFilter = SamplerMinMagFilter.Linear,
            MipFilter = SamplerMipFilter.Linear,
            MaxAnisotropy = 1
        };
        _computeSampler = AddDisposable(_graphicsDevice.CreateSampler(samplerComputeSampler));

        _transformCB = AddDisposable(new ConstantBuffer<TransformCB>(_graphicsDevice, "Transform"));
        _shadingCB = AddDisposable(new ConstantBuffer<ShadingCB>(_graphicsDevice, "Shading"));

        _pbrMesh = AddDisposable(MeshFromFile("cerberus.fbx"));
        _albedoTexture = AddDisposable(CreateTexture(ImageFromFile("cerberus_A.png"), TextureFormat.Rgba8UnormSrgb));
        _normalTexture = AddDisposable(CreateTexture(ImageFromFile("cerberus_N.png"), TextureFormat.Rgba8Unorm));
        _metalnessTexture = AddDisposable(CreateTexture(ImageFromFile("cerberus_M.png", 1), TextureFormat.R8Unorm));
        _roughnessTexture = AddDisposable(CreateTexture(ImageFromFile("cerberus_R.png", 1), TextureFormat.R8Unorm));

        RenderPipelineDescription pbrPipelineDesc = new()
        {
            VertexShader = CompileShader("pbr.hlsl", "main_vs", "vs_5_0"),
            FragmentShader = CompileShader("pbr.hlsl", "main_ps", "ps_5_0"),
            VertexDescriptor = new(new VertexLayoutDescriptor((uint)VertexMesh.SizeInBytes, VertexMesh.Attributes))
        };
        _pbrPipeline = AddDisposable(_graphicsDevice.CreateRenderPipeline(pbrPipelineDesc));

        _skybox = AddDisposable(MeshFromFile("skybox.obj"));
        RenderPipelineDescription skyboxPipelineDesc = new()
        {
            VertexShader = CompileShader("skybox.hlsl", "main_vs", "vs_5_0"),
            FragmentShader = CompileShader("skybox.hlsl", "main_ps", "ps_5_0"),
            VertexDescriptor = new(new VertexLayoutDescriptor((uint)VertexMesh.SizeInBytes, new VertexAttributeDescriptor(VertexFormat.Float32x3, 0))),
            DepthStencil = new DepthStencilDescriptor()
            {
                DepthWriteEnabled = false,
                DepthCompare = CompareFunction.Always
            }
        };
        _skyboxPipeline = AddDisposable(_graphicsDevice.CreateRenderPipeline(skyboxPipelineDesc));

        RenderPipelineDescription tonemapPipelineDesc = new()
        {
            Label = "Tonemap",
            VertexShader = CompileShader("tonemap.hlsl", "main_vs", "vs_5_0"),
            FragmentShader = CompileShader("tonemap.hlsl", "main_ps", "ps_5_0")
        };
        _tonemapPipeline = AddDisposable(_graphicsDevice.CreateRenderPipeline(tonemapPipelineDesc));

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
            using Texture envTextureEquirect = CreateTexture(ImageFromFile("environment.hdr"));

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
                Label = "Spmap",
                ComputeShader = CompileShader("spmap.hlsl", "main", "cs_5_0")
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

        // Compute diffuse irradiance cubemap.
        {
            ComputePipelineDescription irmapPipelineDesc = new()
            {
                Label = "Irmap",
                ComputeShader = CompileShader("irmap.hlsl", "main", "cs_5_0")
            };

            using Pipeline irmapPipeline = _graphicsDevice.CreateComputePipeline(irmapPipelineDesc);

            _irmapTexture = AddDisposable(CreateTextureCube(TextureFormat.Rgba16Float, 32, 32, 1));

            context.SetSRV(0, _envTexture);
            context.SetSampler(0, _computeSampler);
            context.SetUAV(0, _irmapTexture, 0);
            context.SetPipeline(irmapPipeline);
            context.Dispatch(_irmapTexture.Width / 32, _irmapTexture.Height / 32, 6);
        }

        // Compute Cook-Torrance BRDF 2D LUT for split-sum approximation.
        {
            ComputePipelineDescription spBRDFPipelineDesc = new()
            {
                Label = "spBRDF",
                ComputeShader = CompileShader("spbrdf.hlsl", "main", "cs_5_0")
            };

            using Pipeline spBRDFPipeline = _graphicsDevice.CreateComputePipeline(spBRDFPipelineDesc);

            TextureDescription BRDFTexturDesc = TextureDescription.Texture2D(TextureFormat.Rg16Float, 256, 256, 1, TextureUsage.ShaderReadWrite);
            _spBRDF_LUT = AddDisposable(_graphicsDevice.CreateTexture(BRDFTexturDesc));

            SamplerDescription BRDFSamplerDesc = new()
            {
                Label = "Default Sampler",
                AddressModeU = SamplerAddressMode.ClampToEdge,
                AddressModeV = SamplerAddressMode.ClampToEdge,
                AddressModeW = SamplerAddressMode.ClampToEdge,
                MinFilter = SamplerMinMagFilter.Linear,
                MagFilter = SamplerMinMagFilter.Linear,
                MipFilter = SamplerMipFilter.Linear,
                MaxAnisotropy = 1
            };
            _spBRDF_Sampler = AddDisposable(_graphicsDevice.CreateSampler(BRDFSamplerDesc));
            //createTextureUAV(m_spBRDF_LUT, 0);

            context.SetPipeline(spBRDFPipeline);
            context.SetUAV(0, _spBRDF_LUT, 0);
            context.Dispatch(_spBRDF_LUT.Width / 32, _spBRDF_LUT.Height / 32, 1);
        }
    }

    private static Image ImageFromFile(string fileName, int channels = 4)
    {
        return Image.FromFile(Path.Combine(AppContext.BaseDirectory, "assets", "textures", fileName), channels);
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

    private Texture CreateTexture(Image image, TextureFormat format = TextureFormat.Invalid)
    {
        if (format == TextureFormat.Invalid)
        {
            format = image.Format;
        }

        TextureDescription desc = TextureDescription.Texture2D(format, image.Width, image.Height, 1, TextureUsage.ShaderRead | TextureUsage.ShaderWrite);
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

        // Matrix4x4 projectionMatrix = Matrix4x4.CreatePerspectiveFieldOfView(MathHelper.ToRadians(_viewSettings.FieldOfView), (float)Size.Width / Size.Height, 1.0f, 1000.0f);
        Matrix4x4 projectionMatrix = perspectiveFovRH_NO(_viewSettings.FieldOfView, (float)Size.Width, Size.Height, 1.0f, 1000.0f);

        Matrix4x4 viewRotationMatrix = EulerAngleXY(MathHelper.ToRadians(_viewSettings.Pitch), MathHelper.ToRadians(_viewSettings.Yaw));
        Matrix4x4 sceneRotationMatrix = EulerAngleXY(MathHelper.ToRadians(_scenePitch), MathHelper.ToRadians(_sceneYaw));
        Matrix4x4 viewMatrix = viewRotationMatrix * Matrix4x4.CreateTranslation(0.0f, 0.0f, -_viewSettings.Distance);
        Vector3 eyePosition = Vector3.Zero;
        if (Matrix4x4.Invert(viewMatrix, out Matrix4x4 inverseParentMatrix))
        {
            eyePosition = inverseParentMatrix.Translation;
        }

        //context.UpdateConstantBuffer(spmapCB.Buffer, spmapConstants);
        TransformCB transformData = new()
        {
            ViewProjectionMatrix = Matrix4x4.Multiply(viewMatrix, projectionMatrix),
            SkyProjectionMatrix = Matrix4x4.Multiply(viewRotationMatrix, projectionMatrix),
            SceneRotationMatrix = sceneRotationMatrix
        };
        _transformCB.SetData(context, transformData);

        // Update shading constant buffer (for pixel shader).
        {
            ShadingCB shadingConstants = new();
            shadingConstants.EyePosition = new Vector4(eyePosition, 0.0f);
            for (int i = 0; i < _numLights; ++i)
            {
                ref Light light = ref _lights[i];
                Vector4 radiance = Vector4.Zero;

                if (light.Enabled)
                {
                    radiance = new Vector4(light.Radiance, 0.0f);
                }

                switch (i)
                {
                    case 0:
                        shadingConstants.Light1.Direction = new Vector4(light.Direction, 0.0f);
                        shadingConstants.Light1.Radiance = radiance;
                        break;

                    case 1:
                        shadingConstants.Light2.Direction = new Vector4(light.Direction, 0.0f);
                        shadingConstants.Light2.Radiance = radiance;
                        break;

                    case 2:
                        shadingConstants.Light3.Direction = new Vector4(light.Direction, 0.0f);
                        shadingConstants.Light3.Radiance = radiance;
                        break;
                }
            }

            _shadingCB.SetData(context, shadingConstants);
        }


        context.SetConstantBuffer(0, _transformCB.Buffer);
        context.SetConstantBuffer(1, _shadingCB.Buffer);

        // Prepare framebuffer for rendering.
        RenderPassColorAttachment colorAttachment = new(_fboColorTexture)
        {
            ResolveTexture = (_fboColorTexture != _fboResolveColorTexture) ? _fboResolveColorTexture : _fboColorTexture,
        };
        RenderPassDepthStencilAttachment depthStencilAttachment = new(_fboDepthStencilTexture);
        RenderPassDescriptor renderPass = new(depthStencilAttachment, colorAttachment)
        {
            Label = "Main Pass"
        };

        using (context.PushScopedPassPass(renderPass))
        {
            // Draw skybox.
            context.SetPipeline(_skyboxPipeline);
            context.SetVertexBuffer(0, _skybox.VertexBuffer);
            context.SetIndexBuffer(_skybox.IndexBuffer, 0, _skybox.IndexType);
            context.SetSRV(0, _envTexture);
            context.SetSampler(0, _defaultSampler);
            context.DrawIndexed(_skybox.IndexCount, 1);

            // Draw PBR model.
            context.SetPipeline(_pbrPipeline);
            context.SetVertexBuffer(0, _pbrMesh.VertexBuffer);
            context.SetIndexBuffer(_pbrMesh.IndexBuffer, 0, _pbrMesh.IndexType);

            context.SetSRV(0, _albedoTexture);
            context.SetSRV(1, _normalTexture);
            context.SetSRV(2, _metalnessTexture);
            context.SetSRV(3, _roughnessTexture);
            context.SetSRV(4, _envTexture);
            context.SetSRV(5, _irmapTexture);
            context.SetSRV(6, _spBRDF_LUT);

            context.SetSampler(0, _defaultSampler);
            context.SetSampler(1, _spBRDF_Sampler);
            context.DrawIndexed(_pbrMesh.IndexCount, 1);
        }

        // Draw a full screen triangle for postprocessing/tone mapping.
        RenderPassDescriptor backBufferRenderPass = new(new RenderPassColorAttachment(_graphicsDevice.ColorTexture))
        {
            Label = "BackBuffer"
        };

        using (context.PushScopedPassPass(backBufferRenderPass))
        {
            context.SetPipeline(_tonemapPipeline);
            context.SetSRV(0, _fboResolveColorTexture);
            context.SetSampler(0, _computeSampler);
            context.Draw(3);
        }

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

            case SDL_MOUSEWHEEL:
                _viewSettings.Distance += ZoomSpeed * -evt.wheel.y;
                break;

            case SDL_MOUSEBUTTONDOWN:
                if (_mode == InputMode.None)
                {
                    if (evt.button.button == 1)
                        _mode = InputMode.RotatingView;
                    if (evt.button.button == 3)
                        _mode = InputMode.RotatingScene;

                    _prevCursorX = evt.button.x;
                    _prevCursorY = evt.button.y;
                    SDL_ShowCursor(0);
                }
                break;

            case SDL_MOUSEBUTTONUP:
                if (evt.button.button == 1)
                    _mode = InputMode.None;
                if (evt.button.button == 3)
                    _mode = InputMode.None;
                SDL_ShowCursor(1);
                break;

            case SDL_MOUSEMOTION:
                if (_mode != InputMode.None)
                {
                    int dx = evt.button.x - _prevCursorX;
                    int dy = evt.button.y - _prevCursorY;

                    switch (_mode)
                    {
                        case InputMode.RotatingScene:
                            _sceneYaw += OrbitSpeed * dx;
                            _scenePitch += OrbitSpeed * dy;
                            break;
                        case InputMode.RotatingView:
                            _viewSettings.Yaw += OrbitSpeed * dx;
                            _viewSettings.Pitch += OrbitSpeed * dy;
                            break;
                    }

                    _prevCursorX = evt.button.x;
                    _prevCursorY = evt.button.y;
                }
                break;

            case SDL_KEYDOWN:
                if (evt.key.keysym.sym == SDL_Keycode.SDLK_F1)
                {
                    _lights[0].Enabled = !_lights[0].Enabled;
                }
                else if (evt.key.keysym.sym == SDL_Keycode.SDLK_F2)
                {
                    _lights[1].Enabled = !_lights[1].Enabled;
                }
                else if (evt.key.keysym.sym == SDL_Keycode.SDLK_F3)
                {
                    _lights[2].Enabled = !_lights[2].Enabled;
                }
                break;

            case SDL_KEYUP:
                break;

        }
    }

    private void HandleWindowEvent(in SDL_Event evt)
    {
    }

    private static Matrix4x4 perspectiveFovRH_NO(float fov, float width, float height, float zNear, float zFar)
    {
        //assert(width > static_cast<T>(0));
        //assert(height > static_cast<T>(0));
        //assert(fov > static_cast<T>(0));


        float rad = fov;
        float h = MathF.Cos(0.5f * rad) / MathF.Sin(0.5f * rad);
        float w = h * height / width; ///todo max(width , Height) / min(width , Height)?

        Matrix4x4 result = default;
        result.M11 = w;
        result.M22 = h;
        result.M33 = -(zFar + zNear) / (zFar - zNear);
        result.M34 = -1.0f;
        result.M43 = -(2.0f * zFar * zNear) / (zFar - zNear);

        return result;
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
    public struct Light
    {
        public Vector3 Direction;
        public Vector3 Radiance;
        public bool Enabled;
    }

    [StructLayout(LayoutKind.Sequential, Pack = 4)]
    public struct TransformCB
    {
        public Matrix4x4 ViewProjectionMatrix;
        public Matrix4x4 SkyProjectionMatrix;
        public Matrix4x4 SceneRotationMatrix;
    }

    [StructLayout(LayoutKind.Sequential, Pack = 4)]
    public struct LightCB
    {
        public Vector4 Direction;
        public Vector4 Radiance;
    }

    [StructLayout(LayoutKind.Sequential, Pack = 4)]
    public struct ShadingCB
    {
        public LightCB Light1;
        public LightCB Light2;
        public LightCB Light3;

        [UnscopedRef]
        public ref LightCB this[int index]
        {
            [MethodImpl(MethodImplOptions.AggressiveInlining)]
            get
            {
                return ref AsSpan()[index];
            }
        }

        [UnscopedRef]
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public Span<LightCB> AsSpan()
        {
            return MemoryMarshal.CreateSpan(ref Light1, 3);
        }

        public Vector4 EyePosition;
    }

    [StructLayout(LayoutKind.Sequential, Pack = 4)]
    public struct SpecularMapFilterSettingsCB
    {
        public float Roughness;
        private readonly Vector3 _padding;
    }
}
