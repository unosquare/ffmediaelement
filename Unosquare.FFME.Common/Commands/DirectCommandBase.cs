namespace Unosquare.FFME.Commands
{
    /// <inheritdoc />
    /// <summary>
    /// Represents a promise-style media command that is executed directly on the
    /// media command manager.
    /// </summary>
    /// <seealso cref="CommandBase" />
    internal abstract class DirectCommandBase : CommandBase
    {
        /// <summary>
        /// Initializes a new instance of the <see cref="DirectCommandBase"/> class.
        /// </summary>
        /// <param name="mediaCore">The media core.</param>
        protected DirectCommandBase(MediaEngine mediaCore)
            : base(mediaCore)
        {
            // placeholder
        }

        /// <summary>
        /// Performs actions when the command has been executed.
        /// This is useful to notify exceptions or update the state of the media.
        /// </summary>
        public abstract void PostProcess();
    }
}
