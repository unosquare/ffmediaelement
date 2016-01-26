namespace Unosquare.FFmpegMediaElement
{
    using System.Collections.Generic;
    using System.Linq;

    partial class FFmpegMedia
    {

        /// <summary>
        /// Provides audio data samples in PCM, 16-bit format.
        /// This class is used to keep track of the samples and matching frames times
        /// It also ensures samples are not repeated and are unique in a discrete timeline
        /// </summary>
        private class AudioBufferProvider
        {
            private readonly Dictionary<decimal, bool> ContainedFrameTimes = new Dictionary<decimal, bool>();
            private readonly List<byte> AudioBuffer = new List<byte>();
            private readonly FFmpegMedia Media;

            /// <summary>
            /// Initializes a new instance of the <see cref="AudioBufferProvider"/> class.
            /// </summary>
            /// <param name="media">The media.</param>
            public AudioBufferProvider(FFmpegMedia media)
            {
                this.Media = media;
            }

            /// <summary>
            /// Clears the buffer and frame times
            /// </summary>
            public void Clear()
            {
                AudioBuffer.Clear();
                ContainedFrameTimes.Clear();
            }

            /// <summary>
            /// Provides the next small buffer that the audio device requests.
            /// the buffer to fill is a non-null reference to the buffer that needs to be filled
            /// The return value represents how many bytes were written to buffer.
            /// </summary>
            public int ProvideNext(byte[] bufferToFill)
            {
                var renderTime = Media.RealtimeClock.PositionSeconds;
                var audioBufferTargetLength = bufferToFill.Length;
                var renderFrame = Media.AudioFramesCache.GetFrame(renderTime, true);
                var startFrameIndex = Media.AudioFramesCache.IndexOf(renderFrame);

                if (renderFrame == null)
                {
                    AudioBuffer.Clear();
                    ContainedFrameTimes.Clear();
                    return 0;
                }

                while (AudioBuffer.Count < audioBufferTargetLength)
                {
                    renderFrame = Media.AudioFramesCache.GetFrameAt(startFrameIndex);
                    startFrameIndex++;

                    if (renderFrame == null)
                    {
                        var emptyBuffer = new byte[bufferToFill.Length];
                        AudioBuffer.AddRange(emptyBuffer);
                        continue;
                    }

                    if (ContainedFrameTimes.ContainsKey(renderFrame.StartTime))
                        continue;

                    AudioBuffer.AddRange(renderFrame.AudioBuffer);
                    ContainedFrameTimes.Add(renderFrame.StartTime, true);

                }

                const decimal keepThreshold = 0.5M;
                var keyFrames = ContainedFrameTimes.Keys.ToArray();
                foreach (var frameTime in keyFrames)
                {
                    if (frameTime < renderTime - keepThreshold || frameTime > renderTime + keepThreshold)
                        ContainedFrameTimes.Remove(frameTime);
                }

                if (Media.IsPlaying && Media.SpeedRatio >= Constants.DefaultSpeedRatio)
                {
                    AudioBuffer.CopyTo(0, bufferToFill, 0, bufferToFill.Length);
                    AudioBuffer.RemoveRange(0, bufferToFill.Length);
                    return bufferToFill.Length;
                }
                else
                {
                    // Write out all zeroes if we don't need to play any sound
                    var silenceBuffer = new byte[bufferToFill.Length];
                    silenceBuffer.CopyTo(bufferToFill, 0);
                    return bufferToFill.Length;
                    //return 0;
                }

            }

        }

    }
}
