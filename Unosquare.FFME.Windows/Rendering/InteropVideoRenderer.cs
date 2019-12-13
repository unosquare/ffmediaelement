namespace Unosquare.FFME.Rendering
{
    using System;
    using System.IO.MemoryMappedFiles;
    using System.Windows.Interop;
    using System.Windows.Media;
    using System.Windows.Threading;
    using Common;
    using Container;
    using Diagnostics;
    using Unosquare.FFME.Engine;

    internal sealed class InteropVideoRenderer : VideoRendererBase, IDisposable
    {
        private readonly InteropBuffer Graphics;

        public InteropVideoRenderer(MediaEngine mediaCore)
            : base(mediaCore)
        {
            Graphics = new InteropBuffer(this);
        }

        public override void Render(MediaBlock mediaBlock, TimeSpan clockPosition)
        {
            var block = BeginRenderingCycle(mediaBlock);
            if (block == null) return;

            if (!block.TryAcquireWriterLock(out var blockLock))
                return;

            try
            {
                var bitmap = Graphics.Write(block);
                if (bitmap == null)
                    return;

                MediaElement?.RaiseRenderingVideoEvent(block, bitmap, clockPosition);
                UpdateTargetImage(DispatcherPriority.Background, false);
                FinishRenderingCycle(block, clockPosition);
            }
            catch (Exception ex)
            {
                this.LogError(Aspects.VideoRenderer, $"{nameof(InteropVideoRenderer)}.{nameof(Render)} bitmap failed.", ex);
            }
            finally
            {
                blockLock.Dispose();
            }
        }

        public override void OnClose()
        {
            base.OnClose();
            Dispose();
        }

        public void Dispose() => Graphics?.Dispose();

        private void UpdateTargetImage(DispatcherPriority priority, bool syncrhonous)
        {
            var task = VideoDispatcher?.InvokeAsync(() => Graphics.Render(MediaElement.VideoView), priority);
            if (syncrhonous)
                task?.Wait();
        }

        private sealed class InteropBuffer : IDisposable
        {
            private readonly object SyncLock = new object();
            private readonly InteropVideoRenderer Parent;

            private MemoryMappedFile BackBufferFile;
            private MemoryMappedViewAccessor BackBufferView;
            private InteropBitmap BackBufferImage;
            private BitmapDataBuffer BitmapData;
            private bool IsDisposed;
            private bool NeedsNewImage;
            private int Width;
            private int Height;
            private int Stride;

            public InteropBuffer(InteropVideoRenderer parent)
            {
                Parent = parent;
            }

            public unsafe BitmapDataBuffer Write(VideoBlock block)
            {
                lock (SyncLock)
                {
                    if (IsDisposed) return null;

                    EnsureBuffers(block);

                    // Compute a safe number of bytes to copy
                    // At this point, we it is assumed the strides are equal
                    var bufferLength = Math.Min(block.BufferLength, BackBufferView.Capacity);
                    var scan0 = BackBufferView.SafeMemoryMappedViewHandle.DangerousGetHandle();

                    // Copy the block data into the back buffer of the target bitmap.
                    Buffer.MemoryCopy(
                        block.Buffer.ToPointer(),
                        scan0.ToPointer(),
                        bufferLength,
                        bufferLength);

                    if (BitmapData == null || BitmapData.Scan0 != scan0)
                    {
                        BitmapData = new BitmapDataBuffer(
                            scan0, block.PictureBufferStride, block.PixelWidth, block.PixelHeight, Parent.DpiX, Parent.DpiY);
                    }

                    BackBufferView.Flush();
                    return BitmapData;
                }
            }

            public void Render(ImageHost host)
            {
                lock (SyncLock)
                {
                    if (IsDisposed) return;

                    if (NeedsNewImage)
                    {
                        BackBufferImage = (InteropBitmap)Imaging.CreateBitmapSourceFromMemorySection(
                            BackBufferFile.SafeMemoryMappedFileHandle.DangerousGetHandle(), Width, Height, PixelFormats.Bgra32, Stride, 0);

                        NeedsNewImage = false;
                    }

                    BackBufferImage.Invalidate();
                    host.Source = BackBufferImage;
                }
            }

            public void Dispose()
            {
                lock (SyncLock)
                {
                    if (IsDisposed) return;
                    IsDisposed = true;

                    BackBufferView?.Dispose();
                    BackBufferFile?.Dispose();
                }
            }

            private void EnsureBuffers(VideoBlock block)
            {
                if (BackBufferView == null || BackBufferView.Capacity != block.BufferLength)
                {
                    BackBufferView?.Dispose();
                    BackBufferFile?.Dispose();

                    BackBufferFile = MemoryMappedFile.CreateNew(null, block.BufferLength);
                    BackBufferView = BackBufferFile.CreateViewAccessor();
                    NeedsNewImage = true;
                }

                Width = block.PixelWidth;
                Height = block.PixelHeight;
                Stride = block.PictureBufferStride;
            }
        }
    }
}
