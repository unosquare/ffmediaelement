namespace Unosquare.FFME.Rendering
{
    using Events;
    using FFmpeg.AutoGen;
    using Platform;
    using Primitives;
    using Shared;
    using System;
    using System.Collections.Generic;
    using System.Runtime.CompilerServices;
    using System.Threading.Tasks;
    using System.Windows;
    using System.Windows.Media;
    using System.Windows.Media.Imaging;
    using System.Windows.Threading;

    /// <summary>
    /// Provides Video Image Rendering via a WPF Writable Bitmap
    /// </summary>
    /// <seealso cref="IMediaRenderer" />
    internal sealed class VideoRenderer : IMediaRenderer
    {
        #region Private State

        private const double DefaultDpi = 96.0;

        /// <summary>
        /// Contains an equivalence lookup of FFmpeg pixel fromat and WPF pixel formats.
        /// </summary>
        private static readonly Dictionary<AVPixelFormat, PixelFormat> MediaPixelFormats = new Dictionary<AVPixelFormat, PixelFormat>
        {
            { AVPixelFormat.AV_PIX_FMT_BGR0, PixelFormats.Bgr32 }
        };

        /// <summary>
        /// The bitmap that is presented to the user.
        /// </summary>
        private WriteableBitmap TargetBitmap = null;

        /// <summary>
        /// Set when a bitmap is being written to the target bitmap
        /// </summary>
        private AtomicBoolean IsRenderingInProgress = new AtomicBoolean(false);

        /// <summary>
        /// The load block buffer on locking
        /// </summary>
        private bool LoadBlockBufferOnGui = true;

        /// <summary>
        /// The raise video event on GUI
        /// </summary>
        private bool RaiseVideoEventOnGui = true;

        #endregion

        #region Constructors

        /// <summary>
        /// Initializes a new instance of the <see cref="VideoRenderer"/> class.
        /// </summary>
        /// <param name="mediaEngine">The core media element.</param>
        public VideoRenderer(MediaEngine mediaEngine)
        {
            MediaCore = mediaEngine;

            // Check that the renderer supports the passed in Pixel format
            if (MediaPixelFormats.ContainsKey(Constants.Video.VideoPixelFormat) == false)
                throw new NotSupportedException($"Unable to get equivalent pixel fromat from source: {Constants.Video.VideoPixelFormat}");

            // Set the DPI
            GuiContext.Current.Invoke(() =>
            {
                var visual = PresentationSource.FromVisual(MediaElement);
                DpiX = 96.0 * visual?.CompositionTarget?.TransformToDevice.M11 ?? 96.0;
                DpiY = 96.0 * visual?.CompositionTarget?.TransformToDevice.M22 ?? 96.0;
            });
        }

        /// <summary>
        /// Gets the parent media element (platform specific).
        /// </summary>
        public MediaElement MediaElement => MediaCore?.Parent as MediaElement;

        /// <summary>
        /// Gets the core platform independent player component.
        /// </summary>
        public MediaEngine MediaCore { get; }

        #endregion

        #region Properties

        /// <summary>
        /// Gets the DPI along the X axis.
        /// </summary>
        public double DpiX { get; private set; } = DefaultDpi;

        /// <summary>
        /// Gets the DPI along the Y axis.
        /// </summary>
        public double DpiY { get; private set; } = DefaultDpi;

        #endregion

        #region Unused Media Renderer Methods

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

        #region MediaRenderer Methods

        /// <summary>
        /// Renders the specified media block.
        /// This needs to return immediately so the calling thread is not disturbed.
        /// </summary>
        /// <param name="mediaBlock">The media block.</param>
        /// <param name="clockPosition">The clock position.</param>
        public void Render(MediaBlock mediaBlock, TimeSpan clockPosition)
        {
            var block = mediaBlock as VideoBlock;
            if (block == null) return;
            if (IsRenderingInProgress.Value == true)
            {
                MediaElement?.MediaCore?.Log(MediaLogMessageType.Debug, $"{nameof(VideoRenderer)}: Frame skipped at {mediaBlock.StartTime}");
                return;
            }

            // Flag the start of a rendering cycle
            IsRenderingInProgress.Value = true;

            // Ensure the target bitmap can be loaded
            var bitmapData = LockTargetBitmap(block);

            // Check if we have a valid pointer to the back-buffer
            if (bitmapData == null)
            {
                IsRenderingInProgress.Value = false;
                return;
            }

            // Write the pixels on a non-UI thread
            if (LoadBlockBufferOnGui == false)
                LoadTarget(bitmapData, block);

            // Fire the rendering event o a non-UI thread
            if (RaiseVideoEventOnGui == false)
                MediaElement.RaiseRenderingVideoEvent(block, bitmapData, clockPosition);

            // Send to the rendering to the UI
            GuiContext.Current.EnqueueInvoke(DispatcherPriority.Render,
                () => { RenderTargetBitmap(block, bitmapData, clockPosition); });
        }

        /// <summary>
        /// Executed when the Close method is called on the parent MediaElement
        /// </summary>
        public void Close()
        {
            GuiContext.Current.EnqueueInvoke(() =>
            {
                TargetBitmap = null;
                MediaElement.VideoView.Source = null;
            });
        }

        #endregion

        /// <summary>
        /// Renders the target bitmap.
        /// </summary>
        /// <param name="block">The block.</param>
        /// <param name="bitmapData">The bitmap data.</param>
        /// <param name="clockPosition">The clock position.</param>
        private void RenderTargetBitmap(VideoBlock block, BitmapDataBuffer bitmapData, TimeSpan clockPosition)
        {
            try
            {
                if (RaiseVideoEventOnGui)
                    MediaElement.RaiseRenderingVideoEvent(block, bitmapData, clockPosition);

                // Signal an update on the rendering surface
                TargetBitmap?.AddDirtyRect(bitmapData.UpdateRect);
                TargetBitmap?.Unlock();
                ApplyScaleTransform(block);
            }
            catch (Exception ex)
            {
                MediaElement?.MediaCore?.Log(
                    MediaLogMessageType.Error,
                    $"{nameof(VideoRenderer)} {ex.GetType()}: {ex.Message}. Stack Trace:\r\n{ex.StackTrace}");
            }
            finally
            {
                IsRenderingInProgress.Value = false;
            }
        }

        /// <summary>
        /// Initializes the target bitmap if not available and locks it for loading the back-buffer.
        /// </summary>
        /// <param name="block">The block.</param>
        /// <returns>
        /// The locking result. Returns a null pointer on back buffer for invalid.
        /// </returns>
        private BitmapDataBuffer LockTargetBitmap(VideoBlock block)
        {
            // Result will be set on the GUI thread
            BitmapDataBuffer result = null;

            GuiContext.Current.Invoke(() =>
            {
                // Skip the locking if scrubbing is not enabled
                if (MediaElement.ScrubbingEnabled == false && (MediaElement.IsPlaying == false || MediaElement.IsSeeking))
                    return;

                // Figure out what we need to do
                var needsCreation = TargetBitmap == null && MediaElement.HasVideo;
                var needsModification = needsCreation == false && (TargetBitmap.PixelWidth != block.PixelWidth || TargetBitmap.PixelHeight != block.PixelHeight);
                var hasValidDimensions = block.PixelWidth > 0 && block.PixelHeight > 0;

                // Instantiate or update the target bitmap
                if ((needsCreation || needsModification) && hasValidDimensions)
                {
                    TargetBitmap = new WriteableBitmap(
                        block.PixelWidth, block.PixelHeight, DpiX, DpiY, MediaPixelFormats[Constants.Video.VideoPixelFormat], null);
                }
                else if (hasValidDimensions == false)
                {
                    TargetBitmap = null;
                }

                // Update the target ViewBox image if not already set
                if (MediaElement.VideoView.Source != TargetBitmap)
                    MediaElement.VideoView.Source = TargetBitmap;

                // Don't set the result
                if (TargetBitmap == null) return;

                // Lock the back-buffer and create a pointer to it
                TargetBitmap.Lock();
                result = BitmapDataBuffer.FromWriteableBitmap(TargetBitmap);

                if (LoadBlockBufferOnGui)
                    LoadTarget(result, block);
            });

            return result;
        }

        /// <summary>
        /// Loads that target data buffer with block data
        /// </summary>
        /// <param name="target">The target.</param>
        /// <param name="source">The source.</param>
        private void LoadTarget(BitmapDataBuffer target, VideoBlock source)
        {
            // Copy the block data into the back buffer of the target bitmap.
            if (target.Stride == source.BufferStride)
            {
                WindowsNativeMethods.Instance.CopyMemory(target.Scan0, source.Buffer, (uint)source.BufferLength);
            }
            else
            {
                var format = MediaPixelFormats[Constants.Video.VideoPixelFormat];
                var bytesPerPixel = format.BitsPerPixel / 8;
                var copyLength = (uint)Math.Min(target.Stride, source.BufferStride);
                Parallel.For(0, source.PixelHeight, (i) =>
                {
                    var sourceOffset = source.Buffer + (i * source.BufferStride);
                    var targetOffset = target.Scan0 + (i * target.Stride);
                    WindowsNativeMethods.Instance.CopyMemory(targetOffset, sourceOffset, copyLength);
                });
            }
        }

        /// <summary>
        /// Applies the scale transform according to the block's aspect ratio.
        /// </summary>
        /// <param name="b">The b.</param>
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        private void ApplyScaleTransform(VideoBlock b)
        {
            var scaleTransform = MediaElement.VideoView.LayoutTransform as ScaleTransform;

            // Process Aspect Ratio according to block.
            if (b.AspectWidth != b.AspectHeight)
            {
                var scaleX = b.AspectWidth > b.AspectHeight ? (double)b.AspectWidth / b.AspectHeight : 1d;
                var scaleY = b.AspectHeight > b.AspectWidth ? (double)b.AspectHeight / b.AspectWidth : 1d;

                if (scaleTransform == null)
                {
                    scaleTransform = new ScaleTransform(scaleX, scaleY);
                    MediaElement.VideoView.LayoutTransform = scaleTransform;
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
