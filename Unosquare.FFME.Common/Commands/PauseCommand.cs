namespace Unosquare.FFME.Commands
{
    using Shared;
    using System.Threading.Tasks;

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
        /// <returns>A <see cref="Task"/> representing the asynchronous operation.</returns>
        internal override Task ExecuteInternal()
        {
            var m = Manager.MediaCore;
            if (m.State.IsOpen == false || m.State.CanPause == false) return Task.CompletedTask;

            m.Clock.Pause();

            foreach (var renderer in m.Renderers.Values)
                renderer.Pause();

            var wallClock = m.SnapToFramePosition(m.WallClock);
            m.Clock.Update(wallClock);
            m.State.UpdateMediaState(PlaybackStatus.Pause);

            return Task.CompletedTask;
        }
    }
}
