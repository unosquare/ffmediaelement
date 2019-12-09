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
    using System.Runtime.InteropServices;
    using System.Windows;
    using System.Windows.Interop;
    using System.Windows.Media;
    using System.Windows.Threading;
    using RenderingEventArgs = System.Windows.Media.RenderingEventArgs;

    /// <summary>
    /// A video renderer based on Direct3D.
    /// https://stackoverflow.com/questions/19480373/sharpdx-render-in-wpf
    /// https://stackoverflow.com/questions/45802931/show-a-d3dimage-with-sharpdx
    /// https://www.codeproject.com/Articles/28526/Introduction-to-D3DImage
    /// https://docs.microsoft.com/en-us/dotnet/framework/wpf/advanced/walkthrough-creating-direct3d9-content-for-hosting-in-wpf.
    /// </summary>
    /// <seealso cref="IMediaRenderer" />
    /// <seealso cref="ILoggingSource" />
    internal sealed class D3DVideoRenderer : VideoRendererBase, IDisposable
    {
        private const int DefaultDisplayAdapter = 0;
        private const Format SurfaceFormat = Format.A8R8G8B8;

        private readonly object DeviceLock = new object();
        private readonly AtomicBoolean IsDisposed = new AtomicBoolean(false);

        private long LastRenderTime;
        private DeviceEx m_Device;
        private Surface TargetSurface;
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
                CompositionTarget.Rendering += OnCompositionTargetRendering;
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
        private Device Device
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

            var rect = new RawRectangle(0, 0, block.PixelWidth, block.PixelHeight);

            try
            {
                EnsurePresentable(block);
                if (!block.TryAcquireReaderLock(out var readerLock))
                    return;

                var bitmapData = new BitmapDataBuffer(block, DpiX, DpiY);
                MediaElement?.RaiseRenderingVideoEvent(block, bitmapData, clockPosition);

                NativeMethods.LoadSurfaceFromMemory(
                    TargetSurface, block.Buffer, Filter.None, 0, SurfaceFormat, block.PictureBufferStride, rect, null, null);

                readerLock.Dispose();

                /*
                VideoDispatcher?.Invoke(() =>
                {
                    // You must call Unlock even in the case where TryLock indicates failure (i.e., returns false)
                    if (TargetImage.TryLock(WpfLockTimeout))
                    {
                        TargetImage.SetBackBuffer(D3DResourceType.IDirect3DSurface9, TargetSurface.NativePointer);
                        TargetImage.AddDirtyRect(new Int32Rect(0, 0, block.PixelWidth, block.PixelHeight));
                    }

                    TargetImage.Unlock();
                }, DispatcherPriority.Background);
                */
            }
            catch (Exception ex)
            {
                this.LogError(Aspects.VideoRenderer, $"{nameof(VideoRenderer)}.{nameof(Render)} bitmap failed.", ex);
            }
            finally
            {
                FinishRenderingCycle(block, clockPosition);
            }
        }

        /// <inheritdoc />
        public void Dispose() => Dispose(true);

        /// <summary>
        /// Helper method that creates a D3D device.
        /// </summary>
        /// <returns>A D3D device.</returns>
        private static DeviceEx CreateDevice()
        {
            var windowHandle = NativeMethods.GetDesktopWindow();
            return new DeviceEx(Engine, DefaultDisplayAdapter, DeviceType.Hardware, windowHandle, DeviceCreationFlags, DeviceParameters);
        }

        /// <summary>
        /// Ensures the device is available.
        /// </summary>
        private void EnsureDeviceAvailable()
        {
            lock (DeviceLock)
            {
                var needsCreation = m_Device != null
                    ? m_Device.CheckDeviceState(IntPtr.Zero) != DeviceState.Ok
                    : true;

                if (!needsCreation)
                    return;

                m_Device?.Dispose();
                m_Device = CreateDevice();
            }
        }

        /// <summary>
        /// Ensures the video image is presentable.
        /// </summary>
        /// <param name="block">The block.</param>
        /// <returns>True if is ready to be presented.</returns>
        private bool EnsurePresentable(VideoBlock block)
        {
            if (TargetImage != null && TargetSurface != null && TargetSurface.Description.Width == block.PixelWidth && TargetSurface.Description.Height == block.PixelHeight)
                return false;

            var videoView = MediaElement?.VideoView;
            if (videoView != null && videoView.ElementDispatcher != null)
            {
                videoView.ElementDispatcher.Invoke(() =>
                {
                    if (TargetImage == null)
                        TargetImage = new D3DImage();

                    TargetImage.Lock();
                    TargetImage.SetBackBuffer(D3DResourceType.IDirect3DSurface9, IntPtr.Zero);
                    TargetImage.Unlock();

                    videoView.Source = TargetImage;
                });
            }

            TargetSurface?.Dispose();

            lock (DeviceLock)
            {
                // Create the surface that will act as the render target.
                TargetSurface = Surface.CreateRenderTarget(
                    Device, block.PixelWidth, block.PixelHeight, SurfaceFormat, MultisampleType.None, 0, false);
            }

            return true;
        }

        private void OnCompositionTargetRendering(object sender, EventArgs e)
        {
            var args = e as RenderingEventArgs;
            var currentRenderTime = args.RenderingTime.Ticks;
            var img = TargetImage;
            var surface = TargetSurface;

            // It's possible for Rendering to call back twice in the same frame
            // so only render when we haven't already rendered in this frame.
            if (surface == null || img == null || !img.IsFrontBufferAvailable || LastRenderTime == currentRenderTime)
                return;

            while (IsRenderingInProgress)
            {
                // wait for rendering to finish
            }

            // Repeatedly calling SetBackBuffer with the same IntPtr has no performance penalty.
            // You must call Unlock even in the case where TryLock indicates failure (i.e., returns false)
            img.Lock();
            img.SetBackBuffer(D3DResourceType.IDirect3DSurface9, surface.NativePointer);
            img.AddDirtyRect(new Int32Rect(0, 0, img.PixelWidth, img.PixelHeight));
            img.Unlock();

            LastRenderTime = currentRenderTime;
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
                TargetSurface?.Dispose();
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
    }
}
#pragma warning restore CA1812