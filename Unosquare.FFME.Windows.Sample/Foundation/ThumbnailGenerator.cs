namespace Unosquare.FFME.Windows.Sample.Foundation
{
    using System;
    using System.Drawing;
    using System.Drawing.Drawing2D;
    using System.Drawing.Imaging;
    using System.IO;
    using System.Windows.Media;
    using System.Windows.Media.Imaging;
    using Color = System.Drawing.Color;

    /// <summary>
    /// A class for creating, saving, and loading thumbnails.
    /// </summary>
    internal static class ThumbnailGenerator
    {
        /// <summary>
        /// Snaps the thumbnail.
        /// </summary>
        /// <param name="sourceImage">The source image.</param>
        /// <param name="targetPath">The target path.</param>
        /// <returns>The file name guid.</returns>
        public static string SnapThumbnail(Image sourceImage, string targetPath)
        {
            using (var thumb = CreateThumbnail(sourceImage, Color.Black, 256, 144)) // 16:9 (in general)
            {
                return SaveThumbnail(thumb, targetPath);
            }
        }

        /// <summary>
        /// Gets the thumbnail.
        /// </summary>
        /// <param name="targetPath">The target path.</param>
        /// <param name="thumbnailFilename">The thumbnail filename.</param>
        /// <returns>
        /// An image Source.
        /// </returns>
        public static ImageSource GetThumbnail(string targetPath, string thumbnailFilename)
        {
            var sourcePath = Path.Combine(targetPath, thumbnailFilename);
            if (string.IsNullOrWhiteSpace(thumbnailFilename) || File.Exists(sourcePath) == false)
                return default;

            try
            {
                var thumbnail = new BitmapImage();
                thumbnail.BeginInit();
                thumbnail.CacheOption = BitmapCacheOption.OnLoad;
                thumbnail.UriSource = new Uri(sourcePath);
                thumbnail.EndInit();
                thumbnail.Freeze();
                return thumbnail;
            }
            catch { /* Ignore exception and continue */ }

            return null;
        }

        /// <summary>
        /// Creates the thumbnail.
        /// </summary>
        /// <param name="sourceImage">The source image.</param>
        /// <param name="background">The background.</param>
        /// <param name="width">The width.</param>
        /// <param name="height">The height.</param>
        /// <returns>The thumbnail image.</returns>
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

        /// <summary>
        /// Saves the thumbnail.
        /// </summary>
        /// <param name="thumbnail">The thumbnail.</param>
        /// <param name="baseDirectory">The base directory.</param>
        /// <returns>The filename with extension.</returns>
        public static string SaveThumbnail(Image thumbnail, string baseDirectory)
        {
            var guid = Guid.NewGuid();
            var targetFilename = Path.Combine(Path.GetFullPath(baseDirectory), $"{guid.ToString()}.png");
            thumbnail.Save(targetFilename, ImageFormat.Png);
            return Path.GetFileName(targetFilename);
        }

        /// <summary>
        /// Computes the size of the proportional.
        /// </summary>
        /// <param name="maxSize">The maximum size.</param>
        /// <param name="currentSize">Size of the current.</param>
        /// <returns>A proportional size structure.</returns>
        private static Size ComputeProportionalSize(Size maxSize, Size currentSize)
        {
            if (maxSize.Width < 1 || maxSize.Height < 1 || currentSize.Width < 1 || currentSize.Height < 1)
                return Size.Empty;

            var maxScaleRatio = maxSize.Width / (double)maxSize.Height;
            var currentScaleRatio = currentSize.Width / (double)currentSize.Height;

            // Prepare the output
            int outputWidth;
            int outputHeight;

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
