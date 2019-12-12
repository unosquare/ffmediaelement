#pragma warning disable CA1812
namespace Unosquare.FFME.Rendering
{
    using Common;
    using Container;
    using Diagnostics;
    using Engine;
    using Platform;
    using Primitives;
    using SharpDX.Direct3D9;
    using SharpDX.Mathematics.Interop;
    using System;
    using System.Collections.Concurrent;
    using System.Runtime.InteropServices;
    using System.Windows;
    using System.Windows.Interop;
    using System.Windows.Media;

    /// <summary>
    /// A video renderer based on Direct3D.
    /// https://stackoverflow.com/questions/19480373/sharpdx-render-in-wpf
    /// https://stackoverflow.com/questions/45802931/show-a-d3dimage-with-sharpdx
    /// https://www.codeproject.com/Articles/28526/Introduction-to-D3DImage
    /// https://docs.microsoft.com/en-us/dotnet/framework/wpf/advanced/walkthrough-creating-direct3d9-content-for-hosting-in-wpf.
    /// Microsoft.Toolkit.Wpf.UI.Controls
    /// </summary>
    /// <seealso cref="IMediaRenderer" />
    /// <seealso cref="ILoggingSource" />
    internal sealed class D3DVideoRenderer : VideoRendererBase, IDisposable
    {
        private const int DefaultDisplayAdapter = 0;
        private const Format SurfaceFormat = Format.A8R8G8B8;

        private readonly AtomicBoolean IsDisposed = new AtomicBoolean(false);

        private readonly GraphicsBuffer Graphics = new GraphicsBuffer();
        private DeviceEx m_Device;
        private D3DImage TargetImage;

        /// <summary>
        /// Initializes static members of the <see cref="D3DVideoRenderer"/> class.
        /// </summary>
        static D3DVideoRenderer()
        {
            IsAvailable = true;

            try
            {
                Engine = new Direct3DEx();
                var capabilities = Engine.GetDeviceCaps(DefaultDisplayAdapter, DeviceType.Hardware);
                var vertexMode = capabilities.DeviceCaps.HasFlag(DeviceCaps.HWTransformAndLight)
                    ? CreateFlags.HardwareVertexProcessing
                    : CreateFlags.SoftwareVertexProcessing;

                DeviceCreationFlags = CreateFlags.Multithreaded | CreateFlags.FpuPreserve | vertexMode;
            }
            catch
            {
                IsAvailable = false;
            }
        }

        /// <summary>
        /// Initializes a new instance of the <see cref="D3DVideoRenderer"/> class.
        /// </summary>
        /// <param name="mediaCore">The media core.</param>
        public D3DVideoRenderer(MediaEngine mediaCore)
            : base(mediaCore)
        {
            VideoDispatcher?.InvokeAsync(() =>
            {
                CompositionTarget.Rendering += (s, e) =>
                {
                    var videoView = MediaElement?.VideoView;

                    if (TargetImage == null)
                        TargetImage = new D3DImage();

                    var img = TargetImage;
                    if (img != null)
                    {
                        Graphics.ReadInto(img);
                    }

                    if (videoView != null)
                        videoView.Source = img;
                };
            });
        }

        /// <summary>
        /// Gets a value indicating whether the D3DEx Api is available.
        /// </summary>
        public static bool IsAvailable { get; }

        /// <summary>
        /// Gets the D3D engine.
        /// </summary>
        private static Direct3DEx Engine { get; }

        /// <summary>
        /// Gets the D3D device creation flags.
        /// </summary>
        private static CreateFlags DeviceCreationFlags { get; }

        /// <summary>
        /// Gets the device presentation parameters.
        /// </summary>
        private static PresentParameters DeviceParameters { get; } = new PresentParameters
        {
            Windowed = true,
            SwapEffect = SwapEffect.Discard,
            PresentationInterval = PresentInterval.Default,
            BackBufferFormat = Format.Unknown,
            BackBufferWidth = 1,
            BackBufferHeight = 1,
        };

        /// <summary>
        /// Gets a valid D3D device.
        /// </summary>
        private DeviceEx Device
        {
            get
            {
                EnsureDeviceAvailable();
                return m_Device;
            }
        }

