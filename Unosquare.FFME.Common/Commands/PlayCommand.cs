namespace Unosquare.FFME.Commands
{
    using Shared;
    using System;
    using System.Threading.Tasks;

    /// <summary>
    /// Implements the logic to start or resume media playback
    /// </summary>
    /// <seealso cref="MediaCommand" />
    internal sealed class PlayCommand : MediaCommand
    {
        /// <summary>
        /// Initializes a new instance of the <see cref="PlayCommand" /> class.
        /// </summary>
        /// <param name="manager">The media element.</param>
        public PlayCommand(MediaCommandManager manager)
            : base(manager, MediaCommandType.Play)
        {
        }

        /// <summary>
        /// Performs the actions that this command implements.
        /// </summary>
        /// <returns>A <see cref="Task"/> representing the asynchronous operation.</returns>
        internal override Task ExecuteInternal()
        {
            var m = Manager.MediaCore;

            if (m.State.IsOpen == false) return Task.CompletedTask;
            if (m.State.HasMediaEnded
                || (m.State.NaturalDuration.HasValue
                && m.State.NaturalDuration != TimeSpan.MinValue
                && m.WallClock >= m.State.NaturalDuration.Value))
                return Task.CompletedTask;

            foreach (var renderer in m.Renderers.Values)
                renderer.Play();

            m.Clock.Play();
            m.State.UpdateMediaState(PlaybackStatus.Play, m.WallClock);

            return Task.CompletedTask;
        }
    }
}
