namespace Unosquare.FFME.Workers
{
    /// <summary>
    /// Defines the different worker types.
    /// </summary>
    internal enum MediaWorkerType
    {
        /// <summary>
        /// The packet reading worker
        /// </summary>
        Read,

        /// <summary>
        /// The frame decoding worker
        /// </summary>
        Decode,

        /// <summary>
        /// The block rendering worker
        /// </summary>
        Render
    }
}
