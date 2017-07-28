namespace Unosquare.FFME.Commands
{
    using System;

    /// <summary>
    /// Implements the logic to pause and rewind the media stream
    /// </summary>
    /// <seealso cref="Unosquare.FFME.Commands.MediaCommand" />
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
        internal override void Execute()
        {
            var m = Manager.MediaElement;
            m.Clock.Reset();
            var seek = new SeekCommand(this.Manager, TimeSpan.Zero);
            seek.Execute();

            foreach (var renderer in m.Renderers.Values)
                renderer.Stop();

            m.MediaState = System.Windows.Controls.MediaState.Stop;
        }
    }
}
