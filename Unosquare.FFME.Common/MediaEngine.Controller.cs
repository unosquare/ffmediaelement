namespace Unosquare.FFME
{
    using Commands;
    using Decoding;
    using Primitives;
    using Shared;
    using System;
    using System.Threading.Tasks;

    public partial class MediaEngine
    {
        #region Internal Members

        /// <summary>
        /// The open or close command done signalling object.
        /// Open and close are synchronous commands.
        /// </summary>
        private readonly IWaitEvent SynchronousCommandDone = WaitEventFactory.Create(isCompleted: true, useSlim: true);

        /// <summary>
        /// The command queue to be executed in the order they were sent.
        /// </summary>
        internal MediaCommandManager Commands { get; private set; }

        /// <summary>
        /// The underlying media container that provides access to
        /// individual media component streams
        /// </summary>
        internal MediaContainer Container { get; set; } = null;

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
            if (BeginSynchronousCommand() == false) return;

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
                EndSynchronousCommand();
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
            if (BeginSynchronousCommand() == false) return;

            try
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
            catch
            {
                throw;
            }
            finally
            {
                EndSynchronousCommand();
            }
        }

        /// <summary>
        /// Closes the currently loaded media.
        /// </summary>
        /// <returns>The awaitable task</returns>
        public async Task Close()
        {
            if (BeginSynchronousCommand() == false) return;

            try
            { await Commands.CloseAsync(); }
            catch (OperationCanceledException) { }
            catch { throw; }
            finally
            {
                EndSynchronousCommand();
            }
        }

        /// <summary>
        /// Requests new media options to be applied, including stream component selection.
        /// </summary>
        /// <returns>The awaitable command</returns>
        public async Task ChangeMedia()
        {
            try { await Commands.ChangeMediaAsync(); }
            catch (OperationCanceledException) { }
            catch { throw; }
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

        #region Synchronous Command Management

        /// <summary>
        /// Begins a synchronous command by locking the internal wait handle.
        /// </summary>
        /// <returns>True if successful, false if unsuccessful</returns>
        private bool BeginSynchronousCommand()
        {
            if (IsDisposed) return false;

            var waitHandle = SynchronousCommandDone;
            if (waitHandle == null || waitHandle.IsValid == false || waitHandle.IsInProgress)
                return false;

            try
            {
                waitHandle.Wait();
                waitHandle.Begin();
                return true;
            }
            catch { }

            return false;
        }

        /// <summary>
        /// Ends a synchronous command by releasing the internal wait handle.
        /// </summary>
        private void EndSynchronousCommand()
        {
            if (IsDisposed) return;

            var waitHandle = SynchronousCommandDone;
            if (waitHandle == null || waitHandle.IsValid == false) return;

            try { waitHandle.Complete(); }
            catch { }
        }

        #endregion
    }
}
