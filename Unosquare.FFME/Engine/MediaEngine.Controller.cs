namespace Unosquare.FFME.Engine
{
    using Commands;
    using Common;
    using Container;
    using System;
    using System.Threading.Tasks;

    internal partial class MediaEngine
    {
        #region Internal Members

        /// <summary>
        /// The command queue to be executed in the order they were sent.
        /// </summary>
        internal CommandManager Commands { get; }

        /// <summary>
        /// The underlying media container that provides access to
        /// individual media component streams.
        /// </summary>
        internal MediaContainer Container { get; set; }

        #endregion

        #region Public API

        /// <summary>
        /// Opens the media using the specified URI.
        /// </summary>
        /// <param name="uri">The URI.</param>
        /// <returns>The awaitable task.</returns>
        /// <exception cref="InvalidOperationException">Source.</exception>
        public Task<bool> Open(Uri uri)
        {
            if (uri != null)
            {
                return Task.Run(async () =>
                {
                    await Commands.CloseMediaAsync();
                    return await Commands.OpenMediaAsync(uri);
                });
            }
            else
            {
                return Commands.CloseMediaAsync();
            }
        }

        /// <summary>
        /// Opens the media using a custom media input stream.
        /// </summary>
        /// <param name="stream">The URI.</param>
        /// <returns>The awaitable task.</returns>
        /// <exception cref="InvalidOperationException">Source.</exception>
        public Task<bool> Open(IMediaInputStream stream)
        {
            if (stream != null)
            {
                return Task.Run(async () =>
                {
                    await Commands.CloseMediaAsync();
                    return await Commands.OpenMediaAsync(stream);
                });
            }
            else
            {
                return Commands.CloseMediaAsync();
            }
        }

        /// <summary>
        /// Closes the currently loaded media.
        /// </summary>
        /// <returns>The awaitable task.</returns>
        public Task<bool> Close() =>
            Commands.CloseMediaAsync();

        /// <summary>
        /// Requests new media options to be applied, including stream component selection.
        /// </summary>
        /// <returns>The awaitable command.</returns>
        public Task<bool> ChangeMedia() =>
            Commands.ChangeMediaAsync();

        /// <summary>
        /// Begins or resumes playback of the currently loaded media.
        /// </summary>
        /// <returns>The awaitable command.</returns>
        public Task<bool> Play() =>
            Commands.PlayMediaAsync();

        /// <summary>
        /// Pauses playback of the currently loaded media.
        /// </summary>
        /// <returns>The awaitable command.</returns>
        public Task<bool> Pause() =>
            Commands.PauseMediaAsync();

        /// <summary>
        /// Pauses and rewinds the currently loaded media.
        /// </summary>
        /// <returns>The awaitable command.</returns>
        public Task<bool> Stop() =>
            Commands.StopMediaAsync();

        /// <summary>
        /// Seeks to the specified position.
        /// </summary>
        /// <param name="position">New position for the player.</param>
        /// <returns>The awaitable command.</returns>
        public Task<bool> Seek(TimeSpan position) =>
            Commands.SeekMediaAsync(position);

        /// <summary>
        /// Seeks a single frame forward.
        /// </summary>
        /// <returns>The awaitable command.</returns>
        public Task<bool> StepForward() =>
            Commands.StepForwardAsync();

        /// <summary>
        /// Seeks a single frame backward.
        /// </summary>
        /// <returns>The awaitable command.</returns>
        public Task<bool> StepBackward() =>
            Commands.StepBackwardAsync();

        #endregion
    }
}
