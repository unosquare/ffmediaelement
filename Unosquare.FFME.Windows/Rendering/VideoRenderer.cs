#pragma warning disable CA1812
namespace Unosquare.FFME.Rendering
{
    using Common;
    using Container;
    using Diagnostics;
    using Engine;
    using FFmpeg.AutoGen;
    using Platform;
    using System;
    using System.Collections.Generic;
    using System.Diagnostics;
    using System.Runtime.CompilerServices;
    using System.Threading;
    using System.Windows;
    using System.Windows.Media;
    using System.Windows.Media.Imaging;
    using System.Windows.Threading;

    /// <summary>
    /// Provides Video Image Rendering via a WPF Writable Bitmap.
    /// </summary>
    /// <seealso cref="IMediaRenderer" />
    internal sealed class VideoRenderer : IMediaRenderer, ILoggingSource
    {
        #region Private State

        private const double DefaultDpi = 96.0;

        /// <summary>
        /// Contains an equivalence lookup of FFmpeg pixel format and WPF pixel formats.
        /// </summary>
        private static readonly Dictionary<AVPixelFormat, PixelFormat> MediaPixelFormats = new Dictionary<AVPixelFormat, PixelFormat>
        {
            { AVPixelFormat.AV_PIX_FMT_BGR0, PixelFormats.Bgr32 },
            { AVPixelFormat.AV_PIX_FMT_BGRA, PixelFormats.Bgra32 }
        };

        /// <summary>
        /// Keeps track of the elapsed time since the last frame was displayed
        /// for frame limiting purposes.
        /// </summary>
        private readonly Stopwatch RenderStopwatch = new Stopwatch();

        private readonly Stopwatch VideoBlockElapsed = new Stopwatch();

        /// <summary>
        /// The bitmap that is presented to the user.
        /// </summary>
        private WriteableBitmap m_TargetBitmap;

        private BitmapDataBuffer TargetBitmapData;

        private TimeSpan? CurrentBlockDuration;

        #endregion

        #region Constructors

        /// <summary>
        /// Initializes a new instance of the <see cref="VideoRenderer"/> class.
        /// </summary>
        /// <param name="mediaCore">The core media element.</param>
        public VideoRenderer(MediaEngine mediaCore)
        {
            MediaCore = mediaCore;

            // Check that the renderer supports the passed in Pixel format
            if (MediaPixelFormats.ContainsKey(Constants.VideoPixelFormat) == false)
                throw new NotSupportedException($"Unable to get equivalent pixel format from source: {Constants.VideoPixelFormat}");

            // Set the DPI
            Library.GuiContext.EnqueueInvoke(() =>
            {
                var visual = PresentationSource.FromVisual(MediaElement);
                DpiX = 96.0 * visual?.CompositionTarget?.TransformToDevice.M11 ?? 96.0;
                DpiY = 96.0 * visual?.CompositionTarget?.TransformToDevice.M22 ?? 96.0;
            });
        }

        #endregion

        #region Properties

        /// <inheritdoc />
        ILoggingHandler ILoggingSource.LoggingHandler => MediaCore;

        /// <summary>
        /// Gets the parent media element (platform specific).
        /// </summary>
        public MediaElement MediaElement => MediaCore?.Parent as MediaElement;

        /// <inheritdoc />
        public MediaEngine MediaCore { get; }

        /// <summary>
        /// Gets the DPI along the X axis.
        /// </summary>
        public double DpiX { get; private set; } = DefaultDpi;

        /// <summary>
        /// Gets the DPI along the Y axis.
        /// </summary>
        public double DpiY { get; private set; } = DefaultDpi;

        private Dispatcher VideoDispatcher => MediaElement?.VideoView?.ElementDispatcher;

        private Dispatcher ControlDispatcher => MediaElement?.Dispatcher;

        private WriteableBitmap TargetBitmap
        {
            get
            {
                return m_TargetBitmap;
            }
            set
            {
                m_TargetBitmap = value;
                TargetBitmapData = m_TargetBitmap != null
                    ? new BitmapDataBuffer(m_TargetBitmap)
                    : null;

                MediaElement.VideoView.Source = m_TargetBitmap;
                GC.Collect();
            }
        }

        #endregion

        #region MediaRenderer Methods

        /// <inheritdoc />
        public void OnPlay()
        {
            // placeholder
        }

