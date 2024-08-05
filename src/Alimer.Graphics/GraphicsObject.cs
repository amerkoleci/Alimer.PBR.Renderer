// Copyright (c) Amer Koleci and Contributors.
// Licensed under the MIT License (MIT). See LICENSE in the repository root for more information.

using System.Runtime.CompilerServices;
using System.Reflection;
using XenoAtom.Interop;

namespace Alimer.Graphics;

public abstract class GraphicsObject : IDisposable
{
    private volatile uint _isDisposed;
    protected string _label;

    /// <summary>
    /// Initializes a new instance of the <see cref="GraphicsObject" /> class.
    /// </summary>
    /// <param name="label">The label of the object or <c>null</c> to use <see cref="MemberInfo.Name" />.</param>
    protected GraphicsObject(string? label = default)
    {
        _isDisposed = 0;
        _label = label ?? GetType().Name;
    }

    /// <summary>
    /// Initializes a new instance of the <see cref="GraphicsObject" /> class.
    /// </summary>
    /// <param name="label">The label of the object or <c>null</c> to use <see cref="MemberInfo.Name" />.</param>
    protected GraphicsObject(ReadOnlyMemoryUtf8 label)
    {
        _isDisposed = 0;
        _label = label.IsNull ? GetType().Name : label.ToString()!;
    }

    /// <summary>
    /// Gets <c>true</c> if the object has been disposed; otherwise, <c>false</c>.
    /// </summary>
    public bool IsDisposed => _isDisposed != 0;

    /// <summary>
    /// Gets or set the label that identifies the resource.
    /// </summary>
    public string Label
    {
        get => _label;
        set
        {
            _label = value ?? GetType().Name;
            OnLabelChanged(_label);
        }
    }

    /// <inheritdoc />
    public void Dispose()
    {
        if (Interlocked.Exchange(ref _isDisposed, 1) == 0)
        {
            Dispose(disposing: true);
            GC.SuppressFinalize(this);
        }
    }

    protected virtual void OnLabelChanged(string newLabel)
    {
    }

    /// <inheritdoc cref="Dispose()" />
    /// <param name="disposing"><c>true</c> if the method was called from <see cref="Dispose()" />; otherwise, <c>false</c>.</param>
    protected abstract void Dispose(bool disposing);

    /// <inheritdoc />
    public override string ToString() => _label;

    /// <summary>Throws an exception if the object has been disposed.</summary>
    /// <exception cref="ObjectDisposedException">The object has been disposed.</exception>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    protected void ThrowIfDisposed()
    {
        if (_isDisposed != 0)
        {
            throw new ObjectDisposedException(_label);
        }
    }

    /// <summary>Marks the object as being disposed.</summary>
    protected void MarkDisposed()
    {
        _ = Interlocked.Exchange(ref _isDisposed, 1);
    }
}
