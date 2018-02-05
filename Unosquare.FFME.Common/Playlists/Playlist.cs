namespace Unosquare.FFME.Playlists
{
    using System;
    using System.Collections.ObjectModel;
    using System.ComponentModel;
    using System.IO;
    using System.Text;

    /// <summary>
    /// Represents an observable collection of playlist entries.
    /// General guidelines taken from http://xmtvplayer.com/build-m3u-file
    /// </summary>
    /// <seealso cref="ObservableCollection{PlaylistEntry}" />
    public class Playlist : ObservableCollection<PlaylistEntry>
    {
        private const string HeaderPrefix = "#EXTM3U";
        private const string EntryPrefix = "#EXTINF";

        private string m_Name = null;

        /// <summary>
        /// Initializes a new instance of the <see cref="Playlist"/> class.
        /// </summary>
        public Playlist()
        {
            // placeholder
        }

        /// <summary>
        /// Gets or sets the name of this playlist.
        /// </summary>
        public string Name
        {
            get
            {
                return m_Name;
            }
            set
            {
                m_Name = value;
                OnPropertyChanged(new PropertyChangedEventArgs(nameof(Name)));
            }
        }

        /// <summary>
        /// Gets the extended attributes key-value pairs.
        /// </summary>
        public PlaylistEntryAttributeSet Attributes { get; } = new PlaylistEntryAttributeSet();

        /// <summary>
        /// Loads the playlist from the specified text.
        /// </summary>
        /// <param name="text">The text.</param>
        /// <returns>The loaded playlist</returns>
        public static Playlist Load(string text)
        {
            using (var s = new MemoryStream(Encoding.UTF8.GetBytes(text)))
            {
                s.Position = 0;
                return Load(s);
            }
        }

        /// <summary>
        /// Loads the playlist from the specified stream as UTF8.
        /// </summary>
        /// <param name="stream">The stream.</param>
        /// <returns>The loaded playlist</returns>
        public static Playlist Load(Stream stream)
        {
            return Load(stream, Encoding.UTF8);
        }

        /// <summary>
        /// Loads the playlist from the specified file path.
        /// </summary>
        /// <param name="path">The path.</param>
        /// <param name="encoding">The encoding.</param>
        /// <returns>
        /// The loaded playlist
        /// </returns>
        public static Playlist Load(string path, Encoding encoding)
        {
            using (var fileStream = new FileStream(path, FileMode.Open, FileAccess.Read))
            {
                return Load(fileStream, encoding);
            }
        }

        /// <summary>
        /// Loads the playlist from the specified stream.
        /// </summary>
        /// <param name="stream">The stream.</param>
        /// <param name="encoding">The encoding.</param>
        /// <returns>The loaded playlist.</returns>
        public static Playlist Load(Stream stream, Encoding encoding)
        {
            throw new NotImplementedException();

            // using (var reader = new StreamReader(stream, encoding))
            // {
            //    while (reader.EndOfStream == false)
            //    {
            //        var line = reader.ReadLine();
            //        if (string.IsNullOrWhiteSpace(line))
            //            continue;
            //    }
            // }
        }

        /// <summary>
        /// Saves the playlist to the specified stream.
        /// </summary>
        /// <param name="stream">The stream.</param>
        /// <param name="encoding">The encoding.</param>
        public void Save(Stream stream, Encoding encoding)
        {
            throw new NotImplementedException();

            // using (var writer = new StreamWriter(stream, encoding))
            // {
            // }
        }
    }
}
