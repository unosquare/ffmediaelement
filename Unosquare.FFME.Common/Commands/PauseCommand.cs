namespace Unosquare.FFME.Commands
{
    using Core;
    using System;

    /// <summary>
    /// Implements the logic to pause the media stream
    /// </summary>
    /// <seealso cref="Unosquare.FFME.Commands.MediaCommand" />
    internal sealed class PauseCommand : MediaCommand
    {
        /// <summary>
        /// Initializes a new instance of the <see cref="PauseCommand" /> class.
        /// </summary>
        /// <param name="manager">The manager.</param>
        public PauseCommand(MediaCommandManager manager)
            : base(manager, MediaCommandType.Pause)
        {
        }

        /// <summary>
        /// Performs the actions that this command implements.
        /// </summary>
        internal override void ExecuteInternal()
        {
            var m = Manager.MediaElement;
            if (m.IsOpen == false) return;
            if (m.CanPause == false) return;

            m.Clock.Pause();

            foreach (var renderer in m.Renderers.Values)
                renderer.Pause();

            m.SnapVideoPosition(m.Clock.Position);

            if (m.MediaState != CoreMediaState.Stop)
                m.MediaState = CoreMediaState.Pause;
        }
    }
}
