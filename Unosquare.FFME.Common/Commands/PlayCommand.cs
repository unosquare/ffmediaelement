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

        /// <summary>
        /// Gets a value indicating whether the media can play.
        /// </summary>
        private bool CanPlay
        {
            get
            {
                var m = MediaCore;
                if (m.State.HasMediaEnded)
                    return false;

                if (m.State.IsLiveStream)
                    return true;

                if (!m.State.IsSeekable)
                    return true;

                if (!m.State.NaturalDuration.HasValue)
                    return true;

                if (m.State.NaturalDuration == TimeSpan.MinValue)
                    return true;

                return m.WallClock < m.State.NaturalDuration;
            }
        }

        /// <inheritdoc />
        protected override void PerformActions()
        {
            if (!CanPlay)
                return;

            var m = MediaCore;
            foreach (var renderer in m.Renderers.Values)
                renderer.Play();

            m.ResumePlayback();
        }
    }
}
