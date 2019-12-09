namespace Unosquare.FFME.Rendering
{
    using Container;
    using Diagnostics;
    using Engine;
    using Platform;
    using Primitives;
    using System;
    using System.Diagnostics;
    using System.Runtime.CompilerServices;
    using System.Windows;
    using System.Windows.Media;
    using System.Windows.Threading;

    /// <summary>
    /// Provides basic infrastructure for video rendering.
    /// </summary>
    /// <seealso cref="IMediaRenderer" />
    /// <seealso cref="ILoggingSource" />
    internal abstract class VideoRendererBase : IMediaRenderer, ILoggingSource
    {
        /// <summary>
        /// The default dpi.
        /// </summary>
        protected const double DefaultDpi = 96.0;

        /// <summary>
        /// The WPF lock timeout equivalent to hald of the duration of a 60FPS cycle -- approximately 8ms).
        /// </summary>
        protected static readonly Duration WpfLockTimeout = new Duration(TimeSpan.FromMilliseconds(1000d / 60d / 2d));

        /// <summary>
        /// Set when a bitmap is being written to the target bitmap.
        /// </summary>
        private readonly AtomicBoolean m_IsRenderingInProgress = new AtomicBoolean(false);

        /// <summary>
        /// Keeps track of the elapsed time since the last frame was displayed.
        /// for frame limiting purposes.
        /// </summary>
        private readonly Stopwatch RenderStopwatch = new Stopwatch();

        /// <summary>
        /// Initializes a new instance of the <see cref="VideoRendererBase"/> class.
        /// </summary>
        /// <param name="mediaCore">The media core.</param>
        protected VideoRendererBase(MediaEngine mediaCore)
        {
            MediaCore = mediaCore;

            // Set the DPI
            Library.GuiContext.EnqueueInvoke(() =>
            {
                var media = MediaElement;
                if (media != null)
                {
                    var visual = PresentationSource.FromVisual(media);
                    DpiX = DefaultDpi * visual?.CompositionTarget?.TransformToDevice.M11 ?? DefaultDpi;
                    DpiY = DefaultDpi * visual?.CompositionTarget?.TransformToDevice.M22 ?? DefaultDpi;
                }
            });
        }

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

        /// <summary>
        /// Gets or sets a value indicating whether rendering is in progress.
        /// </summary>
        protected bool IsRenderingInProgress
        {
            get => m_IsRenderingInProgress.Value;
            set => m_IsRenderingInProgress.Value = value;
        }

        /// <summary>
        /// Gets the video dispatcher.
        /// </summary>
        protected Dispatcher VideoDispatcher => MediaElement?.VideoView?.ElementDispatcher;

        /// <summary>
        /// Gets the control dispatcher.
        /// </summary>
        protected Dispatcher ControlDispatcher => MediaElement?.Dispatcher;

        /// <summary>
        /// Gets a value indicating whether it is time to render after applying frame rate limiter.
        /// </summary>
        protected bool IsRenderTime
        {
            get
            {
                // Apply frame rate limiter (if active)
                var frameRateLimit = MediaElement.RendererOptions.VideoRefreshRateLimit;
                var result = frameRateLimit <= 0 || !RenderStopwatch.IsRunning || RenderStopwatch.ElapsedMilliseconds >= 1000d / frameRateLimit;
                return result;
            }
        }

        /// <inheritdoc />
        public virtual void OnPause()
        {
            // placeholder
        }

        /// <inheritdoc />
        public virtual void OnPlay()
        {
            // placeholder
        }

        /// <inheritdoc />
        public virtual void OnClose() => ClearVideo();

        /// <inheritdoc />
        public virtual void OnSeek() => ClearCaptions();

        /// <inheritdoc />
        public virtual void OnStarting()
        {
            // placeholder
        }

        /// <inheritdoc />
        public virtual void Update(TimeSpan clockPosition)
        {
            // placeholder
        }

        /// <inheritdoc />
        public abstract void Render(MediaBlock mediaBlock, TimeSpan clockPosition);

        /// <inheritdoc />
        public virtual void OnStop() => ClearCaptions();

        /// <summary>
        /// Clears the video and captions.
        /// </summary>
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        protected void ClearVideo()
        {
            // Force captions reset in the background so it's the last thing processed.
            ClearCaptions();

            // Force image source refresh in the background so it's the last thing processed.
            VideoDispatcher?.InvokeAsync(() =>
            {
                var videoView = MediaElement.VideoView;
                if (videoView != null)
                    videoView.Source = null;
            }, DispatcherPriority.Background);
        }

        /// <summary>
        /// Clears the captions.
        /// </summary>
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        protected void ClearCaptions() =>
            ControlDispatcher?.InvokeAsync(() => MediaElement?.CaptionsView?.Reset(), DispatcherPriority.Background);

        /// <summary>
        /// Begins the rendering cycle.
        /// </summary>
        /// <param name="mediaBlock">The media block.</param>
        /// <returns>The block for rendering. Returns null of not ready.</returns>
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        protected VideoBlock BeginRenderingCycle(MediaBlock mediaBlock)
        {
            if (mediaBlock is VideoBlock == false) return null;

            var block = (VideoBlock)mediaBlock;
            if (IsRenderingInProgress)
            {
                if (MediaCore?.State.IsPlaying ?? false)
                    this.LogDebug(Aspects.VideoRenderer, $"{nameof(VideoRenderer)} frame skipped at {mediaBlock.StartTime}");

                return null;
            }

            // Flag the start of a rendering cycle
            IsRenderingInProgress = true;

            // Send the packets to the CC renderer
            MediaElement?.CaptionsView?.SendPackets(block, MediaCore);

            if (!IsRenderTime)
                return null;
            else
                RenderStopwatch.Restart();

            // Return block for rendering
            return block;
        }

        /// <summary>
        /// Finishes the rendering cycle.
        /// </summary>
        /// <param name="block">The block.</param>
        /// <param name="clockPosition">The clock position.</param>
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        protected void FinishRenderingCycle(VideoBlock block, TimeSpan clockPosition)
        {
            // Alwasy set the progress to false to allow for next cycle.
            IsRenderingInProgress = false;

            // Update the layout including pixel ratio and video rotation
            ControlDispatcher?.InvokeAsync(() =>
                UpdateLayout(block, clockPosition), DispatcherPriority.Render);
        }

        /// <summary>
        /// Updates the layout.
        /// </summary>
        /// <param name="block">The block.</param>
        /// <param name="clockPosition">The clock position.</param>
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
                this.LogError(Aspects.VideoRenderer, $"{nameof(VideoRenderer)}.{nameof(Render)} layout/CC failed.", ex);
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