        /// <inheritdoc />
        public void OnPause()
        {
            // placeholder
        }

        /// <inheritdoc />
        public void OnStop()
        {
            Library.GuiContext.EnqueueInvoke(() =>
                MediaElement.CaptionsView.Reset());
        }

        /// <inheritdoc />
        public void OnSeek()
        {
            Library.GuiContext.EnqueueInvoke(() =>
                MediaElement.CaptionsView.Reset());
        }

        /// <inheritdoc />
        public void OnStarting()
        {
            // placeholder
        }

        /// <inheritdoc />
        public void Update(TimeSpan clockPosition)
        {
            // placeholder
        }

        /// <inheritdoc />
        public void Render(MediaBlock mediaBlock, TimeSpan clockPosition)
        {
            if (mediaBlock is VideoBlock == false) return;

            var block = (VideoBlock)mediaBlock;

            // Send the packets to the CC renderer
            MediaElement?.CaptionsView?.SendPackets(block, MediaCore);

            // Send the packets to the CC renderer
            MediaElement?.CaptionsView?.SendPackets(block, MediaCore);

            if (VideoBlockElapsed.IsRunning && MediaCore.Timing.IsRunning && CurrentBlockDuration.HasValue && MediaCore.Timing.SpeedRatio == 1d)
            {
                var remainingDisplayTime = TimeSpan.FromTicks(CurrentBlockDuration.Value.Ticks - VideoBlockElapsed.Elapsed.Ticks);
                while (remainingDisplayTime.TotalMilliseconds > 0)
                {
                    remainingDisplayTime = TimeSpan.FromTicks(CurrentBlockDuration.Value.Ticks - VideoBlockElapsed.Elapsed.Ticks);
                    var remainingMillis = remainingDisplayTime.TotalMilliseconds;

                    if (remainingMillis < 2d)
                        continue;

                    Thread.Sleep(1);
                }
            }

            VideoBlockElapsed.Restart();
            CurrentBlockDuration = block.Duration;

            VideoDispatcher?.Invoke(() =>
            {
                // Apply frame rate limiter (if active)
                var frameRateLimit = MediaElement.RendererOptions.VideoRefreshRateLimit;
                var isRenderTime = frameRateLimit <= 0 || !RenderStopwatch.IsRunning || RenderStopwatch.ElapsedMilliseconds >= 1000d / frameRateLimit;
                if (!isRenderTime)
                    return;

                RenderStopwatch.Restart();

                // Prepare and write frame data
                if (PrepareVideoFrameBuffer(block))
                    WriteVideoFrameBuffer(block, clockPosition);

                ControlDispatcher?.InvokeAsync(() => UpdateLayout(block, clockPosition));
            });
        }

        /// <inheritdoc />
        public void OnClose()
        {
            Library.GuiContext.EnqueueInvoke(() =>
            {
                TargetBitmap = null;
                MediaElement.CaptionsView.Reset();

                // Force refresh
                MediaElement.VideoView?.InvokeAsync(DispatcherPriority.Render, () => { });
            });
        }

        #endregion

        /// <summary>
        /// Initializes the target bitmap if not available and returns a pointer to the back-buffer for filling.
        /// </summary>
        /// <param name="block">The block.</param>
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        private bool PrepareVideoFrameBuffer(VideoBlock block)
        {
            // Figure out what we need to do
            var needsCreation = (TargetBitmapData == null || TargetBitmap == null) && MediaElement.HasVideo;
            var needsModification = MediaElement.HasVideo && TargetBitmap != null && TargetBitmapData != null &&
                (TargetBitmapData.PixelWidth != block.PixelWidth ||
                TargetBitmapData.PixelHeight != block.PixelHeight ||
                TargetBitmapData.Stride != block.PictureBufferStride);

            var hasValidDimensions = block.PixelWidth > 0 && block.PixelHeight > 0;

            if ((!needsCreation && !needsModification) && hasValidDimensions)
                return TargetBitmapData != null;

            if (!hasValidDimensions)
            {
                TargetBitmap = null;
                return false;
            }

            // Instantiate or update the target bitmap
            TargetBitmap = new WriteableBitmap(
                block.PixelWidth, block.PixelHeight, DpiX, DpiY, MediaPixelFormats[Constants.VideoPixelFormat], null);

            return TargetBitmapData != null;
        }

