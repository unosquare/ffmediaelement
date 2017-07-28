namespace Unosquare.FFME
{
    using Core;
    using FFmpeg.AutoGen;
    using System;
    using System.Collections.Generic;
    using System.Windows.Media.Imaging;
    using Decoding;
    using System.Runtime.CompilerServices;

    partial class MediaElement
    {

        #region Events

        /// <summary>
        /// Occurs right before the video is presented on the screen.
        /// You can update the pizels on the bitmap before it is rendered on the screen.
        /// Or you could take a screenshot.
        /// Ensure you handle this very quickly as it runs on the UI thread.
        /// </summary>
        public event EventHandler<RenderingVideoEventArgs> RenderingVideo;

        /// <summary>
        /// Occurs right before the audio is added to the audio buffer.
        /// You can update the bytes before they are enqueued.
        /// Ensure you handle this quickly before you get choppy audio.
        /// </summary>
        public event EventHandler<RenderingAudioEventArgs> RenderingAudio;

        /// <summary>
        /// Occurs right before the subtitles are rendered.
        /// You can update the text.
        /// Ensure you handle this quickly before you get choppy subtitles.
        /// </summary>
        public event EventHandler<RenderingSubtitlesEventArgs> RenderingSubtitles;

        #endregion

        #region Event Raisers

        /// <summary>
        /// Raises the rendering video event.
        /// </summary>
        /// <param name="bitmap">The bitmap.</param>
        /// <param name="stream">The stream.</param>
        /// <param name="startTime">The start time.</param>
        /// <param name="duration">The duration.</param>
        /// <param name="clock">The clock.</param>
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        internal void RaiseRenderingVideoEvent(WriteableBitmap bitmap, StreamInfo stream, TimeSpan startTime, TimeSpan duration, TimeSpan clock)
        {
            RenderingVideo?.Invoke(this, new RenderingVideoEventArgs(bitmap, stream, startTime, duration, clock));
        }

        /// <summary>
        /// Raises the rendering audio event.
        /// </summary>
        /// <param name="audioBlock">The audio block.</param>
        /// <param name="clock">The clock.</param>
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        internal void RaiseRenderingAudioEvent(AudioBlock audioBlock, TimeSpan clock)
        {
            RenderingAudio?.Invoke(this, new RenderingAudioEventArgs(audioBlock.Buffer, audioBlock.BufferLength, 
                Container.MediaInfo.Streams[audioBlock.StreamIndex], audioBlock.StartTime, audioBlock.Duration, clock));
        }



        /// <summary>
        /// Raises the rendering subtitles event.
        /// </summary>
        /// <param name="block">The block.</param>
        /// <param name="clock">The clock.</param>
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        internal void RaiseRenderingSubtitlesEvent(SubtitleBlock block, TimeSpan clock)
        {
            RenderingSubtitles?.Invoke(this, new RenderingSubtitlesEventArgs(block.Text, block.OriginalText, block.OriginalTextType, 
                Container.MediaInfo.Streams[block.StreamIndex], block.StartTime, block.Duration, clock));
        }

        #endregion

    }

    #region Event Classes

    /// <summary>
    /// A base class to represent media block
    /// rendering event arguments.
    /// </summary>
    /// <seealso cref="System.EventArgs" />
    public abstract class RenderingEventArgs : EventArgs
    {
        /// <summary>
        /// Initializes a new instance of the <see cref="RenderingEventArgs" /> class.
        /// </summary>
        /// <param name="stream">The stream.</param>
        /// <param name="startTime">The position.</param>
        /// <param name="duration">The duration.</param>
        /// <param name="clock">The clock.</param>
        protected RenderingEventArgs(StreamInfo stream, TimeSpan startTime, TimeSpan duration, TimeSpan clock)
        {
            StartTime = startTime;
            Duration = duration;
            Clock = clock;
            Stream = stream;
        }

        /// <summary>
        /// Provides Stream Information coming from the media container.
        /// </summary>
        public StreamInfo Stream { get; }

        /// <summary>
        /// Gets the clock position at which the media
        /// was called for rendering
        /// </summary>
        public TimeSpan Clock { get; }

        /// <summary>
        /// Gets the starting time at which this media
        /// has to be presented.
        /// </summary>
        public TimeSpan StartTime { get; }

        /// <summary>
        /// Gets how long this media has to be presented.
        /// </summary>
        public TimeSpan Duration { get; }
    }

    /// <summary>
    /// Provides the audio samples rendering payload as event arguments.
    /// </summary>
    /// <seealso cref="System.EventArgs" />
    public sealed class RenderingAudioEventArgs : RenderingEventArgs
    {

        /// <summary>
        /// Initializes a new instance of the <see cref="RenderingAudioEventArgs" /> class.
        /// </summary>
        /// <param name="buffer">The buffer.</param>
        /// <param name="length">The length.</param>
        /// <param name="stream">The stream.</param>
        /// <param name="startTime">The start time.</param>
        /// <param name="duration">The duration.</param>
        /// <param name="clock">The clock.</param>
        internal RenderingAudioEventArgs(IntPtr buffer, int length, StreamInfo stream, TimeSpan startTime, TimeSpan duration, TimeSpan clock)
            : base(stream, startTime, duration, clock)
        {
            Buffer = buffer;
            BufferLength = length;
            SampleRate = AudioParams.Output.SampleRate;
            ChannelCount = AudioParams.Output.ChannelCount;
            BitsPerSample = AudioParams.OutputBitsPerSample;
        }

        /// <summary>
        /// Gets a pointer to the samples buffer.
        /// Samples are provided in PCM 16-bit signed, interleaved stereo.
        /// </summary>
        public IntPtr Buffer { get; }

        /// <summary>
        /// Gets the length in bytes of the samples buffer.
        /// </summary>
        public int BufferLength { get; }

        /// <summary>
        /// Gets the number of samples in 1 second.
        /// </summary>
        public int SampleRate { get; }

        /// <summary>
        /// Gets the number of channels.
        /// </summary>
        public int ChannelCount { get; }

        /// <summary>
        /// Gets the number of bits per sample.
        /// </summary>
        public int BitsPerSample { get; }

        /// <summary>
        /// Gets the number of samples in the buffer for all channels.
        /// </summary>
        public int Samples { get { return BufferLength / (BitsPerSample / 8); } }

        /// <summary>
        /// Gets the number of samples in the buffer per channel.
        /// </summary>
        public int SamplesPerChannel { get { return Samples / ChannelCount; } }
    }

    /// <summary>
    /// Provides the subtitles rendering payload as event arguments.
    /// </summary>
    /// <seealso cref="System.EventArgs" />
    public sealed class RenderingSubtitlesEventArgs : RenderingEventArgs
    {

        /// <summary>
        /// Initializes a new instance of the <see cref="RenderingSubtitlesEventArgs" /> class.
        /// </summary>
        /// <param name="text">The text.</param>
        /// <param name="originalText">The original text.</param>
        /// <param name="format">The format.</param>
        /// <param name="stream">The stream.</param>
        /// <param name="startTime">The start time.</param>
        /// <param name="duration">The duration.</param>
        /// <param name="clock">The clock.</param>
        internal RenderingSubtitlesEventArgs(List<string> text, List<string> originalText, AVSubtitleType format,
            StreamInfo stream, TimeSpan startTime, TimeSpan duration, TimeSpan clock)
            : base(stream, startTime, duration, clock)
        {
            Text = text;
            Format = format;
            OriginalText = originalText;
        }

        /// <summary>
        /// Gets the text stripped out of ASS or SRT formatting.
        /// This is what the default subtitle renderer will display
        /// on the screen.
        /// </summary>
        public List<string> Text { get; }

        /// <summary>
        /// Gets the text as originally decoded including
        /// all markup and formatting.
        /// </summary>
        public List<string> OriginalText { get; }

        /// <summary>
        /// Gets the type of subtitle format the original
        /// subtitle text is in.
        /// </summary>
        public AVSubtitleType Format { get; }

    }


    /// <summary>
    /// The video rendering event arguments
    /// </summary>
    /// <seealso cref="System.EventArgs" />
    public sealed class RenderingVideoEventArgs : RenderingEventArgs
    {

        /// <summary>
        /// Initializes a new instance of the <see cref="RenderingVideoEventArgs" /> class.
        /// </summary>
        /// <param name="bitmap">The bitmap.</param>
        /// <param name="stream">The stream.</param>
        /// <param name="startTime">The start time.</param>
        /// <param name="duration">The duration.</param>
        /// <param name="clock">The clock.</param>
        internal RenderingVideoEventArgs(WriteableBitmap bitmap, StreamInfo stream, TimeSpan startTime, TimeSpan duration, TimeSpan clock)
            : base(stream, startTime, duration, clock)
        {
            Bitmap = bitmap;
        }

        /// <summary>
        /// Gets the writable bitmap filled with the video frame pixels.
        /// Feel free to capture or change this image.
        /// </summary>
        public WriteableBitmap Bitmap { get; }

    }

    #endregion

}
