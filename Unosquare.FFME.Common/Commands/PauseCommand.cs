﻿namespace Unosquare.FFME.Commands
{
    using Shared;

    /// <summary>
    /// The Pause Command Implementation
    /// </summary>
    /// <seealso cref="MediaCommand" />
    internal sealed class PauseCommand : MediaCommand
    {
        /// <summary>
        /// Initializes a new instance of the <see cref="PauseCommand"/> class.
        /// </summary>
        /// <param name="mediaCore">The media core.</param>
        public PauseCommand(MediaEngine mediaCore)
            : base(mediaCore)
        {
            CommandType = MediaCommandType.Pause;
        }

        /// <summary>
        /// Gets the command type identifier.
        /// </summary>
        public override MediaCommandType CommandType { get; }

        /// <summary>
        /// Performs the actions represented by this deferred task.
        /// </summary>
        protected override void PerformActions()
        {
            var m = MediaCore;
            if (m.State.CanPause == false)
                return;

            m.Clock.Pause();

            foreach (var renderer in m.Renderers.Values)
                renderer.Pause();

            var wallClock = m.SnapPositionToBlockPosition(m.WallClock);
            m.Clock.Update(wallClock);
            m.State.UpdateMediaState(PlaybackStatus.Pause);
        }
    }
}
