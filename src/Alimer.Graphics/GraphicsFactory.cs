// Copyright (c) Amer Koleci and Contributors.
// Licensed under the MIT License (MIT). See LICENSE in the repository root for more information.

using CommunityToolkit.Diagnostics;

namespace Alimer.Graphics;

/// <summary>
/// Defines a graphics factory for enumerating adapters and surface creation
/// </summary>
public abstract class GraphicsFactory : GraphicsObject
{
    protected GraphicsFactory(GraphicsBackend backend, in GraphicsFactoryDescription description)
        : base(description.Label)
    {
        Backend = backend;
        ValidationMode = description.ValidationMode;
    }

    /// <summary>
    /// Gets a value identifying the specific graphics backend used by this factory.
    /// </summary>
    public GraphicsBackend Backend { get; }

    /// <summary>
    /// Gets the supported and enabled validation mode.
    /// </summary>
    public ValidationMode ValidationMode { get; }

    public abstract GraphicsDevice CreateDevice(in GraphicsDeviceDescription description);

    public static bool IsBackendSupport(GraphicsBackend backend)
    {
        Guard.IsTrue(backend != GraphicsBackend.Count, nameof(backend), "Invalid backend");

        switch (backend)
        {
#if !EXCLUDE_VULKAN_BACKEND
            case GraphicsBackend.Vulkan:
                return Vulkan.VulkanGraphicsFactory.IsSupported();
#endif

#if !EXCLUDE_D3D12_BACKEND
            case GraphicsBackend.D3D12:
                return D3D12.D3D12GraphicsFactory.IsSupported();
#endif

#if !EXCLUDE_D3D11_BACKEND
            case GraphicsBackend.D3D11:
                return D3D11.D3D11GraphicsFactory.IsSupported();
#endif

            default:
                return false;
        }
    }

    public static GraphicsFactory Create(in GraphicsFactoryDescription description)
    {
        GraphicsBackend backend = description.PreferredBackend;
        if (backend == GraphicsBackend.Count)
        {
            if (IsBackendSupport(GraphicsBackend.D3D12))
            {
                backend = GraphicsBackend.D3D12;
            }
            else if (IsBackendSupport(GraphicsBackend.D3D11))
            {
                backend = GraphicsBackend.D3D11;
            }
            else if (IsBackendSupport(GraphicsBackend.Vulkan))
            {
                backend = GraphicsBackend.Vulkan;
            }
        }

        GraphicsFactory? instance = default;
        switch (backend)
        {
#if !EXCLUDE_VULKAN_BACKEND
            case GraphicsBackend.Vulkan:
                if (Vulkan.VulkanGraphicsFactory.IsSupported())
                {
                    instance = new Vulkan.VulkanGraphicsFactory(in description);
                }
                break;
#endif

#if !EXCLUDE_D3D12_BACKEND 
            case GraphicsBackend.D3D12:
                if (D3D12.D3D12GraphicsFactory.IsSupported())
                {
                    instance = new D3D12.D3D12GraphicsFactory(in description);
                }
                break;
#endif

#if !EXCLUDE_D3D11_BACKEND
            case GraphicsBackend.D3D11:
                if (D3D11.D3D11GraphicsFactory.IsSupported())
                {
                    instance = new D3D11.D3D11GraphicsFactory(in description);
                }
                break;
#endif

            default:
                break;
        }

        if (instance == null)
        {
            throw new GraphicsException($"{backend} is not supported");
        }

        return instance!;
    }
}
