namespace Unosquare.FFME.Commands
{
    using System;
    using Unosquare.FFME.Shared;

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
            Category = CommandCategory.Priority;
        }

        /// <summary>
        /// Gets the command type identifier.
        /// </summary>
        public override CommandType CommandType { get; }

        /// <summary>
        /// Gets the command category.
        /// </summary>
        public override CommandCategory Category { get; }

        /// <summary>
        /// Performs the actions represented by this deferred task.
        /// </summary>
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

            m.Clock.Play();
            m.State.UpdateMediaState(PlaybackStatus.Play, m.WallClock);
        }
    }
}
