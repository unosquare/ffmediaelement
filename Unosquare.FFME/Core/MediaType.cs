namespace Unosquare.FFME.Core
{
    /// <summary>
    /// Enumerates the different Media Types
    /// </summary>
    internal enum MediaType
    {
        /// <summary>
        /// Represents an unexisting media type (-1)
        /// </summary>
        None = -1,

        /// <summary>
        /// The video media type (0)
        /// </summary>
        Video = 0,
        
        /// <summary>
        /// The audio media type (1)
        /// </summary>
        Audio = 1,
        
        /// <summary>
        /// The subtitle media type (3)
        /// </summary>
        Subtitle = 3,
    }
}
