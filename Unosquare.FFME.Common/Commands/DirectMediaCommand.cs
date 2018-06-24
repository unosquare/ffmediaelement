namespace Unosquare.FFME.Commands
{
    /// <summary>
    /// Represents a promise-style media command that is executed directly on the
    /// media command manager.
    /// </summary>
    /// <seealso cref="MediaCommand" />
    internal abstract class DirectMediaCommand : MediaCommand
    {
        /// <summary>
        /// Initializes a new instance of the <see cref="DirectMediaCommand"/> class.
        /// </summary>
        /// <param name="mediaCore">The media core.</param>
        public DirectMediaCommand(MediaEngine mediaCore)
            : base(mediaCore)
        {
            // Placeholder
        }

        /// <summary>
        /// Performs actions when the command has been executed.
        /// This is useful to notify exceptions or update the state of the media.
        /// </summary>
        public abstract void PostProcess();
    }
}
