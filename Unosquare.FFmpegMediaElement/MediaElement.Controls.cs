namespace Unosquare.FFmpegMediaElement
{

    partial class MediaElement
    {

        /// <summary>
        /// Begins playback if not already playing
        /// </summary>
        public void Play()
        {
            if (this.Media == null && Source != null)
                this.OpenMedia(Source);

            if (this.Media == null)
                return;

            this.Media.Play();
        }

        /// <summary>
        /// Pauses media playback.
        /// </summary>
		public void Pause()
        {
            if (this.Media == null && Source != null)
                this.OpenMedia(Source);

            if (this.Media == null)
                return;

            this.Media.Pause();
        }

        /// <summary>
        /// Stops media playback.
        /// </summary>
		public void Stop()
        {
            if (this.Media == null && Source != null)
                this.OpenMedia(Source);

            if (this.Media == null)
                return;

            this.Media.Stop();
        }

        /// <summary>
        /// Closes the media source and releases its resources
        /// </summary>
		public void Close()
        {
            this.CloseMedia(true);
        }

    }
}
