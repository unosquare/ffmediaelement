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
                await Commands.CloseAsync();
                await Commands.OpenAsync(uri);
            }
            else
            {
                await Commands.CloseAsync();
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
                await Commands.CloseAsync();
                await Commands.OpenAsync(stream);
            }
            else
            {
                await Commands.CloseAsync();
            }
        }

        /// <summary>
        /// Closes the currently loaded media.
        /// </summary>
        /// <returns>The awaitable task</returns>
        public async Task Close() =>
            await Commands.CloseAsync();

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
            await Commands.PlayAsync();

        /// <summary>
        /// Pauses playback of the currently loaded media.
        /// </summary>
        /// <returns>The awaitable command</returns>
        public async Task Pause() =>
            await Commands.PauseAsync();

        /// <summary>
        /// Pauses and rewinds the currently loaded media.
        /// </summary>
        /// <returns>The awaitable command</returns>
        public async Task Stop() =>
            await Commands.StopAsync();

        /// <summary>
        /// Seeks to the specified position.
        /// </summary>
        /// <param name="position">New position for the player.</param>
        /// <returns>The awaitable command</returns>
        public async Task Seek(TimeSpan position) =>
            await Commands.SeekAsync(position);

        #endregion
    }
}
