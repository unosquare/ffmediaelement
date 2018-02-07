namespace Unosquare.FFME.Windows.Sample.Kernel
{
    using Playlists;

    /// <summary>
    /// A class exposing usage of custom playlists
    /// </summary>
    public class CustomPlaylist : Playlist<CustomPlaylistEntry>
    {
        /// <summary>
        /// Initializes a new instance of the <see cref="CustomPlaylist"/> class.
        /// </summary>
        public CustomPlaylist()
        {
            // placeholder
        }

        /// <summary>
        /// Opens the playlist from the specified path.
        /// </summary>
        /// <param name="filePath">The file path.</param>
        /// <returns>The playlist</returns>
        public static new CustomPlaylist Open(string filePath)
        {
            var source = Playlist<CustomPlaylistEntry>.Open(filePath);
            var target = new CustomPlaylist
            {
                Name = source.Name
            };

            foreach (var aourceAttribute in source.Attributes)
                target.Attributes[aourceAttribute.Key] = aourceAttribute.Value;

            foreach (var sourceEntry in source)
                target.Add(sourceEntry);

            return target;
        }
    }
}
