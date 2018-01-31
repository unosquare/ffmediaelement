namespace Unosquare.FFME
{
    using Commands;
    using Core;
    using Decoding;
    using System;
    using System.Threading;
    using System.Threading.Tasks;

    public partial class MediaEngine
    {
        #region Internal Members

        /// <summary>
        /// The open or close command done signalling object.
        /// Open and close are synchronous commands.
        /// </summary>
        private readonly ManualResetEvent OpenOrCloseCommandDone = new ManualResetEvent(true);

        /// <summary>
        /// The command queue to be executed in the order they were sent.
        /// </summary>
        internal MediaCommandManager Commands { get; private set; }

        /// <summary>
        /// Represents a real-time time measuring device.
        /// Rendering media should occur as requested by the clock.
        /// </summary>
        internal RealTimeClock Clock { get; } = new RealTimeClock();

        /// <summary>
        /// The underlying media container that provides access to
        /// individual media component streams
        /// </summary>
        internal MediaContainer Container { get; set; } = null;

        #endregion

        #region Public API

        /// <summary>
        /// Opens the specified URI.
        /// </summary>
        /// <param name="uri">The URI.</param>
        /// <returns>The awaitable task</returns>
        /// <exception cref="InvalidOperationException">Source</exception>
        public async Task Open(Uri uri)
        {
            OpenOrCloseCommandDone.WaitOne();
            OpenOrCloseCommandDone.Reset();

            try
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
            catch
            {
                throw;
            }
            finally
            {
                OpenOrCloseCommandDone.Set();
            }
        }

        /// <summary>
        /// Closes the currently loaded media.
        /// </summary>
        /// <returns>The awaitable task</returns>
        public async Task Close()
        {
            OpenOrCloseCommandDone.WaitOne();
            OpenOrCloseCommandDone.Reset();

            try
            { await Commands.CloseAsync(); }
            catch (OperationCanceledException) { }
            catch { throw; }
            finally
            {
                OpenOrCloseCommandDone.Set();
            }
        }

        /// <summary>
        /// Begins or resumes playback of the currently loaded media.
        /// </summary>
        /// <returns>The awaitable command</returns>
        public async Task Play()
        {
            try { await Commands.PlayAsync(); }
            catch (OperationCanceledException) { }
            catch { throw; }
        }

        /// <summary>
        /// Pauses playback of the currently loaded media.
        /// </summary>
        /// <returns>The awaitable command</returns>
        public async Task Pause()
        {
            try { await Commands.PauseAsync(); }
            catch (OperationCanceledException) { }
            catch { throw; }
        }

        /// <summary>
        /// Pauses and rewinds the currently loaded media.
        /// </summary>
        /// <returns>The awaitable command</returns>
        public async Task Stop()
        {
            try { await Commands.StopAsync(); }
            catch (OperationCanceledException) { }
            catch { throw; }
        }

        /// <summary>
        /// Seeks to the specified position.
        /// </summary>
        /// <param name="position">New position for the player.</param>
        public void RequestSeek(TimeSpan position) => Commands.EnqueueSeek(position);

        /// <summary>
        /// Sets the specified playback speed ratio.
        /// </summary>
        /// <param name="targetSpeedRatio">New playback speed ratio.</param>
        public void RequestSpeedRatio(double targetSpeedRatio) => Commands.EnqueueSpeedRatio(targetSpeedRatio);

        #endregion
    }
}
