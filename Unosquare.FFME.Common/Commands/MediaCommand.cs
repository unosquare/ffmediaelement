namespace Unosquare.FFME.Commands
{
    using Primitives;

    /// <summary>
    /// Represents a promise-style command executed in a queue.
    /// </summary>
    /// <seealso cref="PromiseBase" />
    internal abstract class MediaCommand : PromiseBase
    {
        /// <summary>
        /// Initializes a new instance of the <see cref="MediaCommand"/> class.
        /// </summary>
        /// <param name="mediaCore">The media core.</param>
        protected MediaCommand(MediaEngine mediaCore)
            : base(continueOnCapturedContext: false)
        {
            MediaCore = mediaCore;
        }

        /// <summary>
        /// Contins a reference to the media engine associated with this command
        /// </summary>
        public MediaEngine MediaCore { get; }

        /// <summary>
        /// Gets the command type identifier.
        /// </summary>
        public abstract MediaCommandType CommandType { get; }
    }
}
