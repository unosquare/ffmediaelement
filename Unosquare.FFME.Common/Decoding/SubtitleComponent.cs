namespace Unosquare.FFME.Decoding
{
    using FFmpeg.AutoGen;
    using Shared;
    using System;
    using System.Collections.Generic;
    using System.Text;

    /// <summary>
    /// Performs subtitle stream extraction, decoding and text conversion.
    /// </summary>
    /// <seealso cref="MediaComponent" />
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
            // Adjust the offset according to options
            Delay = container.MediaOptions.SubtitlesDelay;
        }

        /// <summary>
        /// Gets the amount of time to offset the subtitles by for this component
        /// </summary>
        public TimeSpan Delay { get; } = TimeSpan.Zero;

        /// <summary>
        /// Converts decoded, raw frame data in the frame source into a a usable frame. <br />
        /// The process includes performing picture, samples or text conversions
        /// so that the decoded source frame data is easily usable in multimedia applications
        /// </summary>
        /// <param name="input">The source frame to use as an input.</param>
        /// <param name="output">The target frame that will be updated with the source frame. If null is passed the frame will be instantiated.</param>
        /// <param name="siblings">The sibling blocks that may help guess some additional parameters for the input frame.</param>
        /// <returns>
        /// Return the updated output frame
        /// </returns>
        /// <exception cref="ArgumentNullException">input cannot be null</exception>
        public override MediaBlock MaterializeFrame(MediaFrame input, ref MediaBlock output, List<MediaBlock> siblings)
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

            // Process time offsets
            if (Delay != TimeSpan.Zero)
            {
                target.StartTime = TimeSpan.FromTicks(target.StartTime.Ticks + Delay.Ticks);
                target.EndTime = TimeSpan.FromTicks(target.EndTime.Ticks + Delay.Ticks);
                target.Duration = TimeSpan.FromTicks(target.EndTime.Ticks - target.StartTime.Ticks);
            }

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
                    var strippedText = StripAssFormat(text);
                    if (string.IsNullOrWhiteSpace(strippedText) == false)
                        target.Text.Add(strippedText);
                }
                else
                {
                    var strippedText = StripSrtFormat(text);
                    if (string.IsNullOrWhiteSpace(strippedText) == false)
                        target.Text.Add(strippedText);
                }
            }

            return target;
        }

        #region Output Formatting

        /// <summary>
        /// Strips the SRT format and returns plain text.
        /// </summary>
        /// <param name="input">The input.</param>
        /// <returns>The formatted string</returns>
        internal static string StripSrtFormat(string input)
        {
            var output = new StringBuilder(input.Length);
            var isInTag = false;
            var currentChar = default(char);

            for (var i = 0; i < input.Length; i++)
            {
                currentChar = input[i];
                if (currentChar == '<' && isInTag == false)
                {
                    isInTag = true;
                    continue;
                }

                if (currentChar == '>' && isInTag == true)
                {
                    isInTag = false;
                    continue;
                }

                output.Append(currentChar);
            }

            return output.ToString();
        }

        /// <summary>
        /// Strips a line of text from the ASS format.
        /// </summary>
        /// <param name="input">The input.</param>
        /// <returns>The formatted string</returns>
        internal static string StripAssFormat(string input)
        {
            const string DialoguePrefix = "dialogue:";

            if (input.Substring(0, DialoguePrefix.Length).ToLowerInvariant().Equals(DialoguePrefix) == false)
                return string.Empty;

            var inputParts = input.Split(new char[] { ',' }, 10);
            if (inputParts.Length != 10)
                return string.Empty;

            input = inputParts[inputParts.Length - 1].Replace("\\n", " ").Replace("\\N", "\r\n");
            var builder = new StringBuilder(input.Length);
            var isInStyle = false;
            var currentChar = default(char);

            for (var i = 0; i < input.Length; i++)
            {
                currentChar = input[i];
                if (currentChar == '{' && isInStyle == false)
                {
                    isInStyle = true;
                    continue;
                }

                if (currentChar == '}' && isInStyle == true)
                {
                    isInStyle = false;
                    continue;
                }

                if (isInStyle == false)
                    builder.Append(currentChar);
            }

            return builder.ToString().Trim();
        }

        #endregion

        /// <summary>
        /// Creates a frame source object given the raw FFmpeg subtitle reference.
        /// </summary>
        /// <param name="frame">The raw FFmpeg subtitle pointer.</param>
        /// <returns>The managed frame</returns>
        protected override unsafe MediaFrame CreateFrameSource(AVSubtitle* frame)
        {
            var frameHolder = new SubtitleFrame(frame, this);
            return frameHolder;
        }
    }
}
