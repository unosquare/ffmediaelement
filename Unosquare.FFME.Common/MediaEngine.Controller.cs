namespace Unosquare.FFME
{
    using Commands;
    using Decoding;
    using Shared;
    using System;
    using System.Threading.Tasks;

    public partial class MediaEngine
    {
        #region Internal Members

        /// <summary>
        /// The command queue to be executed in the order they were sent.
        /// </summary>
        internal CommandManager Commands { get; }

        /// <summary>
        /// The underlying media container that provides access to
        /// individual media component streams
        /// </summary>
        internal MediaContainer Container { get; set; }

        #endregion

        #region Public API

        /// <summary>
        /// Opens the media using the specified URI.
        /// </summary>
        /// <param name="uri">The URI.</param>
        /// <returns>The awaitable task</returns>
        /// <exception cref="InvalidOperationException">Source</exception>
        public async Task Open(Uri uri)
        {
            if (uri != null)
            {
                await Commands.CloseMediaAsync();
                await Commands.OpenMediaAsync(uri);
            }
            else
            {
                await Commands.CloseMediaAsync();
            }
        }

        /// <summary>
        /// Opens the media using a custom media input stream.
        /// </summary>
        /// <param name="stream">The URI.</param>
        /// <returns>The awaitable task</returns>
        /// <exception cref="InvalidOperationException">Source</exception>
        public async Task Open(IMediaInputStream stream)
        {
            if (stream != null)
            {
                await Commands.CloseMediaAsync();
                await Commands.OpenMediaAsync(stream);
            }
            else
            {
                await Commands.CloseMediaAsync();
            }
        }

        /// <summary>
        /// Closes the currently loaded media.
        /// </summary>
        /// <returns>The awaitable task</returns>
        public async Task Close() =>
            await Commands.CloseMediaAsync();

        /// <summary>
        /// Requests new media options to be applied, including stream component selection.
        /// </summary>
        /// <returns>The awaitable command</returns>
        public async Task ChangeMedia() =>
            await Commands.ChangeMediaAsync();

        /// <summary>
        /// Begins or resumes playback of the currently loaded media.
        /// </summary>
        /// <returns>The awaitable command</returns>
        public async Task Play() =>
            await Commands.PlayMediaAsync();

        /// <summary>
        /// Pauses playback of the currently loaded media.
        /// </summary>
        /// <returns>The awaitable command</returns>
        public async Task Pause() =>
            await Commands.PauseMediaAsync();

        /// <summary>
        /// Pauses and rewinds the currently loaded media.
        /// </summary>
        /// <returns>The awaitable command</returns>
        public async Task Stop() =>
            await Commands.StopMediaAsync();

        /// <summary>
        /// Seeks to the specified position.
        /// </summary>
        /// <param name="position">New position for the player.</param>
        /// <returns>The awaitable command</returns>
        public async Task Seek(TimeSpan position) =>
            await Commands.SeekMediaAsync(position);

        /// <summary>
        /// Seeks a single frame forward.
        /// </summary>
        /// <returns>The awaitable command</returns>
        public async Task StepForward() =>
            await Commands.StepForwardAsync();

        /// <summary>
        /// Seeks a single frame backward.
        /// </summary>
        /// <returns>The awaitable command</returns>
        public async Task StepBackward() =>
            await Commands.StepBackwardAsync();

        #endregion
    }
}
