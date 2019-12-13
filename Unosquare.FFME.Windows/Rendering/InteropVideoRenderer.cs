namespace Unosquare.FFME.Rendering
{
    using Container;
    using System;
    using System.IO.MemoryMappedFiles;
    using System.Windows.Interop;
    using System.Windows.Media;
    using System.Windows.Threading;
    using Unosquare.FFME.Diagnostics;
    using Unosquare.FFME.Engine;

    internal sealed class InteropVideoRenderer : VideoRendererBase
    {
        private readonly InteropBuffer Graphics = new InteropBuffer();

        public InteropVideoRenderer(MediaEngine mediaCore)
            : base(mediaCore)
        {
            // placeholder
        }

        public override void Render(MediaBlock mediaBlock, TimeSpan clockPosition)
        {
            var block = BeginRenderingCycle(mediaBlock);
            if (block == null) return;

            Graphics.Write(block);

            VideoDispatcher?.BeginInvoke(() =>
            {
                try
                {
                    Graphics.Render(MediaElement.VideoView);
                }
                catch (Exception ex)
                {
                    this.LogError(Aspects.VideoRenderer, $"{nameof(InteropVideoRenderer)}.{nameof(Render)} bitmap failed.", ex);
                }
                finally
                {
                    FinishRenderingCycle(block, clockPosition);
                }
            }, DispatcherPriority.Send);
        }

        private class InteropBuffer
        {
            private readonly object SyncLock = new object();

            private MemoryMappedFile BackBufferFile;
            private MemoryMappedViewAccessor BackBufferView;
            private InteropBitmap BackBufferImage;
            private bool NeedsNewImage;
            private int Width;
            private int Height;
            private int Stride;

            public InteropBuffer()
            {
                // placeholder
            }

            public unsafe void Write(VideoBlock block)
            {
                lock (SyncLock)
                {
                    EnsureBuffers(block);

                    // Compute a safe number of bytes to copy
                    // At this point, we it is assumed the strides are equal
                    var bufferLength = Math.Min(block.BufferLength, BackBufferView.Capacity);
                    var scan0 = BackBufferView.SafeMemoryMappedViewHandle.DangerousGetHandle().ToPointer();

                    // Copy the block data into the back buffer of the target bitmap.
                    Buffer.MemoryCopy(
                        block.Buffer.ToPointer(),
                        scan0,
                        bufferLength,
                        bufferLength);
                }
            }

            public void Render(ImageHost host)
            {
                lock (SyncLock)
                {
                    if (NeedsNewImage && Stride > 0)
                    {
                        BackBufferImage = (InteropBitmap)Imaging.CreateBitmapSourceFromMemorySection(
                            BackBufferFile.SafeMemoryMappedFileHandle.DangerousGetHandle(), Width, Height, PixelFormats.Bgra32, Stride, 0);

                        NeedsNewImage = false;
                        Stride = 0;
                    }
                }

                host.Source = BackBufferImage;
                BackBufferImage.Invalidate();
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
