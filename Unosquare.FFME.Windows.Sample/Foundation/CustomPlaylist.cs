namespace Unosquare.FFME.Windows.Sample.Foundation
{
    using Events;
    using Playlists;
    using Shared;
    using System;
    using System.IO;
    using System.Text;
    using ViewModels;

    /// <summary>
    /// A class exposing usage of custom playlists
    /// </summary>
    public class CustomPlaylist : Playlist<CustomPlaylistEntry>
    {
        private readonly object SyncRoot = new object();
        private readonly PlaylistViewModel ViewModel;

        /// <summary>
        /// Initializes a new instance of the <see cref="CustomPlaylist" /> class.
        /// </summary>
        /// <param name="viewModel">The view model.</param>
        public CustomPlaylist(PlaylistViewModel viewModel)
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
        /// <param name="mediaUrl">The media URL.</param>
        /// <returns>The playlist entry or null if not found</returns>
        public CustomPlaylistEntry FindEntryByMediaUrl(string mediaUrl)
        {
            lock (SyncRoot)
            {
                var lookupMediaUrl = mediaUrl.ToLowerInvariant() ?? string.Empty;
                foreach (var entry in this)
                {
                    if (Equals(entry.MediaUrl?.ToLowerInvariant(), lookupMediaUrl))
                        return entry;
                }

                return null;
            }
        }

        /// <summary>
        /// Adds or updates an entry.
        /// </summary>
        /// <param name="mediaUrl">The media URL.</param>
        /// <param name="info">The information.</param>
        public void AddOrUpdateEntry(string mediaUrl, MediaInfo info)
        {
            lock (SyncRoot)
            {
                var entry = FindEntryByMediaUrl(mediaUrl);
                if (entry == null)
                {
                    // Create a new entry with default values
                    entry = new CustomPlaylistEntry
                    {
                        MediaUrl = mediaUrl,
                        Title = Uri.TryCreate(mediaUrl, UriKind.RelativeOrAbsolute, out var entryUri)
                            ? Path.GetFileNameWithoutExtension(Uri.UnescapeDataString(entryUri.AbsolutePath))
                            : $"Media File {DateTime.Now}"
                    };

                    // Try to get a title from metadata
                    foreach (var meta in info.Metadata)
                    {
                        if (meta.Key?.ToLowerInvariant()?.Trim()?.Equals("title") ?? false)
                        {
                            entry.Title = meta.Value;
                            break;
                        }
                    }

                    if (string.IsNullOrWhiteSpace(entry.Title))
                        entry.Title = $"(No Name) - {mediaUrl}";
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
        /// <param name="mediaUrl">The media URL.</param>
        /// <param name="bitmap">The bitmap.</param>
        public void AddOrUpdateEntryThumbnail(string mediaUrl, BitmapDataBuffer bitmap)
        {
            lock (SyncRoot)
            {
                var entry = FindEntryByMediaUrl(mediaUrl);
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
        /// <param name="mediaUrl">The media URL.</param>
        public void RemoveEntryByMediaUrl(string mediaUrl)
        {
            lock (SyncRoot)
            {
                var entry = FindEntryByMediaUrl(mediaUrl);
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
