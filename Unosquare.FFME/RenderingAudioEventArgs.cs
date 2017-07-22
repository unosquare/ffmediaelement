namespace Unosquare.FFME
{
    using System;

    /// <summary>
    /// The audio rendering event arguments
    /// </summary>
    /// <seealso cref="System.EventArgs" />
    public class RenderingAudioEventArgs : EventArgs
    {

        /// <summary>
        /// Initializes a new instance of the <see cref="RenderingAudioEventArgs"/> class.
        /// </summary>
        /// <param name="buffer">The buffer.</param>
        /// <param name="length">The length.</param>
        /// <param name="position">The position.</param>
        public RenderingAudioEventArgs(IntPtr buffer, int length, TimeSpan position)
            : base()
        {
            Buffer = buffer;
            Length = length;
            Position = position;
        }

        /// <summary>
        /// Gets the clock position on which this rendering event was fired.
        /// </summary>
        public TimeSpan Position { get; }

        /// <summary>
        /// Gets a pointer to the samples buffer
        /// </summary>
        public IntPtr Buffer { get; }

        /// <summary>
        /// Gets the length of the samples buffer.
        /// </summary>
        public int Length { get; }
    }
}