        /// <inheritdoc />
        public override unsafe void Render(MediaBlock mediaBlock, TimeSpan clockPosition)
        {
            var block = BeginRenderingCycle(mediaBlock);
            if (block == null) return;

            IDisposable blockLock = null;

            try
            {
                if (!block.TryAcquireWriterLock(out blockLock))
                    return;

                RaiseRenderingEvent(block, clockPosition);
                Graphics.EnqueueWrite(block, Device);

                // UpdateTargetImage();
            }
            catch (Exception ex)
            {
                this.LogError(Aspects.VideoRenderer, $"{nameof(VideoRenderer)}.{nameof(Render)} bitmap failed.", ex);
            }
            finally
            {
                blockLock?.Dispose();
                FinishRenderingCycle(block, clockPosition);
            }
        }

        /// <inheritdoc />
        public void Dispose() => Dispose(true);

        /// <summary>
        /// Helper method that creates a D3D device.
        /// </summary>
        /// <returns>A D3D device.</returns>
        private static DeviceEx CreateDevice() =>
            new DeviceEx(Engine, DefaultDisplayAdapter, DeviceType.Hardware, IntPtr.Zero, DeviceCreationFlags, DeviceParameters)
            {
                GPUThreadPriority = -7,
                MaximumFrameLatency = 1,
            };

        /// <summary>
        /// Ensures the device is available.
        /// </summary>
        private void EnsureDeviceAvailable()
        {
            var needsCreation = m_Device != null
                ? m_Device.CheckDeviceState(IntPtr.Zero) != DeviceState.Ok
                : true;

            if (!needsCreation)
                return;

            m_Device?.Dispose();
            m_Device = CreateDevice();
        }

        /// <summary>
        /// Updates the target image on the Video dispatcher thread.
        /// </summary>
        private void UpdateTargetImage()
        {
            VideoDispatcher?.InvokeAsync(() =>
            {
                var videoView = MediaElement?.VideoView;

                if (TargetImage == null)
                    TargetImage = new D3DImage();

                var img = TargetImage;
                if (img != null)
                {
                    Graphics.ReadInto(img);
                }

                if (videoView != null)
                    videoView.Source = img;
            });
        }

        /// <summary>
        /// Raises the rendering event with the block data.
        /// </summary>
        /// <param name="block">The block.</param>
        /// <param name="clockPosition">The clock position.</param>
        private void RaiseRenderingEvent(VideoBlock block, TimeSpan clockPosition)
        {
            var bitmapData = new BitmapDataBuffer(block, DpiX, DpiY);
            MediaElement?.RaiseRenderingVideoEvent(block, bitmapData, clockPosition);
        }

        /// <summary>
        /// Releases unmanaged and - optionally - managed resources.
        /// </summary>
        /// <param name="alsoManaged"><c>true</c> to release both managed and unmanaged resources; <c>false</c> to release only unmanaged resources.</param>
        private void Dispose(bool alsoManaged)
        {
            if (IsDisposed.Value) return;
            IsDisposed.Value = true;

            if (alsoManaged)
            {
                Graphics.Dispose();
                m_Device?.Dispose();
            }

            TargetImage = null;
        }

        /// <summary>
        /// Provides access to unmanaged methods.
        /// </summary>
        private static class NativeMethods
        {
            [DllImport("user32.dll", SetLastError = false)]
            public static extern IntPtr GetDesktopWindow();

            public static void LoadSurfaceFromMemory(
                Surface surface,
                IntPtr data,
                Filter filter,
                int colorKey,
                Format sourceFormat,
                int sourcePitch,
                RawRectangle sourceRectangle,
                PaletteEntry[] sourcePalette,
                PaletteEntry[] destinationPalette)
            {
                unsafe
                {
                    LoadSurfaceFromMemory(
                        surface,
                        destinationPalette,
                        IntPtr.Zero,
                        data,
                        sourceFormat,
                        sourcePitch,
                        sourcePalette,
                        new IntPtr(&sourceRectangle),
                        filter,
                        colorKey);
                }
            }

