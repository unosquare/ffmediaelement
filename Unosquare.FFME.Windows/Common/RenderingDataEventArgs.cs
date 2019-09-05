namespace Unosquare.FFME.Common
{
    using Container;
    using Engine;
    using System;
    using System.Collections.Generic;
    using System.Linq;

    /// <summary>
    /// Provides the data rendering payload as event arguments.
    /// </summary>
    /// <seealso cref="EventArgs" />
    public sealed class RenderingDataEventArgs : RenderingEventArgs
    {
        /// <summary>
        /// Initializes a new instance of the <see cref="RenderingDataEventArgs" /> class.
        /// </summary>
        /// <param name="engineState">The engine.</param>
        /// <param name="dataBlock">Data block.</param>
        /// <param name="stream">The stream.</param>
        /// <param name="startTime">The start time.</param>
        /// <param name="duration">The duration.</param>
        /// <param name="clock">The clock.</param>
        internal RenderingDataEventArgs(
            MediaEngineState engineState,
            DataBlock dataBlock,
            StreamInfo stream,
            TimeSpan startTime,
            TimeSpan duration,
            TimeSpan clock)
            : base(engineState, stream, startTime, duration, clock)
        {
            Bytes = dataBlock.Bytes ?? Enumerable.Empty<byte>();
        }

        /// <summary>
        /// Data block.
        /// </summary>
        public IEnumerable<byte> Bytes { get; }
    }
}