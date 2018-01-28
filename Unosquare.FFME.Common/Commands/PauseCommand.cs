namespace Unosquare.FFME.Commands
{
    using Shared;

    /// <summary>
    /// Implements the logic to pause the media stream
    /// </summary>
    /// <seealso cref="MediaCommand" />
    internal sealed class PauseCommand : MediaCommand
    {
        /// <summary>
        /// Initializes a new instance of the <see cref="PauseCommand" /> class.
        /// </summary>
        /// <param name="manager">The manager.</param>
        public PauseCommand(MediaCommandManager manager)
            : base(manager, MediaCommandType.Pause)
        {
            // placeholder
        }

        /// <summary>
        /// Performs the actions that this command implements.
        /// </summary>
        internal override void ExecuteInternal()
        {
            var m = Manager.MediaCore;
            if (m.State.IsOpen == false) return;
            if (m.State.CanPause == false) return;

            m.Clock.Pause();

            foreach (var renderer in m.Renderers.Values)
                renderer.Pause();

            m.Clock.Position = m.SnapToFramePosition(m.Clock.Position);

            if (m.State.MediaState != PlaybackStatus.Stop)
                m.State.MediaState = PlaybackStatus.Pause;
        }
    }
}
