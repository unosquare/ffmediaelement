namespace Unosquare.FFME.Rendering
{
    using Core;
    using Decoding;
    using System;
    using System.Windows;
    using System.Windows.Media;
    using System.Windows.Media.Imaging;
    using System.Windows.Threading;

    /// <summary>
    /// Provides Video Image Rendering via a WPF Writable Bitmap
    /// </summary>
    /// <seealso cref="Unosquare.FFME.Rendering.IRenderer" />
    internal sealed class VideoRenderer : IRenderer
    {
        #region Private State

        /// <summary>
        /// The target bitmap
        /// </summary>
        private WriteableBitmap TargetBitmap;

        private volatile bool IsRenderingInProgress = false;

        #endregion

        #region Constructors

        /// <summary>
        /// Initializes a new instance of the <see cref="VideoRenderer"/> class.
        /// </summary>
        /// <param name="mediaElement">The media element.</param>
        public VideoRenderer(MediaElement mediaElement)
        {
            MediaElement = mediaElement;
            InitializeTargetBitmap(null);
        }

        /// <summary>
        /// Initializes the target bitmap. Pass a null block to initialize with the default video properties.
        /// </summary>
        /// <param name="block">The block.</param>
        private void InitializeTargetBitmap(VideoBlock block)
        {
            Utils.UIInvoke(DispatcherPriority.Normal, () =>
            {
                var visual = PresentationSource.FromVisual(MediaElement);

                var dpiX = 96.0 * visual?.CompositionTarget?.TransformToDevice.M11 ?? 96.0;
                var dpiY = 96.0 * visual?.CompositionTarget?.TransformToDevice.M22 ?? 96.0;

                var pixelWidth = block?.PixelWidth ?? MediaElement.NaturalVideoWidth;
                var pixelHeight = block?.PixelHeight ?? MediaElement.NaturalVideoHeight;

                if (MediaElement.HasVideo && pixelWidth > 0 && pixelHeight > 0)
                    TargetBitmap = new WriteableBitmap(
                        block?.PixelWidth ?? MediaElement.NaturalVideoWidth,
                        block?.PixelHeight ?? MediaElement.NaturalVideoHeight,
                        dpiX, dpiY, PixelFormats.Bgr24, null);
                else
                    TargetBitmap = null; // new WriteableBitmap(1, 1, dpiX, dpiY, PixelFormats.Bgr24, null);

                MediaElement.ViewBox.Source = TargetBitmap;
            });
        }

        #endregion

        #region Public API

        /// <summary>
        /// Gets the parent media element.
        /// </summary>
        public MediaElement MediaElement { get; private set; }

        /// <summary>
        /// Executed when the Play method is called on the parent MediaElement
        /// </summary>
        public void Play()
        {
            // placeholder
        }

        /// <summary>
        /// Executed when the Pause method is called on the parent MediaElement
        /// </summary>
        public void Pause()
        {
            // placeholder
        }


        /// <summary>
        /// Executed when the Pause method is called on the parent MediaElement
        /// </summary>
        public void Stop()
        {
            // placeholder
        }

        /// <summary>
        /// Executed when the Close method is called on the parent MediaElement
        /// </summary>
        public void Close()
        {
            Utils.UIInvoke(DispatcherPriority.Render, () =>
            {
                if (TargetBitmap == null) return;
                TargetBitmap = null;
                MediaElement.ViewBox.Source = TargetBitmap;
            });
        }

        /// <summary>
        /// Executed after a Seek operation is performed on the parent MediaElement
        /// </summary>
        public void Seek()
        {
            // placeholder
        }

        /// <summary>
        /// Renders the specified media block.
        /// This needs to return immediately so the calling thread is not disturbed.
        /// </summary>
        /// <param name="mediaBlock">The media block.</param>
        /// <param name="clockPosition">The clock position.</param>
        /// <param name="renderIndex">Index of the render.</param>
        public void Render(MediaBlock mediaBlock, TimeSpan clockPosition, int renderIndex)
        {
            var block = mediaBlock as VideoBlock;
            if (block == null) return;
            if (IsRenderingInProgress) return;

            IsRenderingInProgress = true;

            Utils.UIEnqueueInvoke(DispatcherPriority.Render, new Action<VideoBlock, TimeSpan, int>((b, cP, rI) =>
            {
                try
                {
                    if (TargetBitmap == null || TargetBitmap.PixelWidth != b.PixelWidth || TargetBitmap.PixelHeight != b.PixelHeight)
                        InitializeTargetBitmap(b);

                    var updateRect = new Int32Rect(0, 0, b.PixelWidth, b.PixelHeight);
                    TargetBitmap.WritePixels(updateRect, b.Buffer, b.BufferLength, b.BufferStride);

                    var scaleTransform = MediaElement.ViewBox.LayoutTransform as ScaleTransform;

                    // Process Aspect Ratio according to block.
                    if (b.AspectWidth != b.AspectHeight)
                    {
                        var scaleX = b.AspectWidth > b.AspectHeight ? (double)b.AspectWidth / b.AspectHeight : 1d;
                        var scaleY = b.AspectHeight > b.AspectWidth ? (double)b.AspectHeight / b.AspectWidth : 1d;

                        if (scaleTransform == null)
                        {
                            scaleTransform = new ScaleTransform(scaleX, scaleY);
                            MediaElement.ViewBox.LayoutTransform = scaleTransform;
                        }

                        if (scaleTransform.ScaleX != scaleX || scaleTransform.ScaleY != scaleY)
                        {
                            scaleTransform.ScaleX = scaleX;
                            scaleTransform.ScaleY = scaleY;
                        }
                    }
                    else
                    {
                        if (scaleTransform != null && (scaleTransform.ScaleX != 1d || scaleTransform.ScaleY != 1d))
                        {
                            scaleTransform.ScaleX = 1d;
                            scaleTransform.ScaleY = 1d;
                        }
                    }
                }
                catch (Exception ex)
                {
                    Utils.Log(MediaElement, MediaLogMessageType.Error, $"{nameof(VideoRenderer)} {ex.GetType()}: {ex.Message}. Stack Trace:\r\n{ex.StackTrace}");
                }
                finally
                {
                    IsRenderingInProgress = false;
                }
            }), block, clockPosition, renderIndex);
        }

        #endregion
    }
}
