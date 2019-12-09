#pragma warning disable CA1812
namespace Unosquare.FFME.Rendering
{
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
    /// https://www.codeproject.com/Articles/28526/Introduction-to-D3DImage.
    /// </summary>
    /// <seealso cref="IMediaRenderer" />
    /// <seealso cref="ILoggingSource" />
    internal sealed class D3DVideoRenderer : IMediaRenderer, ILoggingSource, IDisposable
    {
        private readonly AtomicBoolean IsRendering = new AtomicBoolean(false);

        private readonly Direct3D Engine;
        private readonly AtomicBoolean IsDisposed = new AtomicBoolean(false);
        private readonly Device Device;

        private Surface BackSurface;
        private Surface FrontSurface;
        private D3DImage TargetImage;

        public D3DVideoRenderer(MediaEngine mediaCore)
        {
            MediaCore = mediaCore;
            Engine = new Direct3D();

            var windowHandle = NativeMethods.GetDesktopWindow();
            var createFlags = CreateFlags.HardwareVertexProcessing | CreateFlags.Multithreaded | CreateFlags.FpuPreserve;
            var presentParameters = new PresentParameters
            {
                Windowed = true,
                SwapEffect = SwapEffect.Discard,
                PresentationInterval = PresentInterval.Default,
                BackBufferFormat = Format.Unknown,
                BackBufferWidth = 1,
                BackBufferHeight = 1,
            };

            // createFlags = CreateFlags.HardwareVertexProcessing;
            // windowHandle = IntPtr.Zero;
            // presentParameters = new PresentParameters(1, 1);
            Device = new Device(Engine, 0, DeviceType.Hardware, windowHandle, createFlags, presentParameters);
        }

        /// <inheritdoc />
        ILoggingHandler ILoggingSource.LoggingHandler => MediaCore;

        /// <inheritdoc />
        public MediaEngine MediaCore { get; }

        /// <summary>
        /// Gets the parent media element (platform specific).
        /// </summary>
        public MediaElement MediaElement => MediaCore?.Parent as MediaElement;

        /// <inheritdoc />
        public void OnClose() => Dispose(true);

        /// <inheritdoc />
        public void OnPause()
        {
            // placeholder
        }

        /// <inheritdoc />
        public void OnPlay()
        {
            // placeholder
        }

        /// <inheritdoc />
        public void OnSeek()
        {
            // placeholder
        }

        /// <inheritdoc />
        public void OnStarting()
        {
            // placeholder
        }

        /// <inheritdoc />
        public void OnStop()
        {
            // placeholder
        }

        /// <inheritdoc />
        public void Update(TimeSpan clockPosition)
        {
            // placeholder
        }

        /// <inheritdoc />
        public unsafe void Render(MediaBlock mediaBlock, TimeSpan clockPosition)
        {
            if (mediaBlock is VideoBlock == false) return;

            if (IsRendering == true)
                return;

            IsRendering.Value = true;

            var block = (VideoBlock)mediaBlock;
            var rect = new RawRectangle(0, 0, block.PixelWidth, block.PixelHeight);

            EnsurePresentable(block);
            if (!block.TryAcquireReaderLock(out var readerLock))
                return;

            // BackSurface.LockRectangle(LockFlags.None);
            NativeMethods.LoadSurfaceFromMemory(BackSurface, block.Buffer, Filter.None, 0, Format.A8R8G8B8, block.PictureBufferStride, rect, null, null);

            // BackSurface.UnlockRectangle();
            readerLock.Dispose();

            // Surface.FromFile(BackSurface, @"c:\Users\UnoSp\Desktop\images.jpg", Filter.None, 0);
            Device.UpdateSurface(BackSurface, FrontSurface);

            var videoView = MediaElement?.VideoView;
            if (videoView != null && videoView.ElementDispatcher != null)
            {
                videoView.ElementDispatcher.InvokeAsync(() =>
                {
                    if (!TargetImage.IsFrontBufferAvailable)
                        return;

                    TargetImage.Lock();
                    TargetImage.AddDirtyRect(new Int32Rect(0, 0, block.PixelWidth, block.PixelHeight));
                    TargetImage.Unlock();
                });
            }

            IsRendering.Value = false;
        }

        public void Dispose() => Dispose(true);

        private void EnsurePresentable(VideoBlock block)
        {
            if (TargetImage != null && BackSurface != null && BackSurface.Description.Width == block.PixelWidth && BackSurface.Description.Height == block.PixelHeight)
                return;

            var videoView = MediaElement?.VideoView;
            if (videoView != null && videoView.ElementDispatcher != null)
            {
                videoView.ElementDispatcher.Invoke(() =>
                {
                    if (TargetImage == null)
                        TargetImage = new D3DImage();

                    videoView.Source = TargetImage;
                    TargetImage.Lock();
                    TargetImage.SetBackBuffer(D3DResourceType.IDirect3DSurface9, IntPtr.Zero);
                    TargetImage.Unlock();
                });
            }

            BackSurface?.Dispose();
            FrontSurface?.Dispose();

            // Create an empty offscreen surface. Use SystemMemory to allow for surface copying.
            BackSurface = Surface.CreateOffscreenPlain(Device, block.PixelWidth, block.PixelHeight, Format.A8R8G8B8, Pool.SystemMemory);

            // Create the surface that will act as the render target.
            // Set as lockable (required for D3DImage)
            FrontSurface = Surface.CreateRenderTarget(Device, block.PixelWidth, block.PixelHeight, Format.A8R8G8B8, MultisampleType.None, 0, true);

            if (videoView != null && videoView.ElementDispatcher != null)
            {
                videoView.ElementDispatcher.Invoke(() =>
                {
                    if (TargetImage == null)
                        TargetImage = new D3DImage();

                    videoView.Source = TargetImage;
                    TargetImage.Lock();
                    TargetImage.SetBackBuffer(D3DResourceType.IDirect3DSurface9, FrontSurface.NativePointer);
                    TargetImage.Unlock();
                });
            }
        }

        private void Dispose(bool alsoManaged)
        {
            if (IsDisposed.Value) return;
            IsDisposed.Value = true;

            if (alsoManaged)
            {
                BackSurface?.Dispose();
                FrontSurface?.Dispose();
                Device?.Dispose();
                Engine.Dispose();
            }
        }

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