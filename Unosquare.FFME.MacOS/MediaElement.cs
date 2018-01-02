namespace Unosquare.FFME.MacOS
{
    using AppKit;
    using Foundation;
    using System;
    using System.Threading.Tasks;
    using Unosquare.FFME.Core;
    using Unosquare.FFME.MacOS.Core;
    using Unosquare.FFME.MacOS.Rendering;

    public class MediaElement
    {
        private MediaElementCore mediaElementCore;

        #region Constructors

        static MediaElement()
        {
            MediaElementCore.Initialize(MacPlatform.Default);
        }

        public MediaElement(NSImageView imageView)
        {
            this.ImageView = imageView;
            this.mediaElementCore = new MediaElementCore(this, false, new MacEventConnector(this));
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
            get => MediaElementCore.FFmpegDirectory;
            set => MediaElementCore.FFmpegDirectory = value;
        }

        #endregion

        #region Public methods

        public async Task Open(Uri uri)
        {
            await mediaElementCore.Open(uri);
        }

        #endregion
    }
}
