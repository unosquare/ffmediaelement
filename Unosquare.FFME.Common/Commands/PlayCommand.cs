namespace Unosquare.FFME.Commands
{
    using System;

    /// <summary>
    /// The Play Command Implementation
    /// </summary>
    /// <seealso cref="CommandBase" />
    internal sealed class PlayCommand : CommandBase
    {
        /// <summary>
        /// Initializes a new instance of the <see cref="PlayCommand"/> class.
        /// </summary>
        /// <param name="mediaCore">The media core.</param>
        public PlayCommand(MediaEngine mediaCore)
            : base(mediaCore)
        {
            CommandType = CommandType.Play;
        }

        /// <inheritdoc />
        public override CommandType CommandType { get; }

        /// <inheritdoc />
        protected override void PerformActions()
        {
            var m = MediaCore;
            if (m.State.HasMediaEnded
                || (m.State.NaturalDuration.HasValue
                && m.State.NaturalDuration != TimeSpan.MinValue
                && m.WallClock >= m.State.NaturalDuration.Value))
            {
                return;
            }

            foreach (var renderer in m.Renderers.Values)
                renderer.Play();

            m.ResumePlayback();
        }
    }
}
