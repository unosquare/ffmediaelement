namespace Unosquare.FFME.Commands
{
    using Primitives;

    /// <summary>
    /// Represents a promise-style command executed in a queue.
    /// </summary>
    /// <seealso cref="PromiseBase" />
    internal abstract class CommandBase : PromiseBase
    {
        /// <summary>
        /// Initializes a new instance of the <see cref="CommandBase"/> class.
        /// </summary>
        /// <param name="mediaCore">The media core.</param>
        protected CommandBase(MediaEngine mediaCore)
            : base(continueOnCapturedContext: false)
        {
            MediaCore = mediaCore;
        }

        /// <summary>
        /// Contains a reference to the media engine associated with this command
        /// </summary>
        public MediaEngine MediaCore { get; }

        /// <summary>
        /// Gets the command type identifier.
        /// </summary>
        public abstract CommandType CommandType { get; }

        /// <summary>
        /// Gets the command category.
        /// </summary>
        public abstract CommandCategory Category { get; }

        /// <summary>
        /// Gets a value indicating whether this command processes seeking operations
        /// </summary>
        public bool AffectsSeekingState => TypeAffectsSeekingState(CommandType);

        /// <summary>
        /// Determines if the command type affects seeking states.
        /// </summary>
        /// <param name="commandType">Type of the command.</param>
        /// <returns>The value</returns>
        public static bool TypeAffectsSeekingState(CommandType commandType) =>
            commandType == CommandType.Seek ||
            commandType == CommandType.Stop;
    }
}
