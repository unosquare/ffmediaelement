namespace Unosquare.FFME.Windows.Sample.Foundation
{
    using Engine;
    using Events;
    using Playlists;
    using System;
    using System.IO;
    using System.Text;
    using ViewModels;

    /// <summary>
    /// A class exposing usage of custom play lists
    /// </summary>
    public class CustomPlaylistEntryCollection : PlaylistEntryCollection<CustomPlaylistEntry>
    {
        private readonly object SyncRoot = new object();
        private readonly PlaylistViewModel ViewModel;

        /// <summary>
        /// Initializes a new instance of the <see cref="CustomPlaylistEntryCollection" /> class.
        /// </summary>
        /// <param name="viewModel">The view model.</param>
        public CustomPlaylistEntryCollection(PlaylistViewModel viewModel)
        {
            ViewModel = viewModel;

            // Create a default playlist.
            if (File.Exists(ViewModel.PlaylistFilePath))
            {
                LoadEntries();
            }
            else
            {
                Name = RootViewModel.ProductName;
                Attributes["x-projecturl"] = "https://github.com/unosquare/ffmediaelement";
                SaveEntries();
            }

            // Update the version
            Attributes["x-version"] = ViewModel.Root.AppVersion;
        }

        /// <summary>
        /// Finds an entry based on the media url.
        /// </summary>
        /// <param name="mediaSource">The media URL.</param>
        /// <returns>The playlist entry or null if not found</returns>
        public CustomPlaylistEntry FindEntryByMediaSource(string mediaSource)
        {
            lock (SyncRoot)
            {
                var lookupMediaSource = mediaSource?.Trim() ?? string.Empty;
                foreach (var entry in this)
                {
                    if (lookupMediaSource.Trim().Equals(entry.MediaSource, StringComparison.OrdinalIgnoreCase))
                        return entry;
                }

                return null;
            }
        }

        /// <summary>
        /// Adds or updates an entry.
        /// </summary>
        /// <param name="mediaSource">The media URL.</param>
        /// <param name="info">The information.</param>
        public void AddOrUpdateEntry(string mediaSource, MediaInfo info)
        {
            lock (SyncRoot)
            {
                var entry = FindEntryByMediaSource(mediaSource);
                if (entry == null)
                {
                    // Create a new entry with default values
                    entry = new CustomPlaylistEntry
                    {
                        MediaSource = mediaSource,
                        Title = Uri.TryCreate(mediaSource, UriKind.RelativeOrAbsolute, out var entryUri)
                            ? Path.GetFileNameWithoutExtension(Uri.UnescapeDataString(entryUri.AbsolutePath))
                            : $"Media File {DateTime.Now}"
                    };

                    // Try to get a title from metadata
                    foreach (var meta in info.Metadata)
                    {
                        if (!(meta.Key?.Trim().Equals("title", StringComparison.OrdinalIgnoreCase) ?? false))
                            continue;

                        entry.Title = meta.Value;
                        break;
                    }

                    if (string.IsNullOrWhiteSpace(entry.Title))
                        entry.Title = $"(No Name) - {mediaSource}";
                }
                else
                {
                    // Remove. We will insert at 0 later
                    Remove(entry);
                }

                // Set as the first entry by inserting it in the zeroth position
                Insert(0, entry);

                // Try to get the duration and other properties
                entry.Duration = info.Duration == TimeSpan.MinValue ? TimeSpan.FromSeconds(-1) : info.Duration;
                entry.LastOpenedUtc = DateTime.UtcNow;
                entry.Format = info.Format;

                foreach (var meta in info.Metadata)
                {
                    // Get a safe meta-key
                    var metaKey = meta.Key?.Trim() ?? "none";
                    var sb = new StringBuilder();
                    foreach (var c in metaKey)
                    {
                        if (char.IsWhiteSpace(c))
                            sb.Append("-");
                        else
                            sb.Append(c);
                    }

                    metaKey = sb.ToString();
                    entry.Attributes[$"{nameof(meta)}-{metaKey}"] = meta.Value;
                }
            }
        }

        /// <summary>
        /// Sets the entry thumbnail.
        /// Deletes the prior thumbnail file is found or previously set.
        /// </summary>
        /// <param name="mediaSource">The media URL.</param>
        /// <param name="bitmap">The bitmap.</param>
        public void AddOrUpdateEntryThumbnail(string mediaSource, BitmapDataBuffer bitmap)
        {
            lock (SyncRoot)
            {
                var entry = FindEntryByMediaSource(mediaSource);
                if (entry == null) return;

                if (entry.Thumbnail != null)
                {
                    var existingThumbnailFilePath = Path.Combine(ViewModel.ThumbsDirectory, entry.Thumbnail);
                    if (File.Exists(existingThumbnailFilePath))
                        File.Delete(existingThumbnailFilePath);

                    entry.Thumbnail = null;
                }

                using (var bmp = bitmap.CreateDrawingBitmap())
                {
                    entry.Thumbnail = ThumbnailGenerator.SnapThumbnail(bmp, ViewModel.ThumbsDirectory);
                }
            }
        }

        /// <summary>
        /// Removes the entry.
        /// </summary>
        /// <param name="mediaSource">The media URL.</param>
        public void RemoveEntryByMediaSource(string mediaSource)
        {
            lock (SyncRoot)
            {
                var entry = FindEntryByMediaSource(mediaSource);
                if (entry == null) return;
                Remove(entry);
            }
        }

        /// <summary>
        /// Loads the Playlist from the default location
        /// </summary>
        public void LoadEntries()
        {
            lock (SyncRoot)
            {
                Load(ViewModel.PlaylistFilePath);
            }
        }

        /// <summary>
        /// Saves the playlist to the default location
        /// </summary>
        public void SaveEntries()
        {
            lock (SyncRoot)
            {
                Save(ViewModel.PlaylistFilePath);
            }
        }
    }
}
