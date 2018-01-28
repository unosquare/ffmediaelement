namespace Unosquare.FFME.Commands
{
    using Shared;
    using System;

    /// <summary>
    /// Implements the logic to pause and rewind the media stream
    /// </summary>
    /// <seealso cref="MediaCommand" />
    internal sealed class StopCommand : MediaCommand
    {
        /// <summary>
        /// Initializes a new instance of the <see cref="StopCommand" /> class.
        /// </summary>
        /// <param name="manager">The media element.</param>
        public StopCommand(MediaCommandManager manager)
            : base(manager, MediaCommandType.Stop)
        {
            // placeholder
        }

        /// <summary>
        /// Performs the actions that this command implements.
        /// </summary>
        internal override void ExecuteInternal()
        {
            var m = Manager.MediaCore;
            m.Clock.Reset();
            m.State.MediaState = PlaybackStatus.Stop;
            var seek = new SeekCommand(Manager, TimeSpan.Zero);
            seek.ExecuteInternal();

            foreach (var renderer in m.Renderers.Values)
                renderer.Stop();
        }
    }
}
