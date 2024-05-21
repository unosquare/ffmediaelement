namespace Unosquare.FFME.Container
{
    using FFmpeg.AutoGen;
    using System;
    using System.Text;

    /// <summary>
    /// Performs subtitle stream extraction, decoding and text conversion.
    /// </summary>
    /// <seealso cref="MediaComponent" />
    internal sealed unsafe class SubtitleComponent : MediaComponent
    {
        private static readonly char[] SeparatorChars = [','];

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
        /// Gets the amount of time to offset the subtitles by for this component.
        /// </summary>
        public TimeSpan Delay { get; }

        /// <inheritdoc />
        public override bool MaterializeFrame(MediaFrame input, ref MediaBlock output, MediaBlock previousBlock)
        {
            if (output == null) output = new SubtitleBlock();
            if (input is SubtitleFrame == false || output is SubtitleBlock == false)
                throw new ArgumentNullException($"{nameof(input)} and {nameof(output)} are either null or not of a compatible media type '{MediaType}'");

            var source = (SubtitleFrame)input;
            var target = (SubtitleBlock)output;

            // Set the target data
            target.PresentationTime = source.PresentationTime;
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
            {
                foreach (var t in source.Text)
                    target.OriginalText.Add(t);
            }

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

            return true;
        }

        #region Output Formatting

        /// <summary>
        /// Strips the SRT format and returns plain text.
        /// </summary>
        /// <param name="input">The input.</param>
        /// <returns>The formatted string.</returns>
        internal static string StripSrtFormat(string input)
        {
            var output = new StringBuilder(input.Length);
            var isInTag = false;
            char currentChar;

            for (var i = 0; i < input.Length; i++)
            {
                currentChar = input[i];
                if (currentChar == '<' && isInTag == false)
                {
                    isInTag = true;
                    continue;
                }

                if (currentChar == '>' && isInTag)
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
        /// <returns>The formatted string.</returns>
        internal static string StripAssFormat(string input)
        {
            const string DialoguePrefix = "dialogue:";

            if (!input.StartsWith(DialoguePrefix, StringComparison.InvariantCultureIgnoreCase))
                return string.Empty;

            var inputParts = input.Split(SeparatorChars, 10);
            if (inputParts.Length != 10)
                return string.Empty;

            var normalizedInput = inputParts[^1]
                .ReplaceOrdinal("\\n", " ")
                .ReplaceOrdinal("\\N", "\r\n");

            var builder = new StringBuilder(normalizedInput.Length);
            var isInStyle = false;
            char currentChar;

            for (var i = 0; i < normalizedInput.Length; i++)
            {
                currentChar = normalizedInput[i];
                if (currentChar == '{' && isInStyle == false)
                {
                    isInStyle = true;
                    continue;
                }

                if (currentChar == '}' && isInStyle)
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

        /// <inheritdoc />
        protected override MediaFrame CreateFrameSource(IntPtr framePointer)
        {
            var frame = (AVSubtitle*)framePointer;
            var frameHolder = new SubtitleFrame(frame, this);
            return frameHolder;
        }
    }
}