        /// <summary>
        /// Loads that target data buffer with block data.
        /// </summary>
        /// <param name="block">The source.</param>
        /// <param name="clockPosition">Current clock position.</param>
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        private unsafe void WriteVideoFrameBuffer(VideoBlock block, TimeSpan clockPosition)
        {
            var bitmap = TargetBitmap;
            var target = TargetBitmapData;
            if (bitmap == null || target == null || block == null || block.IsDisposed || !block.TryAcquireReaderLock(out var readLock))
                return;

            try
            {
                bitmap.Lock();

                // Lock the video block for reading
                using (readLock)
                {
                    // Compute a safe number of bytes to copy
                    // At this point, we it is assumed the strides are equal
                    var bufferLength = Math.Min(block.BufferLength, target.BufferLength);

                    // Copy the block data into the back buffer of the target bitmap.
                    Buffer.MemoryCopy(
                        block.Buffer.ToPointer(),
                        target.Scan0.ToPointer(),
                        bufferLength,
                        bufferLength);

                    // with the locked video block, raise the rendering video event.
                    MediaElement?.RaiseRenderingVideoEvent(block, TargetBitmapData, clockPosition);
                }
            }
            finally
            {
                bitmap.AddDirtyRect(TargetBitmapData.UpdateRect);
                bitmap.Unlock();
            }
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        private void UpdateLayout(VideoBlock block, TimeSpan clockPosition)
        {
            try
            {
                MediaElement?.CaptionsView?.Render(MediaElement.ClosedCaptionsChannel, clockPosition);
                ApplyLayoutTransforms(block);
            }
            catch (Exception ex)
            {
                this.LogError(Aspects.VideoRenderer, $"{nameof(AsyncVideoRenderer)}.{nameof(Render)} layout/CC failed.", ex);
            }
        }

        /// <summary>
        /// Applies the scale transform according to the block's aspect ratio.
        /// </summary>
        /// <param name="b">The b.</param>
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        private void ApplyLayoutTransforms(VideoBlock b)
        {
            var videoView = MediaElement?.VideoView;
            if (videoView == null) return;

            ScaleTransform scaleTransform;
            RotateTransform rotateTransform;

            if (videoView.LayoutTransform is TransformGroup layoutTransforms)
            {
                scaleTransform = layoutTransforms.Children[0] as ScaleTransform;
                rotateTransform = layoutTransforms.Children[1] as RotateTransform;
            }
            else
            {
                layoutTransforms = new TransformGroup();
                scaleTransform = new ScaleTransform(1, 1);
                rotateTransform = new RotateTransform(0, 0.5, 0.5);
                layoutTransforms.Children.Add(scaleTransform);
                layoutTransforms.Children.Add(rotateTransform);

                videoView.LayoutTransform = layoutTransforms;
            }

            // return if no proper transforms were found
            if (scaleTransform == null || rotateTransform == null)
                return;

            // Check if we need to ignore pixel aspect ratio
            var ignoreAspectRatio = MediaElement?.IgnorePixelAspectRatio ?? false;

            // Process Aspect Ratio according to block.
            if (!ignoreAspectRatio && b.PixelAspectWidth != b.PixelAspectHeight)
            {
                var scaleX = b.PixelAspectWidth > b.PixelAspectHeight ? Convert.ToDouble(b.PixelAspectWidth) / Convert.ToDouble(b.PixelAspectHeight) : 1d;
                var scaleY = b.PixelAspectHeight > b.PixelAspectWidth ? Convert.ToDouble(b.PixelAspectHeight) / Convert.ToDouble(b.PixelAspectWidth) : 1d;

                if (Math.Abs(scaleTransform.ScaleX - scaleX) > double.Epsilon ||
                    Math.Abs(scaleTransform.ScaleY - scaleY) > double.Epsilon)
                {
                    scaleTransform.ScaleX = scaleX;
                    scaleTransform.ScaleY = scaleY;
                }
            }
            else
            {
                scaleTransform.ScaleX = 1d;
                scaleTransform.ScaleY = 1d;
            }

            // Process Rotation
            if (Math.Abs(MediaCore.State.VideoRotation - rotateTransform.Angle) > double.Epsilon)
                rotateTransform.Angle = MediaCore.State.VideoRotation;
        }
    }
}
#pragma warning restore CA1812