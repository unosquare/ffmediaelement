namespace Unosquare.FFME.Playlists
{
    using System;
    using System.Collections.Generic;
    using System.Collections.ObjectModel;
    using System.ComponentModel;
    using System.IO;
    using System.Linq;
    using System.Text;

    /// <inheritdoc cref="ObservableCollection{T}"/>
    /// <summary>
    /// Represents an observable collection of playlist entries.
    /// General guidelines taken from http://xmtvplayer.com/build-m3u-file.
    /// </summary>
    /// <typeparam name="T">The type of playlist items.</typeparam>
    public class PlaylistEntryCollection<T> : ObservableCollection<T>
        where T : PlaylistEntry, new()
    {
        #region Private state

        internal const string HeaderPrefix = "#EXTM3U";
        internal const string EntryPrefix = "#EXTINF";

        private string m_Name;

        #endregion

        #region Constructors

        /// <summary>
        /// Initializes a new instance of the <see cref="PlaylistEntryCollection{T}"/> class.
        /// </summary>
        public PlaylistEntryCollection()
        {
            // Placeholder
        }

        /// <summary>
        /// Initializes a new instance of the <see cref="PlaylistEntryCollection{T}"/> class.
        /// </summary>
        /// <param name="name">The name.</param>
        public PlaylistEntryCollection(string name)
            : this()
        {
            m_Name = name;
        }

        #endregion

        #region Properties

        /// <summary>
        /// Gets or sets the name of this playlist.
        /// </summary>
        public string Name
        {
            get => m_Name;
            set
            {
                m_Name = value;
                OnPropertyChanged(new PropertyChangedEventArgs(nameof(Name)));
            }
        }

        /// <summary>
        /// Gets the extended attributes key-value pairs.
        /// </summary>
        public PlaylistEntryAttributeDictionary Attributes { get; } = new PlaylistEntryAttributeDictionary();

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
        /// Loads the playlist data into this playlist.
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
                    writer.WriteLine(entry.MediaSource?.Trim());
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
        /// Called when [property changed].
        /// </summary>
        /// <param name="propertyName">Name of the property.</param>
        public void NotifyAttributeChangedFor(string propertyName)
        {
            OnPropertyChanged(new PropertyChangedEventArgs(propertyName));
        }

        /// <summary>
        /// Adds an entry to the playlist.
        /// </summary>
        /// <param name="title">The title.</param>
        /// <param name="duration">The duration.</param>
        /// <param name="mediaSource">The media source URL.</param>
        /// <param name="attributes">The attributes.</param>
        public void Add(string title, TimeSpan duration, string mediaSource, Dictionary<string, string> attributes)
        {
            var entry = new T
            {
                Duration = duration,
                MediaSource = mediaSource,
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

        /// <summary>
        /// Adds an entry to the playlist without extended attributes.
        /// </summary>
        /// <param name="title">The title.</param>
        /// <param name="duration">The duration.</param>
        /// <param name="mediaSource">The media source URL.</param>
        public void Add(string title, TimeSpan duration, string mediaSource) => Add(title, duration, mediaSource, null);

        /// <summary>
        /// Loads the playlist from the specified stream.
        /// </summary>
        /// <param name="stream">The stream.</param>
        /// <param name="encoding">The encoding.</param>
        /// <returns>The loaded playlist.</returns>
        protected static PlaylistEntryCollection<T> Open(Stream stream, Encoding encoding)
        {
            var result = new PlaylistEntryCollection<T>();
            var currentEntry = default(T);
            using (var reader = new StreamReader(stream, encoding))
            {
                while (reader.EndOfStream == false)
                {
                    var line = reader.ReadLine();

                    // skip blank lines
                    if (string.IsNullOrWhiteSpace(line))
                        continue;

                    if (line.StartsWith($"{HeaderPrefix} ", StringComparison.OrdinalIgnoreCase))
                    {
                        result.ParseHeaderLine(line);
                    }
                    else if (line.StartsWith($"{EntryPrefix}:", StringComparison.OrdinalIgnoreCase))
                    {
                        currentEntry = new T();
                        currentEntry.BeginExtendedInfoLine(line);
                    }
                    else if (line.StartsWith("#", StringComparison.Ordinal))
                    {
                        // This is just a comment. Do nothing.
                    }
                    else
                    {
                        if (currentEntry != null)
                        {
                            currentEntry.MediaSource = line.Trim();
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
    }

    /// <summary>
    /// A standard Playlist class with regular <see cref="PlaylistEntry"/> items.
    /// </summary>
    /// <seealso cref="PlaylistEntryCollection{T}" />
    public class PlaylistEntryCollection : PlaylistEntryCollection<PlaylistEntry> { }
}
