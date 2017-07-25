namespace Unosquare.FFME.Decoding
{
    using FFmpeg.AutoGen;
    using System;
    using Unosquare.FFME.Core;

    /// <summary>
    /// Performs subtitle stream extraction, decoding and text conversion.
    /// </summary>
    /// <seealso cref="Unosquare.FFME.MediaComponent" />
    internal sealed unsafe class SubtitleComponent : MediaComponent
    {
        /// <summary>
        /// Initializes a new instance of the <see cref="SubtitleComponent"/> class.
        /// </summary>
        /// <param name="container">The container.</param>
        /// <param name="streamIndex">Index of the stream.</param>
        internal SubtitleComponent(MediaContainer container, int streamIndex)
            : base(container, streamIndex)
        {
            // placeholder. Nothing else to change here.
        }

        /// <summary>
        /// Creates a frame source object given the raw FFmpeg subtitle reference.
        /// </summary>
        /// <param name="frame">The raw FFmpeg subtitle pointer.</param>
        /// <returns></returns>
        protected override unsafe MediaFrame CreateFrameSource(AVSubtitle* frame)
        {
            var frameHolder = new SubtitleFrame(frame, this);
            return frameHolder;
        }

        /// <summary>
        /// Converts decoded, raw frame data in the frame source into a a usable frame. <br />
        /// The process includes performing picture, samples or text conversions
        /// so that the decoded source frame data is easily usable in multimedia applications
        /// </summary>
        /// <param name="input">The source frame to use as an input.</param>
        /// <param name="output">The target frame that will be updated with the source frame. If null is passed the frame will be instantiated.</param>
        /// <returns>
        /// Return the updated output frame
        /// </returns>
        /// <exception cref="System.ArgumentNullException">input</exception>
        internal override MediaBlock MaterializeFrame(MediaFrame input, ref MediaBlock output)
        {
            if (output == null) output = new SubtitleBlock();
            var source = input as SubtitleFrame;
            var target = output as SubtitleBlock;

            if (source == null || target == null)
                throw new ArgumentNullException($"{nameof(input)} and {nameof(output)} are either null or not of a compatible media type '{MediaType}'");

            // Set the target data
            target.EndTime = source.EndTime;
            target.StartTime = source.StartTime;
            target.Duration = source.Duration;
            target.StreamIndex = input.StreamIndex;

            target.OriginalText.Clear();
            if (source.Text.Count > 0)
                target.OriginalText.AddRange(source.Text);
            target.OriginalTextType = source.TextType;

            target.Text.Clear();
            foreach (var text in source.Text)
            {
                if (string.IsNullOrWhiteSpace(text))
                    continue;

                if (source.TextType == AVSubtitleType.SUBTITLE_ASS)
                {
                    var strippedText = text.StripAssFormat();
                    if (string.IsNullOrWhiteSpace(strippedText) == false)
                        target.Text.Add(strippedText);
                }
                else
                {
                    var strippedText = text.StripSrtFormat();
                    if (string.IsNullOrWhiteSpace(strippedText) == false)
                        target.Text.Add(strippedText);
                }
            }

            return target;
        }

    }

}
