namespace Unosquare.FFME.Commands
{
    /// <summary>
    /// Implements the logic to start or resume media playback
    /// </summary>
    /// <seealso cref="Unosquare.FFME.Commands.MediaCommand" />
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
        protected override void Execute()
        {
            var m = Manager.MediaElement;
            if (m.IsOpen == false) return;

            foreach (var renderer in m.Renderers.Values)
                renderer.Play();

            m.Clock.Play();
            m.MediaState = System.Windows.Controls.MediaState.Play;
        }
    }
}
