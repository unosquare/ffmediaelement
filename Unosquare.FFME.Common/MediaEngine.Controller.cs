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
        public async Task<bool> Open(Uri uri)
        {
            if (uri != null)
            {
                await Commands.CloseMediaAsync();
                return await Commands.OpenMediaAsync(uri);
            }
            else
            {
                return await Commands.CloseMediaAsync();
            }
        }

        /// <summary>
        /// Opens the media using a custom media input stream.
        /// </summary>
        /// <param name="stream">The URI.</param>
        /// <returns>The awaitable task</returns>
        /// <exception cref="InvalidOperationException">Source</exception>
        public async Task<bool> Open(IMediaInputStream stream)
        {
            if (stream != null)
            {
                await Commands.CloseMediaAsync();
                return await Commands.OpenMediaAsync(stream);
            }
            else
            {
                return await Commands.CloseMediaAsync();
            }
        }

        /// <summary>
        /// Closes the currently loaded media.
        /// </summary>
        /// <returns>The awaitable task</returns>
        public async Task<bool> Close() =>
            await Commands.CloseMediaAsync();

        /// <summary>
        /// Requests new media options to be applied, including stream component selection.
        /// </summary>
        /// <returns>The awaitable command</returns>
        public async Task<bool> ChangeMedia() =>
            await Commands.ChangeMediaAsync();

        /// <summary>
        /// Begins or resumes playback of the currently loaded media.
        /// </summary>
        /// <returns>The awaitable command</returns>
        public async Task<bool> Play() =>
            await Commands.PlayMediaAsync();

        /// <summary>
        /// Pauses playback of the currently loaded media.
        /// </summary>
        /// <returns>The awaitable command</returns>
        public async Task<bool> Pause() =>
            await Commands.PauseMediaAsync();

        /// <summary>
        /// Pauses and rewinds the currently loaded media.
        /// </summary>
        /// <returns>The awaitable command</returns>
        public async Task<bool> Stop() =>
            await Commands.StopMediaAsync();

        /// <summary>
        /// Seeks to the specified position.
        /// </summary>
        /// <param name="position">New position for the player.</param>
        /// <returns>The awaitable command</returns>
        public async Task<bool> Seek(TimeSpan position) =>
            await Commands.SeekMediaAsync(position);

        /// <summary>
        /// Seeks a single frame forward.
        /// </summary>
        /// <returns>The awaitable command</returns>
        public async Task<bool> StepForward() =>
            await Commands.StepForwardAsync();

        /// <summary>
        /// Seeks a single frame backward.
        /// </summary>
        /// <returns>The awaitable command</returns>
        public async Task<bool> StepBackward() =>
            await Commands.StepBackwardAsync();

        #endregion
    }
}
