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

    /// <summary>
    /// A video renderer based on Direct3D.
    /// https://stackoverflow.com/questions/19480373/sharpdx-render-in-wpf
    /// https://stackoverflow.com/questions/45802931/show-a-d3dimage-with-sharpdx
    /// https://www.codeproject.com/Articles/28526/Introduction-to-D3DImage
    /// https://docs.microsoft.com/en-us/dotnet/framework/wpf/advanced/walkthrough-creating-direct3d9-content-for-hosting-in-wpf.
    /// Microsoft.Toolkit.Wpf.UI.Controls.
    /// </summary>
    /// <seealso cref="IMediaRenderer" />
    /// <seealso cref="ILoggingSource" />
    internal sealed class D3DVideoRenderer : VideoRendererBase, IDisposable
    {
        private readonly AtomicBoolean IsDisposed = new AtomicBoolean(false);
        private readonly GraphicsBuffer Graphics = new GraphicsBuffer(true);
        private D3DImage TargetImage;

        /// <summary>
        /// Initializes a new instance of the <see cref="D3DVideoRenderer"/> class.
        /// </summary>
        /// <param name="mediaCore">The media core.</param>
        public D3DVideoRenderer(MediaEngine mediaCore)
            : base(mediaCore)
        {
            // placeholder
        }

        public static bool IsAvailable => GraphicsBuffer.IsD3DAvailable;

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
                Graphics.Write(block);
                VideoDispatcher?.Invoke(() => UpdateTargetImage(clockPosition));
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
        public override void OnClose()
        {
            base.OnClose();
            Dispose();
        }

        /// <inheritdoc />
        public void Dispose() => Dispose(true);

        /// <summary>
        /// Updates the target image on the Video dispatcher thread.
        /// </summary>
        private void UpdateTargetImage(TimeSpan clockPosition)
        {
            var videoView = MediaElement?.VideoView;

            if (TargetImage == null)
                TargetImage = new D3DImage();

            var img = TargetImage;
            if (img != null)
            {
                Graphics.Render(img, clockPosition);
            }

            if (videoView != null)
                videoView.Source = img;
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
            private const int DefaultDisplayAdapter = 0;
            private const Format SurfaceFormat = Format.A8R8G8B8;

            private readonly object SyncLock = new object();

            private bool IsDsiposing;
            private TimeSpan LastRenderTime;
            private Surface FrontBuffer;
            private Surface BackBuffer;
            private DeviceEx m_Device;

            static GraphicsBuffer()
            {
                IsD3DAvailable = true;

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
                    IsD3DAvailable = false;
                }
            }

            public GraphicsBuffer(bool useBackBuffer)
            {
                HasBackBuffer = useBackBuffer;
            }

            /// <summary>
            /// Gets a value indicating whether the D3DEx Api is available.
            /// </summary>
            public static bool IsD3DAvailable { get; }

            public bool HasBackBuffer { get; }

            public bool HasNewPicture { get; private set; }

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

            public void Clear(D3DImage image)
            {
                lock (SyncLock)
                {
                    if (image == null || !image.IsFrontBufferAvailable)
                        return;

                    image.Lock();
                    image.SetBackBuffer(D3DResourceType.IDirect3DSurface9, IntPtr.Zero);
                    image.Unlock();
                }
            }

            public void Write(VideoBlock block)
            {
                lock (SyncLock)
                {
                    if (IsDsiposing)
                        return;

                    if (HasBackBuffer)
                        WriteToBackBuffer(block);
                    else
                        WriteToFrontBuffer(block);

                    HasNewPicture = true;
                }
            }

            public bool Render(D3DImage image, TimeSpan renderTime)
            {
                lock (SyncLock)
                {
                    if (!HasNewPicture || renderTime.Ticks == LastRenderTime.Ticks)
                        return false;

                    if (IsDsiposing)
                    {
                        Clear(image);
                        return false;
                    }

                    // Check if there's stuff to render
                    if (HasBackBuffer && (BackBuffer == null || BackBuffer.IsDisposed))
                        return false;

                    // Check if there's stuff to render
                    if (!HasBackBuffer && (FrontBuffer == null || FrontBuffer.IsDisposed))
                        return false;

                    var width = HasBackBuffer ? BackBuffer.Description.Width : FrontBuffer.Description.Width;
                    var height = HasBackBuffer ? BackBuffer.Description.Height : FrontBuffer.Description.Height;
                    EnsureFrontBuffer(width, height);

                    if (image.IsFrontBufferAvailable)
                    {
                        if (image.TryLock(new Duration(TimeSpan.Zero)))
                        {
                            if (HasBackBuffer)
                                Device.UpdateSurface(BackBuffer, FrontBuffer);

                            image.SetBackBuffer(D3DResourceType.IDirect3DSurface9, FrontBuffer.NativePointer);
                            image.AddDirtyRect(new Int32Rect(0, 0, width, height));
                        }

                        image.Unlock();
                    }

                    LastRenderTime = TimeSpan.FromTicks(renderTime.Ticks);
                    HasNewPicture = false;
                    return true;
                }
            }

            public void Dispose()
            {
                lock (SyncLock)
                {
                    if (IsDsiposing)
                        return;

                    IsDsiposing = true;
                    FrontBuffer?.Dispose();
                    BackBuffer?.Dispose();
                    m_Device?.Dispose();
                }
            }

            /// <summary>
            /// Helper method that creates a D3D device.
            /// </summary>
            /// <returns>A D3D device.</returns>
            private static DeviceEx CreateDevice() =>
                new DeviceEx(Engine, DefaultDisplayAdapter, DeviceType.Hardware, NativeMethods.GetDesktopWindow(), DeviceCreationFlags, DeviceParameters);

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

            private void WriteToBackBuffer(VideoBlock block)
            {
                var rect = EnsureBackBuffer(block.PixelWidth, block.PixelHeight);
                NativeMethods.LoadSurfaceFromMemory(
                    BackBuffer, block.Buffer, Filter.None, 0, SurfaceFormat, block.PictureBufferStride, rect, null, null);
            }

            private void WriteToFrontBuffer(VideoBlock block)
            {
                var rect = EnsureFrontBuffer(block.PixelWidth, block.PixelHeight);
                NativeMethods.LoadSurfaceFromMemory(
                    FrontBuffer, block.Buffer, Filter.None, 0, SurfaceFormat, block.PictureBufferStride, rect, null, null);
            }

            private RawRectangle EnsureBackBuffer(int width, int height)
            {
                if (BackBuffer == null || BackBuffer.Description.Width != width || BackBuffer.Description.Height != height)
                {
                    BackBuffer?.Dispose();

                    // Create an off-screen target
                    BackBuffer = Surface.CreateOffscreenPlainEx(
                        Device, width, height, SurfaceFormat, Pool.SystemMemory, Usage.None);
                }

                return new RawRectangle(0, 0, width, height);
            }

            private RawRectangle EnsureFrontBuffer(int width, int height)
            {
                if (FrontBuffer == null || FrontBuffer.Description.Width != width || FrontBuffer.Description.Height != height)
                {
                    FrontBuffer?.Dispose();
                    FrontBuffer = Surface.CreateRenderTargetEx(
                        Device, width, height, SurfaceFormat, MultisampleType.None, 0, false, Usage.None);
                }

                return new RawRectangle(0, 0, width, height);
            }
        }
    }
}
#pragma warning restore CA1812