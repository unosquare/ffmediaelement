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
                await Commands.CloseMediaAsync().ConfigureAwait(false);
                return await Commands.OpenMediaAsync(uri).ConfigureAwait(false);
            }
            else
            {
                return await Commands.CloseMediaAsync().ConfigureAwait(false);
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
                await Commands.CloseMediaAsync().ConfigureAwait(false);
                return await Commands.OpenMediaAsync(stream).ConfigureAwait(false);
            }
            else
            {
                return await Commands.CloseMediaAsync().ConfigureAwait(false);
            }
        }

        /// <summary>
        /// Closes the currently loaded media.
        /// </summary>
        /// <returns>The awaitable task</returns>
        public async Task<bool> Close() =>
            await Commands.CloseMediaAsync().ConfigureAwait(false);

        /// <summary>
        /// Requests new media options to be applied, including stream component selection.
        /// </summary>
        /// <returns>The awaitable command</returns>
        public async Task<bool> ChangeMedia() =>
            await Commands.ChangeMediaAsync().ConfigureAwait(false);

        /// <summary>
        /// Begins or resumes playback of the currently loaded media.
        /// </summary>
        /// <returns>The awaitable command</returns>
        public async Task<bool> Play() =>
            await Commands.PlayMediaAsync().ConfigureAwait(false);

        /// <summary>
        /// Pauses playback of the currently loaded media.
        /// </summary>
        /// <returns>The awaitable command</returns>
        public async Task<bool> Pause() =>
            await Commands.PauseMediaAsync().ConfigureAwait(false);

        /// <summary>
        /// Pauses and rewinds the currently loaded media.
        /// </summary>
        /// <returns>The awaitable command</returns>
        public async Task<bool> Stop() =>
            await Commands.StopMediaAsync().ConfigureAwait(false);

        /// <summary>
        /// Seeks to the specified position.
        /// </summary>
        /// <param name="position">New position for the player.</param>
        /// <returns>The awaitable command</returns>
        public async Task<bool> Seek(TimeSpan position) =>
            await Commands.SeekMediaAsync(position).ConfigureAwait(false);

        /// <summary>
        /// Seeks a single frame forward.
        /// </summary>
        /// <returns>The awaitable command</returns>
        public async Task<bool> StepForward() =>
            await Commands.StepForwardAsync().ConfigureAwait(false);

        /// <summary>
        /// Seeks a single frame backward.
        /// </summary>
        /// <returns>The awaitable command</returns>
        public async Task<bool> StepBackward() =>
            await Commands.StepBackwardAsync().ConfigureAwait(false);

        #endregion
    }
}
