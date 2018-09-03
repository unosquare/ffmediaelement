namespace Unosquare.FFME.MacOS
{
    using AppKit;
    using System;
    using System.Threading.Tasks;
    using Unosquare.FFME.MacOS.Platform;

    public class MediaElement
    {
        private readonly MediaEngine MediaCore;

        #region Constructors

        static MediaElement()
        {
            MediaEngine.Initialize(MacPlatform.Current);
        }

        public MediaElement(NSImageView imageView)
        {
            this.ImageView = imageView;
            this.MediaCore = new MediaEngine(this, new MacMediaConnector(this));
        }

        #endregion

        #region Properties

        public NSImageView ImageView { get; }


        /// <summary>
        /// Gets or sets the FFmpeg path from which to load the FFmpeg binaries.
        /// You must set this path before setting the Source property for the first time on any instance of this control.
        /// Settng this property when FFmpeg binaries have been registered will throw an exception.
        /// </summary>
        public static string FFmpegDirectory
        {
            get => MediaEngine.FFmpegDirectory;
            set => MediaEngine.FFmpegDirectory = value;
        }

        #endregion

        #region Public methods

        public async Task Open(Uri uri)
        {
            await MediaCore.Open(uri);
        }

        #endregion
    }
}
