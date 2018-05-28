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
            { AVPixelFormat.AV_PIX_FMT_BGR0, PixelFormats.Bgr32 },
            { AVPixelFormat.AV_PIX_FMT_BGRA, PixelFormats.Bgra32 }
        };

        /// <summary>
        /// The bitmap that is presented to the user.
        /// </summary>
        private WriteableBitmap TargetBitmap = null;

        /// <summary>
        /// Set when a bitmap is being written to the target bitmap
        /// </summary>
        private AtomicBoolean IsRenderingInProgress = new AtomicBoolean(false);

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
            GuiContext.Current.EnqueueInvoke(() =>
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
            GuiContext.Current.EnqueueInvoke(() =>
            {
                // TODO: Formalize this
                MediaElement.CaptionsView.ResetState();
            });
        }

        /// <summary>
        /// Executed after a Seek operation is performed on the parent MediaElement
        /// </summary>
        public void Seek()
        {
            GuiContext.Current.EnqueueInvoke(() =>
            {
                // TODO: Formalize this
                MediaElement.CaptionsView.ResetState();
            });
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

            // Create an action that holds GUI thread actions
            var foregroundAction = new Action(() =>
            {
                MediaElement.CaptionsView.RenderPacket(block, MediaCore);
                ApplyLayoutTransforms(block);
            });

            var canStartForegroundTask = MediaElement.VideoView.ElementDispatcher != MediaElement.Dispatcher;
            var foregroundTask = canStartForegroundTask ?
                MediaElement.Dispatcher.InvokeAsync(foregroundAction) : null;

            // Ensure the target bitmap can be loaded
            MediaElement.VideoView.InvokeAsync(DispatcherPriority.Render, () =>
            {
                if (block.IsDisposed)
                {
                    IsRenderingInProgress.Value = false;
                    return;
                }

                // Run the foreground action if we could not start it in parallel.
                if (foregroundTask == null)
                {
                    try
                    {
                        foregroundAction();
                    }
                    catch (Exception ex)
                    {
                        MediaElement?.MediaCore?.Log(
                            MediaLogMessageType.Error,
                            $"{nameof(VideoRenderer)} {ex.GetType()}: {nameof(Render)} layout/CC failed. {ex.Message}.");
                    }
                }

                try
                {
                    // Render the bitmap data
                    var bitmapData = LockTargetBitmap(block);
                    if (bitmapData != null)
                    {
                        LoadTargetBitmapBuffer(bitmapData, block);
                        MediaElement.RaiseRenderingVideoEvent(block, bitmapData, clockPosition);
                        RenderTargetBitmap(block, bitmapData, clockPosition);
                    }
                }
                catch (Exception ex)
                {
                    MediaElement?.MediaCore?.Log(
                        MediaLogMessageType.Error,
                        $"{nameof(VideoRenderer)} {ex.GetType()}: {nameof(Render)} bitmap failed. {ex.Message}.");
                }
                finally
                {
                    if (foregroundTask != null)
                    {
                        try
                        {
                            foregroundTask?.Wait();
                        }
                        catch (Exception ex)
                        {
                            MediaElement?.MediaCore?.Log(
                                MediaLogMessageType.Error,
                                $"{nameof(VideoRenderer)} {ex.GetType()}: {nameof(Render)} layout/CC failed. {ex.Message}.");
                        }
                    }

                    // Always reset the rendering state
                    IsRenderingInProgress.Value = false;
                }
            });
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

                // TODO: This still needs work!
                MediaElement.CaptionsView.ResetState();
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
                // Signal an update on the rendering surface
                TargetBitmap?.AddDirtyRect(bitmapData.UpdateRect);
                TargetBitmap?.Unlock();
            }
            catch (Exception ex)
            {
                MediaElement?.MediaCore?.Log(
                    MediaLogMessageType.Error,
                    $"{nameof(VideoRenderer)} {ex.GetType()}: {ex.Message}. Stack Trace:\r\n{ex.StackTrace}");
            }
        }

        /// <summary>
        /// Initializes the target bitmap if not available and locks it for loading the back-buffer.
        /// This method needs to be called from the GUI thread.
        /// </summary>
        /// <param name="block">The block.</param>
        /// <returns>
        /// The locking result. Returns a null pointer on back buffer for invalid.
        /// </returns>
        private BitmapDataBuffer LockTargetBitmap(VideoBlock block)
        {
            // Result will be set on the GUI thread
            BitmapDataBuffer result = null;

            // Skip the locking if scrubbing is not enabled
            // if (MediaElement.ScrubbingEnabled == false && (MediaElement.IsPlaying == false || MediaElement.IsSeeking))
            //     return result;

            // Figure out what we need to do
            var needsCreation = TargetBitmap == null && MediaElement.HasVideo;
            var needsModification = TargetBitmap != null
                && needsCreation == false
                && (TargetBitmap.PixelWidth != block.PixelWidth || TargetBitmap.PixelHeight != block.PixelHeight);

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
            if (TargetBitmap == null) return result;

            // Lock the back-buffer and create a pointer to it
            TargetBitmap.Lock();
            result = BitmapDataBuffer.FromWriteableBitmap(TargetBitmap);

            return result;
        }

        /// <summary>
        /// Loads that target data buffer with block data
        /// </summary>
        /// <param name="target">The target.</param>
        /// <param name="source">The source.</param>
        private void LoadTargetBitmapBuffer(BitmapDataBuffer target, VideoBlock source)
        {
            // Compute a safe number of bytes to copy
            // At this point, we it is assumed the strides are equal
            var bufferLength = Convert.ToUInt32(Math.Min(source.BufferLength, target.BufferLength));

            // Copy the block data into the back buffer of the target bitmap.
            WindowsNativeMethods.Instance.CopyMemory(target.Scan0, source.Buffer, bufferLength);
        }

        /// <summary>
        /// Applies the scale transform according to the block's aspect ratio.
        /// </summary>
        /// <param name="b">The b.</param>
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        private void ApplyLayoutTransforms(VideoBlock b)
        {
            var layoutTransforms = MediaElement.VideoView.LayoutTransform as TransformGroup;
            ScaleTransform scaleTransform = null;
            RotateTransform rotateTransform = null;

            if (layoutTransforms == null)
            {
                layoutTransforms = new TransformGroup();
                scaleTransform = new ScaleTransform(1, 1);
                rotateTransform = new RotateTransform(0, 0.5, 0.5);
                layoutTransforms.Children.Add(scaleTransform);
                layoutTransforms.Children.Add(rotateTransform);

                MediaElement.VideoView.LayoutTransform = layoutTransforms;
            }
            else
            {
                scaleTransform = layoutTransforms.Children[0] as ScaleTransform;
                rotateTransform = layoutTransforms.Children[1] as RotateTransform;
            }

            // Process Aspect Ratio according to block.
            if (b.AspectWidth != b.AspectHeight)
            {
                var scaleX = b.AspectWidth > b.AspectHeight ? Convert.ToDouble(b.AspectWidth) / Convert.ToDouble(b.AspectHeight) : 1d;
                var scaleY = b.AspectHeight > b.AspectWidth ? Convert.ToDouble(b.AspectHeight) / Convert.ToDouble(b.AspectWidth) : 1d;

                if (scaleTransform.ScaleX != scaleX || scaleTransform.ScaleY != scaleY)
                {
                    scaleTransform.ScaleX = scaleX;
                    scaleTransform.ScaleY = scaleY;
                }
            }
            else
            {
                if (scaleTransform.ScaleX != 1d || scaleTransform.ScaleY != 1d)
                {
                    scaleTransform.ScaleX = 1d;
                    scaleTransform.ScaleY = 1d;
                }
            }

            // Process Rotation
            if (MediaCore.State.VideoRotation != rotateTransform.Angle)
                rotateTransform.Angle = MediaCore.State.VideoRotation;
        }
    }
}
