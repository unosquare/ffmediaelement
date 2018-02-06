namespace Unosquare.FFME.Playlists
{
    using System;
    using System.Collections.Generic;
    using System.Collections.ObjectModel;
    using System.ComponentModel;
    using System.IO;
    using System.Linq;
    using System.Text;

    /// <summary>
    /// Represents an observable collection of playlist entries.
    /// General guidelines taken from http://xmtvplayer.com/build-m3u-file
    /// </summary>
    /// <seealso cref="ObservableCollection{PlaylistEntry}" />
    public class Playlist : ObservableCollection<PlaylistEntry>
    {
        #region Private state

        internal const string HeaderPrefix = "#EXTM3U";
        internal const string EntryPrefix = "#EXTINF";

        private string m_Name = null;

        #endregion

        #region Constructors

        /// <summary>
        /// Initializes a new instance of the <see cref="Playlist"/> class.
        /// </summary>
        public Playlist()
        {
            // placeholder
        }

        /// <summary>
        /// Initializes a new instance of the <see cref="Playlist"/> class.
        /// </summary>
        /// <param name="name">The name.</param>
        public Playlist(string name)
            : this()
        {
            Name = name;
        }

        #endregion

        #region Properties

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
        public PlaylistAttributeSet Attributes { get; } = new PlaylistAttributeSet();

        #endregion

        #region Open Methods

        /// <summary>
        /// Loads the playlist from the specified path, assuming UTF8 encoding
        /// </summary>
        /// <param name="filePath">The text.</param>
        /// <returns>The loaded playlist</returns>
        public static Playlist Open(string filePath)
        {
            return Open(filePath, Encoding.UTF8);
        }

        /// <summary>
        /// Loads the playlist from the specified stream as UTF8.
        /// </summary>
        /// <param name="stream">The stream.</param>
        /// <returns>The loaded playlist</returns>
        public static Playlist Open(Stream stream)
        {
            return Open(stream, Encoding.UTF8);
        }

        /// <summary>
        /// Loads the playlist from the specified file path.
        /// </summary>
        /// <param name="path">The path.</param>
        /// <param name="encoding">The encoding.</param>
        /// <returns>
        /// The loaded playlist
        /// </returns>
        public static Playlist Open(string path, Encoding encoding)
        {
            using (var fileStream = new FileStream(path, FileMode.Open, FileAccess.Read))
            {
                return Open(fileStream, encoding);
            }
        }

        /// <summary>
        /// Loads the playlist from the specified stream.
        /// </summary>
        /// <param name="stream">The stream.</param>
        /// <param name="encoding">The encoding.</param>
        /// <returns>The loaded playlist.</returns>
        public static Playlist Open(Stream stream, Encoding encoding)
        {
            var result = new Playlist();
            var currentEntry = default(PlaylistEntry);
            using (var reader = new StreamReader(stream, encoding))
            {
                while (reader.EndOfStream == false)
                {
                    var line = reader.ReadLine();

                    // skip blank lines
                    if (string.IsNullOrWhiteSpace(line))
                        continue;

                    if (line.ToUpperInvariant().StartsWith($"{HeaderPrefix} "))
                    {
                        result.ParseHeaderLine(line);
                    }
                    else if (line.ToUpperInvariant().StartsWith($"{EntryPrefix}:"))
                    {
                        currentEntry = new PlaylistEntry();
                        currentEntry.BeginExtendedInfoLine(line);
                    }
                    else if (line.StartsWith("#"))
                    {
                        // This is just a comment. Do nothing.
                    }
                    else
                    {
                        if (currentEntry != null)
                        {
                            currentEntry.MediaUrl = line.Trim();
                            result.Add(currentEntry);
                            currentEntry = null;
                        }
                        else
                        {
                            result.Add(string.Empty, TimeSpan.Zero, line.Trim(), null);
                        }
                    }
                }
            }

            return result;
        }

        #endregion

        #region Load Methods

        /// <summary>
        /// Loads from the specified file path.
        /// </summary>
        /// <param name="filePath">The file path.</param>
        public void Load(string filePath)
        {
            Load(filePath, Encoding.UTF8);
        }

        /// <summary>
        /// Loads from the specified file path.
        /// </summary>
        /// <param name="filePath">The file path.</param>
        /// <param name="encoding">The encoding.</param>
        public void Load(string filePath, Encoding encoding)
        {
            using (var fileStream = new FileStream(filePath, FileMode.Open, FileAccess.Read))
            {
                Load(fileStream, encoding);
            }
        }

        /// <summary>
        /// Loads the playlist data into this playlist
        /// </summary>
        /// <param name="stream">The stream.</param>
        /// <param name="encoding">The encoding.</param>
        public void Load(Stream stream, Encoding encoding)
        {
            var source = Open(stream, encoding);
            Name = source.Name;
            Clear();
            foreach (var entry in source)
                Add(entry);

            foreach (var key in Attributes.Keys.ToArray())
                Attributes.Remove(key);

            foreach (var attribute in source.Attributes)
                Attributes[attribute.Key] = attribute.Value;
        }

        #endregion

        #region Save Methods

        /// <summary>
        /// Saves the playlist to the specified stream.
        /// </summary>
        /// <param name="stream">The stream.</param>
        /// <param name="encoding">The encoding.</param>
        public void Save(Stream stream, Encoding encoding)
        {
            using (var writer = new StreamWriter(stream, encoding))
            {
                writer.WriteLine($"{HeaderPrefix} {Name} {Attributes}".Trim());
                writer.WriteLine();

                foreach (var entry in this)
                {
                    writer.WriteLine();
                    writer.WriteLine($"{EntryPrefix}:{Convert.ToInt64(entry.Duration.TotalSeconds)} {entry.Attributes}, {entry.Title}".Trim());
                    writer.WriteLine(entry.MediaUrl?.Trim());
                }
            }
        }

        /// <summary>
        /// Saves the playlist to the specified stream with UTF8 encoding.
        /// </summary>
        /// <param name="stream">The stream.</param>
        public void Save(Stream stream)
        {
            Save(stream, Encoding.UTF8);
        }

        /// <summary>
        /// Saves the playlist to the specified path in UTF8 encoding.
        /// </summary>
        /// <param name="path">The path.</param>
        public void Save(string path)
        {
            using (var stream = new FileStream(path, FileMode.Create, FileAccess.Write))
            {
                Save(stream, Encoding.UTF8);
            }
        }

        #endregion

        #region Other Methods

        /// <summary>
        /// Adds an entry to the playlist.
        /// </summary>
        /// <param name="title">The title.</param>
        /// <param name="duration">The duration.</param>
        /// <param name="url">The URL.</param>
        /// <param name="attributes">The attributes.</param>
        public void Add(string title, TimeSpan duration, string url, Dictionary<string, string> attributes = null)
        {
            var entry = new PlaylistEntry
            {
                Duration = duration,
                MediaUrl = url,
                Title = title
            };

            if (attributes != null)
            {
                foreach (var kvp in attributes)
                {
                    entry.Attributes[kvp.Key] = kvp.Value;
                }
            }

            Add(entry);
        }

        #endregion
    }
}
