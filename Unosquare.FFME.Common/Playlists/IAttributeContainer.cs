namespace Unosquare.FFME.Playlists
{
    /// <summary>
    /// Identifies classes that contain a Attributes property
    /// </summary>
    public interface IAttributeContainer
    {
        /// <summary>
        /// Gets the attributes.
        /// </summary>
        PlaylistAttributeSet Attributes { get; }

        /// <summary>
        /// Called when [property changed].
        /// </summary>
        /// <param name="propertyName">Name of the property.</param>
        void NotifyAttributeChangedFor(string propertyName);
    }
}
