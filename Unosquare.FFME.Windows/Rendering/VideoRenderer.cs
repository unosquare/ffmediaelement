namespace Unosquare.FFME.Rendering
{
    using Core;
    using Decoding;
    using System;
    using System.Runtime.CompilerServices;
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
        /// The bitmap that is presented to the user.
        /// </summary>
        private WriteableBitmap TargetBitmap = null;

        /// <summary>
        /// Set when a bitmap is being written to the target bitmap
        /// </summary>
        private AtomicBoolean IsRenderingInProgress = new AtomicBoolean();

        #endregion

        #region Constructors

        /// <summary>
        /// Initializes a new instance of the <see cref="VideoRenderer"/> class.
        /// </summary>
        /// <param name="mediaElementCore">The core media element.</param>
        public VideoRenderer(MediaElementCore mediaElementCore)
        {
            MediaElementCore = mediaElementCore;
            InitializeTargetBitmap(null);
        }

        /// <summary>
        /// Gets the parent media element (platform specific).
        /// </summary>
        public MediaElement MediaElement => MediaElementCore?.Parent as MediaElement;

        /// <summary>
        /// Gets the core platform independent player component.
        /// </summary>
        public MediaElementCore MediaElementCore { get; }

        #endregion

        #region Methods

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
            Runner.UIInvoke(DispatcherPriority.Render, () =>
            {
                TargetBitmap = null;
                MediaElement.ViewBox.Source = null;
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
        /// Waits for the renderer to be ready to render.
        /// </summary>
        public void WaitForReadyState()
        {
            // placeholder
            // we don't need to be ready.
        }

        /// <summary>
        /// Renders the specified media block.
        /// This needs to return immediately so the calling thread is not disturbed.
        /// </summary>
        /// <param name="mediaBlock">The media block.</param>
        /// <param name="clockPosition">The clock position.</param>
        public async void Render(MediaBlock mediaBlock, TimeSpan clockPosition)
        {
            var block = mediaBlock as VideoBlock;
            if (block == null) return;
            if (IsRenderingInProgress.Value == true)
            {
                MediaElement.Logger.Log(MediaLogMessageType.Debug, $"{nameof(VideoRenderer)}: Frame skipped at {mediaBlock.StartTime}");
                return;
            }

            IsRenderingInProgress.Value = true;

            await Runner.UIEnqueueInvoke(
                DispatcherPriority.Render,
                new Action<VideoBlock, TimeSpan>((b, cP) =>
                {
                    try
                    {
                        // Skip rendering if Scrubbing is not enabled
                        if (MediaElement.ScrubbingEnabled == false && MediaElement.IsPlaying == false)
                            return;

                        if (TargetBitmap == null || TargetBitmap.PixelWidth != b.PixelWidth || TargetBitmap.PixelHeight != b.PixelHeight)
                            InitializeTargetBitmap(b);

                        var updateRect = new Int32Rect(0, 0, b.PixelWidth, b.PixelHeight);
                        TargetBitmap.WritePixels(updateRect, b.Buffer, b.BufferLength, b.BufferStride);
                        MediaElementCore.VideoSmtpeTimecode = b.SmtpeTimecode;
                        MediaElementCore.VideoHardwareDecoder = (MediaElementCore.Container?.Components?.Video?.IsUsingHardwareDecoding ?? false) ?
                            MediaElementCore.Container?.Components?.Video?.HardwareAccelerator?.Name ?? string.Empty : string.Empty;

                        MediaElement.RaiseRenderingVideoEvent(
                            TargetBitmap,
                            MediaElementCore.Container.MediaInfo.Streams[b.StreamIndex],
                            b.SmtpeTimecode,
                            b.DisplayPictureNumber,
                            b.StartTime,
                            b.Duration,
                            cP);

                        ApplyScaleTransform(b);
                    }
                    catch (Exception ex)
                    {
                        Utils.Log(MediaElement, MediaLogMessageType.Error, $"{nameof(VideoRenderer)} {ex.GetType()}: {ex.Message}. Stack Trace:\r\n{ex.StackTrace}");
                    }
                    finally
                    {
                        IsRenderingInProgress.Value = false;
                    }
                }), block,
                clockPosition);
        }

        /// <summary>
        /// Called on every block rendering clock cycle just in case some update operation needs to be performed.
        /// This needs to return immediately so the calling thread is not disturbed.
        /// </summary>
        /// <param name="clockPosition">The clock position.</param>
        public void Update(TimeSpan clockPosition)
        {
            // placeholder
        }

        #endregion

        /// <summary>
        /// Initializes the target bitmap. Pass a null block to initialize with the default video properties.
        /// </summary>
        /// <param name="block">The block.</param>
        private void InitializeTargetBitmap(VideoBlock block)
        {
            Runner.UIInvoke(DispatcherPriority.Normal, () =>
            {
                var visual = PresentationSource.FromVisual(MediaElement);

                var dpiX = 96.0 * visual?.CompositionTarget?.TransformToDevice.M11 ?? 96.0;
                var dpiY = 96.0 * visual?.CompositionTarget?.TransformToDevice.M22 ?? 96.0;

                var pixelWidth = block?.PixelWidth ?? MediaElement.NaturalVideoWidth;
                var pixelHeight = block?.PixelHeight ?? MediaElement.NaturalVideoHeight;

                if (MediaElement.HasVideo && pixelWidth > 0 && pixelHeight > 0)
                {
                    TargetBitmap = new WriteableBitmap(
                        block?.PixelWidth ?? MediaElement.NaturalVideoWidth,
                        block?.PixelHeight ?? MediaElement.NaturalVideoHeight,
                        dpiX,
                        dpiY,
                        PixelFormats.Bgr24,
                        null);
                }
                else
                {
                    TargetBitmap = null;
                }

                MediaElement.ViewBox.Source = TargetBitmap;
            });
        }

        /// <summary>
        /// Applies the scale transform according to the block's aspect ratio.
        /// </summary>
        /// <param name="b">The b.</param>
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        private void ApplyScaleTransform(VideoBlock b)
        {
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
    }
}
