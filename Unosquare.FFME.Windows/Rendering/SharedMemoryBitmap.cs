namespace Unosquare.FFME.Rendering
{
    using FFmpeg.AutoGen;
    using Platform;
    using Shared;
    using System;
    using System.Collections.Generic;
    using System.Runtime.InteropServices;
    using System.Windows;
    using System.Windows.Interop;

    internal sealed class SharedMemoryBitmap : IDisposable
    {
        private const double DefaultDpi = 96.0;

        /// <summary>
        /// Contains an equivalence lookup of FFmpeg pixel fromat and WPF pixel formats.
        /// </summary>
        private static readonly Dictionary<AVPixelFormat, System.Windows.Media.PixelFormat> MediaPixelFormats
            = new Dictionary<AVPixelFormat, System.Windows.Media.PixelFormat>
        {
            { AVPixelFormat.AV_PIX_FMT_BGR0, System.Windows.Media.PixelFormats.Bgr32 }
        };

        private bool IsDisposed = false; // To detect redundant calls
        private double DpiX = DefaultDpi;
        private double DpiY = DefaultDpi;
        private InteropBitmap RenderBitmapSource = null;
        private IntPtr Scan0 = IntPtr.Zero;
        private int BufferLength = 0;
        private VideoRenderer Renderer;

        public SharedMemoryBitmap(VideoRenderer videoRenderer)
        {
            Renderer = videoRenderer;
            var visual = PresentationSource.FromVisual(Renderer.MediaElement);

            DpiX = DefaultDpi * visual?.CompositionTarget?.TransformToDevice.M11 ?? DefaultDpi;
            DpiY = DefaultDpi * visual?.CompositionTarget?.TransformToDevice.M22 ?? DefaultDpi;
        }

        ~SharedMemoryBitmap()
        {
            Dispose(false);
        }

        public void Load(VideoBlock block)
        {
            EnsureLoadable(block);
            WindowsNativeMethods.Instance.CopyMemory(Scan0, block.Buffer, (uint)block.BufferLength);
        }

        public void Dispose()
        {
            GC.SuppressFinalize(this);
            Dispose(true);
        }

        public void Render()
        {
            if (Renderer.MediaElement.ViewBox.Source != RenderBitmapSource)
                Renderer.MediaElement.ViewBox.Source = RenderBitmapSource;

            RenderBitmapSource?.Invalidate();
        }

        private void EnsureLoadable(VideoBlock block)
        {
            if (AllocateBuffer(block.BufferLength) == false && RenderBitmapSource != null)
                return;

            RenderBitmapSource = Imaging.CreateBitmapSourceFromMemorySection(
                Scan0,
                block.PixelWidth,
                block.PixelHeight,
                MediaPixelFormats[Defaults.VideoPixelFormat],
                block.BufferStride,
                0) as InteropBitmap;

            if (RenderBitmapSource.CanFreeze)
                RenderBitmapSource.Freeze();
        }

        private bool AllocateBuffer(int length)
        {
            if (BufferLength == length) return false;

            if (BufferLength != length)
                DestroyBuffer();

            Scan0 = Marshal.AllocHGlobal(length);
            BufferLength = length;

            return true;
        }

        private void DestroyBuffer()
        {
            if (Scan0 == null)
            {
                BufferLength = 0;
                return;
            }

            Marshal.FreeHGlobal(Scan0);
            BufferLength = 0;
        }

        #region IDisposable Support

        private void Dispose(bool alsoManaged)
        {
            if (!IsDisposed)
            {
                if (alsoManaged)
                {
                    // dispose managed state (managed objects).
                }

                // Free unmanaged resources (unmanaged objects) and override a finalizer below.
                DestroyBuffer();

                // Set large fields to null.
                RenderBitmapSource = null;

                IsDisposed = true;
            }
        }

        #endregion
    }
}
