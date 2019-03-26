namespace Unosquare.FFME.Rendering
{
    using Engine;
    using FFmpeg.AutoGen;
    using Primitives;
    using System;
    using System.Collections.Generic;
    using System.IO;
    using System.Runtime.InteropServices.WindowsRuntime;
    using Unosquare.FFME.Platform;
    using Windows.Media.MediaProperties;
    using Windows.UI.Xaml.Media.Imaging;

    internal sealed class VideoRenderer : BaseRenderer
    {
        /// <summary>
        /// Contains an equivalence lookup of FFmpeg pixel format and WPF pixel formats.
        /// </summary>
        private static readonly Dictionary<AVPixelFormat, MediaPixelFormat> MediaPixelFormats = new Dictionary<AVPixelFormat, MediaPixelFormat>
        {
            { AVPixelFormat.AV_PIX_FMT_BGR0, MediaPixelFormat.Bgra8 },
            { AVPixelFormat.AV_PIX_FMT_BGRA, MediaPixelFormat.Bgra8 }
        };

        /// <summary>
        /// Set when a bitmap is being written to the target bitmap
        /// </summary>
        private readonly AtomicBoolean IsRenderingInProgress = new AtomicBoolean(false);

        /// <summary>
        /// The bitmap that is presented to the user.
        /// </summary>
        private WriteableBitmap TargetBitmap;

        /// <summary>
        /// Initializes a new instance of the <see cref="VideoRenderer"/> class.
        /// </summary>
        /// <param name="mediaCore">The core media element.</param>
        public VideoRenderer(MediaEngine mediaCore)
            : base(mediaCore)
        {
            // Check that the renderer supports the passed in Pixel format
            if (MediaPixelFormats.ContainsKey(Constants.VideoPixelFormat) == false)
                throw new NotSupportedException($"Unable to get equivalent pixel format from source: {Constants.VideoPixelFormat}");
        }

        public override unsafe void Render(MediaBlock mediaBlock, TimeSpan clockPosition)
        {
            if (IsRenderingInProgress == true)
                return;

            GuiContext.Current.EnqueueInvoke(() =>
            {
                IDisposable readLock = null;

                try
                {
                    var videoBlock = mediaBlock as VideoBlock;
                    if (videoBlock == null || !videoBlock.TryAcquireReaderLock(out readLock))
                        return;

                    if (!SetupTargetBitmap(videoBlock))
                        return;

                    var videoBlockStream = new UnmanagedMemoryStream((byte*)videoBlock.Buffer.ToPointer(), videoBlock.BufferLength);
                    var pixelDataStream = TargetBitmap.PixelBuffer.AsStream();
                    videoBlockStream.CopyTo(pixelDataStream);
                }
                finally
                {
                    readLock?.Dispose();
                    IsRenderingInProgress.Value = false;
                }
            });
        }

        private bool SetupTargetBitmap(VideoBlock block)
        {
            // Figure out what we need to do
            var needsCreation = TargetBitmap == null && MediaElement.HasVideo;
            var needsModification = MediaElement.HasVideo
                && TargetBitmap != null
                && (TargetBitmap.PixelWidth != block.PixelWidth || TargetBitmap.PixelHeight != block.PixelHeight);

            var hasValidDimensions = block.PixelWidth > 0 && block.PixelHeight > 0;

            // Instantiate or update the target bitmap
            if ((needsCreation || needsModification) && hasValidDimensions)
            {
                TargetBitmap = new WriteableBitmap(block.PixelWidth, block.PixelHeight);
            }
            else if (hasValidDimensions == false)
            {
                TargetBitmap = null;
            }

            // Update the target ViewBox image if not already set
            if (MediaElement.VideoView.Source != TargetBitmap)
                MediaElement.VideoView.Source = TargetBitmap;

            return TargetBitmap != null;
        }
    }
}
