﻿namespace Unosquare.FFME.Commands
{
    using System;
    using Unosquare.FFME.Shared;

    /// <summary>
    /// The Stop Command Implementation
    /// </summary>
    /// <seealso cref="MediaCommand" />
    internal sealed class StopCommand : MediaCommand
    {
        /// <summary>
        /// Initializes a new instance of the <see cref="StopCommand"/> class.
        /// </summary>
        /// <param name="mediaCore">The media core.</param>
        public StopCommand(MediaEngine mediaCore)
            : base(mediaCore)
        {
            CommandType = MediaCommandType.Stop;
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
            m.Clock.Reset();
            m.State.UpdateMediaState(PlaybackStatus.Manual);
            var seek = new SeekCommand(m, TimeSpan.Zero);
            seek.Execute();
            m.State.UpdateMediaState(PlaybackStatus.Stop, m.WallClock);

            foreach (var renderer in m.Renderers.Values)
                renderer.Stop();
        }
    }
}
