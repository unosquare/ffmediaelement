namespace Unosquare.FFME.MacOS.Rendering
{
    using System;
    using System.Runtime.InteropServices;
    using AppKit;
    using CoreGraphics;
    using Unosquare.FFME.Core;
    using Unosquare.FFME.Decoding;
    using Unosquare.FFME.Rendering;

    /// <summary>
    /// Provides Video Image Rendering via NSImage.
    /// </summary>
    /// <seealso cref="Unosquare.FFME.Rendering.IRenderer" />
    class VideoRenderer : IRenderer
    {
        private AtomicBoolean IsRenderingInProgress = new AtomicBoolean();

        /// <summary>
        /// Initializes a new instance of the <see cref="T:Unosquare.FFME.MacOS.Rendering.VideoRenderer"/> class.
        /// </summary>
        /// <param name="mediaElementCore">Media element core.</param>
        public VideoRenderer(MediaElementCore mediaElementCore)
        {
            MediaElementCore = mediaElementCore;
        }

        /// <summary>
        /// Gets the media element core player component.
        /// </summary>
        /// <value>The media element core.</value>
        public MediaElementCore MediaElementCore { get; }

        public void Close()
        {
        }

        public void Pause()
        {
        }

        public void Play()
        {
        }

        public void Render(MediaBlock mediaBlock, TimeSpan clockPosition)
        {
            var block = mediaBlock as VideoBlock;
            if (block == null) return;
            //if (IsRenderingInProgress.Value == true)
            //{
            //    //MediaElement.Logger.Log(MediaLogMessageType.Debug, $"{nameof(VideoRenderer)}: Frame skipped at {mediaBlock.StartTime}");
            //    return;
            //}

            IsRenderingInProgress.Value = true;

            var size = block.BufferLength;
            var bytes = new byte[size];
            Marshal.Copy(block.Buffer, bytes, 0, size);

            Transform(block.Buffer, bytes, block.PixelWidth, block.PixelHeight);
        }

        private void Transform(IntPtr buffer, byte[] bytes, int width, int height)
        {
            try
            {

                var space = CGColorSpace.CreateDeviceRGB();
                var provider = new CGDataProvider(bytes);
                //var provider = new CGDataProvider(buffer);
                //var i = new CGImage(64, 64, 8, 24, 64 * 3, space, CGBitmapFlags.ByteOrderDefault, provider, null, false, CGColorRenderingIntent.Default);
                var i = new CGImage(width, height, 8, 24, width * 3, space, CGBitmapFlags.ByteOrderDefault, provider, null, false, CGColorRenderingIntent.Default);
                var nsImage = new NSImage(i, new CGSize(width, height));
                Platform.UIInvoke(CoreDispatcherPriority.Normal, () =>
                {
                    ((MediaElementCore.Parent) as MediaElement).ImageView.Image = nsImage;
                });
            }
            catch (Exception e)
            {
                Console.WriteLine(e);
            }
        }

        public void Seek()
        {
        }

        public void Stop()
        {
        }

        public void Update(TimeSpan clockPosition)
        {
        }

        public void WaitForReadyState()
        {
        }
    }
}
