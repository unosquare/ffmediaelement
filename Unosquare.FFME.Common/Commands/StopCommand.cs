namespace Unosquare.FFME.Commands
{
    using Shared;
    using System;

    /// <summary>
    /// The Stop Command Implementation
    /// </summary>
    /// <seealso cref="CommandBase" />
    internal sealed class StopCommand : SeekCommand
    {
        /// <summary>
        /// Initializes a new instance of the <see cref="StopCommand"/> class.
        /// </summary>
        /// <param name="mediaCore">The media core.</param>
        public StopCommand(MediaEngine mediaCore)
            : base(mediaCore, TimeSpan.Zero, SeekMode.Stop)
        {
            CommandType = CommandType.Stop;
        }

        /// <inheritdoc />
        public override CommandType CommandType { get; }

        /// <inheritdoc />
        protected override void PerformActions()
        {
            var m = MediaCore;
            m.Clock.Reset();
            base.PerformActions();
            foreach (var renderer in m.Renderers.Values)
                renderer.Stop();

            m.State.UpdateMediaState(PlaybackStatus.Stop);
        }
    }
}
