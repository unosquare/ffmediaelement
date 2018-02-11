namespace Unosquare.FFME.Windows.Sample.Kernel
{
    using Shared;
    using System;
    using System.ComponentModel;
    using System.Drawing;
    using System.Drawing.Drawing2D;
    using System.Drawing.Imaging;
    using System.IO;
    using System.Text;
    using System.Windows.Data;
    using Unosquare.FFME.Events;

    /// <summary>
    /// Rperesents a holder for playlist and configuration manager
    /// </summary>
    public static class PlaylistManager
    {
        private static readonly object SyncRoot = new object();
        private static string m_SearchString = string.Empty;

        /// <summary>
        /// Initializes static members of the <see cref="PlaylistManager"/> class.
        /// </summary>
        static PlaylistManager()
        {
            // Set the version according to the assembly
            Version = typeof(PlaylistManager).Assembly.GetName().Version.ToString();

            // Set and create an app data directory
            AppDataDirectory = Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData), ProductName);
            if (Directory.Exists(AppDataDirectory) == false)
                Directory.CreateDirectory(AppDataDirectory);

            // Set and create a thumbnails directory
            ThumbsDirectory = Path.Combine(AppDataDirectory, "Thumbnails");
            if (Directory.Exists(ThumbsDirectory) == false)
                Directory.CreateDirectory(ThumbsDirectory);

            // Set a full path to the playlist file
            PlaylistFilePath = Path.Combine(AppDataDirectory, "ffme.m3u8");

            // Create a default playlist.
            if (File.Exists(PlaylistFilePath))
            {
                Entries = CustomPlaylist.Open(PlaylistFilePath);
            }
            else
            {
                Entries = new CustomPlaylist() { Name = ProductName };
                Entries.Attributes["x-projecturl"] = "https://github.com/unosquare/ffmediaelement";
                FFmpegPath = DefaultFFmpegPath;
                SaveEntries();
            }

            // Update the version
            Entries.Attributes["x-version"] = Version;

            EntriesView = CollectionViewSource.GetDefaultView(Entries) as ICollectionView;
            EntriesView.Filter = (item) =>
            {
                var entry = item as CustomPlaylistEntry;
                if (entry == null) return false;
                if (string.IsNullOrWhiteSpace(SearchString) || SearchString.Trim().Length <= 2)
                    return true;

                if ((entry.Title?.ToLowerInvariant().Contains(SearchString) ?? false) ||
                    (entry.MediaUrl?.ToLowerInvariant().Contains(SearchString) ?? false))
                    return true;

                return false;
            };
        }

        #region Public Use Properties

        /// <summary>
        /// Contains a list of playlist entries
        /// </summary>
        public static CustomPlaylist Entries { get; }

        /// <summary>
        /// Gets the entries view.
        /// </summary>
        public static ICollectionView EntriesView { get; }

        /// <summary>
        /// Gets or sets the entry search string.
        /// </summary>
        public static string SearchString
        {
            get
            {
                return m_SearchString;
            }
            set
            {
                m_SearchString = value;
                EntriesView.Refresh();
            }
        }

        /// <summary>
        /// Gets or sets the FFmpeg path.
        /// </summary>
        public static string FFmpegPath
        {
            get => Entries.Attributes["u-ffmpeg-path"];
            set => Entries.Attributes["u-ffmpeg-path"] = value;
        }

        /// <summary>
        /// Gets the full path of the thumbnails directory
        /// </summary>
        private static string ThumbsDirectory { get; }

        #endregion

        #region Private Use Properties

        private static string ProductName { get; } = "Unosquare FFME-Play";

        private static string Version { get; }

        private static string DefaultFFmpegPath { get; } = @"C:\ffmpeg\";

        private static string AppDataDirectory { get; }

        private static string PlaylistFilePath { get; }

        #endregion

        /// <summary>
        /// Finds an entry based on the media url.
        /// </summary>
        /// <param name="mediaUrl">The media URL.</param>
        /// <returns>The playlist entry or null if not found</returns>
        public static CustomPlaylistEntry FindEntry(string mediaUrl)
        {
            lock (SyncRoot)
            {
                var lookupMediaUrl = mediaUrl.ToLowerInvariant() ?? string.Empty;
                foreach (var entry in Entries)
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
        public static void AddOrUpdateEntry(string mediaUrl, MediaInfo info)
        {
            lock (SyncRoot)
            {
                var entry = FindEntry(mediaUrl);
                if (entry == null)
                {
                    // Create a new entry with default values
                    entry = new CustomPlaylistEntry { MediaUrl = mediaUrl };
                    if (Uri.TryCreate(mediaUrl, UriKind.RelativeOrAbsolute, out Uri entryUri))
                        entry.Title = Path.GetFileNameWithoutExtension(Uri.UnescapeDataString(entryUri.AbsolutePath));
                    else
                        entry.Title = $"Media File {DateTime.Now}";

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
                    Entries.Remove(entry);
                }

                // Set as the first entry by inserting it in the zeroth position
                Entries.Insert(0, entry);

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
        public static void AddOrUpdateEntryThumbnail(string mediaUrl, BitmapDataBuffer bitmap)
        {
            lock (SyncRoot)
            {
                var entry = FindEntry(mediaUrl);
                if (entry == null) return;

                if (entry.Thumbnail != null)
                {
                    var existingThumbnailFilePath = Path.Combine(ThumbsDirectory, entry.Thumbnail);
                    if (File.Exists(existingThumbnailFilePath))
                        File.Delete(existingThumbnailFilePath);

                    entry.Thumbnail = null;
                }

                using (var bmp = bitmap.CreateDrawingBitmap())
                {
                    entry.Thumbnail = ThumbnailGenerator.SnapThumbnail(bmp);
                }
            }
        }

        /// <summary>
        /// Removes the entry.
        /// </summary>
        /// <param name="mediaUrl">The media URL.</param>
        public static void RemoveEntry(string mediaUrl)
        {
            lock (SyncRoot)
            {
                var entry = FindEntry(mediaUrl);
                if (entry == null) return;
                Entries.Remove(entry);
            }
        }

        /// <summary>
        /// Loads the Playlist from the default location
        /// </summary>
        public static void LoadEntries()
        {
            lock (SyncRoot)
            {
                Entries.Load(PlaylistFilePath);
            }
        }

        /// <summary>
        /// Saves the playlist to the default location
        /// </summary>
        public static void SaveEntries()
        {
            lock (SyncRoot)
            {
                Entries.Save(PlaylistFilePath);
            }
        }

        /// <summary>
        /// Gets the thumbnail.
        /// </summary>
        /// <param name="thumbnailFilename">The thumnail filename.</param>
        /// <returns>
        /// An image Source
        /// </returns>
        public static System.Windows.Media.ImageSource GetThumbnail(string thumbnailFilename)
        {
            if (string.IsNullOrWhiteSpace(thumbnailFilename))
                return default(System.Windows.Media.ImageSource);

            try
            {
                var thumbnail = new System.Windows.Media.Imaging.BitmapImage();
                thumbnail.BeginInit();
                thumbnail.CacheOption = System.Windows.Media.Imaging.BitmapCacheOption.OnLoad;
                thumbnail.UriSource = new Uri($"{Path.Combine(ThumbsDirectory, thumbnailFilename)}");
                thumbnail.EndInit();
                thumbnail.Freeze();
                return thumbnail;
            }
            catch { }

            return null;
        }

        private class ThumbnailGenerator
        {
            public static string SnapThumbnail(Image sourceImage)
            {
                using (var thumb = CreateThumbnail(sourceImage, Color.Black, 256, 144)) // 16:9 (in general)
                {
                    return SaveThumbnail(thumb, ThumbsDirectory);
                }
            }

            public static Image CreateThumbnail(Image sourceImage, Color background, int width, int height)
            {
                var outputSize = new Size(width, height);
                var proportionalSize = ComputeProportionalSize(outputSize, sourceImage.Size);
                var destinationPoint = new Point(
                    Convert.ToInt32((outputSize.Width - proportionalSize.Width) / 2d),
                    Convert.ToInt32((outputSize.Height - proportionalSize.Height) / 2d));

                // Resize the bitmap
                var outputImage = new Bitmap(width, height);
                using (var g = Graphics.FromImage(outputImage))
                {
                    g.Clear(background);
                    g.InterpolationMode = InterpolationMode.Bilinear;
                    g.DrawImage(
                        sourceImage,
                        new Rectangle(destinationPoint, proportionalSize),
                        new Rectangle(Point.Empty, sourceImage.Size),
                        GraphicsUnit.Pixel);

                    g.Flush();
                }

                return outputImage;
            }

            public static string SaveThumbnail(Image thumbnail, string baseDirectory)
            {
                var guid = Guid.NewGuid();
                var targetFilename = Path.Combine(Path.GetFullPath(baseDirectory), $"{guid.ToString()}.png");
                thumbnail.Save(targetFilename, ImageFormat.Png);
                return Path.GetFileName(targetFilename);
            }

            private static Size ComputeProportionalSize(Size maxSize, Size currentSize)
            {
                var maxScaleRatio = 0d;
                var currentScaleRatio = 0d;

                if (maxSize.Width < 1 || maxSize.Height < 1 || currentSize.Width < 1 || currentSize.Height < 1)
                    return Size.Empty;

                maxScaleRatio = maxSize.Width / (double)maxSize.Height;
                currentScaleRatio = currentSize.Width / (double)currentSize.Height;

                // Prepare the output
                var outputWidth = 0;
                var outputHeight = 0;

                if (maxScaleRatio < currentScaleRatio)
                {
                    outputWidth = Math.Min(maxSize.Width, currentSize.Width);
                    outputHeight = Convert.ToInt32(outputWidth / currentScaleRatio);
                }
                else
                {
                    outputHeight = Math.Min(maxSize.Height, currentSize.Height);
                    outputWidth = Convert.ToInt32(outputHeight * currentScaleRatio);
                }

                return new Size(outputWidth, outputHeight);
            }
        }
    }
}
