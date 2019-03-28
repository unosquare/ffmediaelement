namespace Unosquare.FFME.Workers
{
    using Primitives;

    /// <summary>
    /// Represents a worker API owned by a <see cref="MediaEngine"/>.
    /// </summary>
    /// <seealso cref="IWorker" />
    internal interface IMediaWorker : IWorker
    {
        /// <summary>
        /// Gets the media core this worker belongs to.
        /// </summary>
        MediaEngine MediaCore { get; }
    }
}
