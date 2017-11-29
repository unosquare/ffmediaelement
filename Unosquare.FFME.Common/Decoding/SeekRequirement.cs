namespace Unosquare.FFME.Decoding
{
    /// <summary>
    /// Enumerates the seek target requirement levels.
    /// </summary>
    internal enum SeekRequirement
    {
        /// <summary>
        /// Seek requirement is satisfied when
        /// the main component has frames in the seek range.
        /// This is the fastest option.
        /// </summary>
        MainComponentOnly,

        /// <summary>
        /// Seek requirement is satisfied when
        /// the both audio and video comps have frames in the seek range.
        /// This is the recommended option.
        /// </summary>
        AudioAndVideo,

        /// <summary>
        /// Seek requirement is satisfied when
        /// ALL components have frames in the seek range
        /// This is NOT recommended as it forces large amounts of
        /// frames to get decoded in subtitle files.
        /// </summary>
        AllComponents,
    }
}