            private static void LoadSurfaceFromMemory(Surface destSurfaceRef, PaletteEntry[] destPaletteRef, IntPtr destRectRef, IntPtr srcMemoryRef, Format srcFormat, int srcPitch, PaletteEntry[] srcPaletteRef, IntPtr srcRectRef, Filter filter, int colorKey)
            {
                unsafe
                {
                    SharpDX.Result __result__;
                    fixed (void* destPaletteRef_ = destPaletteRef)
                    fixed (void* srcPaletteRef_ = srcPaletteRef)
                        __result__ =
                        D3DXLoadSurfaceFromMemory_((void*)((destSurfaceRef == null) ? IntPtr.Zero : destSurfaceRef.NativePointer), destPaletteRef_, (void*)destRectRef, (void*)srcMemoryRef, unchecked((int)srcFormat), srcPitch, srcPaletteRef_, (void*)srcRectRef, unchecked((int)filter), colorKey);
                    __result__.CheckError();
                }
            }

            [DllImport("d3dx9_43.dll", EntryPoint = "D3DXLoadSurfaceFromMemory", CallingConvention = CallingConvention.StdCall)]
            private static extern unsafe int D3DXLoadSurfaceFromMemory_(void* arg0, void* arg1, void* arg2, void* arg3, int arg4, int arg5, void* arg6, void* arg7, int arg8, int arg9);
        }

        private sealed class GraphicsBuffer : IDisposable
        {
            private readonly ConcurrentQueue<Surface> ReadQueue = new ConcurrentQueue<Surface>();
            private readonly ConcurrentQueue<Surface> WriteQueue = new ConcurrentQueue<Surface>();

            private readonly AtomicBoolean IsDsiposing = new AtomicBoolean();

            public void EnqueueWrite(VideoBlock block, DeviceEx device)
            {
                if (IsDsiposing.Value)
                    return;

                Surface surface = null;

                if (WriteQueue.Count > 4)
                    WriteQueue.TryDequeue(out surface);

                if (surface == null || surface.Description.Width != block.PixelWidth || surface.Description.Height != block.PixelHeight)
                {
                    surface?.Dispose();

                    // Create the surface that will act as the render target.
                    surface = Surface.CreateRenderTargetEx(
                        device, block.PixelWidth, block.PixelHeight, SurfaceFormat, MultisampleType.None, 0, true, Usage.None);
                }

                var rect = new RawRectangle(0, 0, block.PixelWidth, block.PixelHeight);
                NativeMethods.LoadSurfaceFromMemory(
                    surface, block.Buffer, Filter.None, 0, SurfaceFormat, block.PictureBufferStride, rect, null, null);

                ReadQueue.Enqueue(surface);
            }

            public bool ReadInto(D3DImage image)
            {
                if (IsDsiposing.Value)
                    return false;

                if (image == null)
                    return false;

                if (!ReadQueue.TryDequeue(out var surface))
                    return false;

                var result = true;
                var surfacePointer = surface == null || surface.IsDisposed ? IntPtr.Zero : surface.NativePointer;
                var rect = surfacePointer == IntPtr.Zero
                    ? new Int32Rect(0, 0, image.PixelWidth, image.PixelHeight)
                    : new Int32Rect(0, 0, surface.Description.Width, surface.Description.Height);

                if (image.IsFrontBufferAvailable)
                {
                    image.Lock();
                    image.SetBackBuffer(D3DResourceType.IDirect3DSurface9, surfacePointer);
                    image.AddDirtyRect(rect);
                    image.Unlock();
                }

                WriteQueue.Enqueue(surface);
                return result;
            }

            public void Dispose()
            {
                if (IsDsiposing.Value)
                    return;

                IsDsiposing.Value = true;

                while (ReadQueue.Count > 0)
                {
                    if (ReadQueue.TryDequeue(out var surface))
                        surface.Dispose();
                }

                while (WriteQueue.Count > 0)
                {
                    if (WriteQueue.TryDequeue(out var surface))
                        surface.Dispose();
                }
            }
        }
    }
}
#pragma warning restore CA1812