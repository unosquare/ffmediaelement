namespace Unosquare.FFME.Commands
{
    using Engine;
    using Primitives;
    using System;
    using System.Runtime.CompilerServices;
    using System.Threading;
    using System.Threading.Tasks;

    internal partial class CommandManager
    {
        #region State Backing Fields

        private readonly AtomicInteger m_PendingPriorityCommand = new AtomicInteger(0);
        private readonly ManualResetEventSlim PriorityCommandCompleted = new ManualResetEventSlim(true);

        #endregion

        /// <summary>
        /// Gets a value indicating whether a priority command is pending.
        /// </summary>
        private bool IsPriorityCommandPending => PendingPriorityCommand != PriorityCommandType.None;

        #region Execution Helpers

        /// <summary>
        /// Executes boilerplate code that queues priority commands.
        /// </summary>
        /// <param name="command">The command.</param>
        /// <returns>An awaitable task</returns>
        private Task<bool> QueuePriorityCommand(PriorityCommandType command)
        {
            lock (SyncLock)
            {
                if (IsDisposed || IsDisposing || !State.IsOpen || IsDirectCommandPending || IsPriorityCommandPending)
                    return Task.FromResult(false);

                PendingPriorityCommand = command;
                PriorityCommandCompleted.Reset();

                var commandTask = new Task<bool>(() =>
                {
                    ResumeAsync().Wait();
                    PriorityCommandCompleted.Wait();
                    return true;
                });

                commandTask.Start();
                return commandTask;
            }
        }

        /// <summary>
        /// Clears the priority commands and marks the completion event as set.
        /// </summary>
        private void ClearPriorityCommands()
        {
            lock (SyncLock)
            {
                PendingPriorityCommand = PriorityCommandType.None;
                PriorityCommandCompleted.Set();
            }
        }

        #endregion

        #region Command Implementations

        /// <summary>
        /// Provides the implementation for the Play Media Command.
        /// </summary>
        /// <returns>True if the command was successful</returns>
        private bool CommandPlayMedia()
        {
            foreach (var renderer in MediaCore.Renderers.Values)
                renderer.OnPlay();

            State.UpdateMediaState(MediaPlaybackState.Play);

            return true;
        }

        /// <summary>
        /// Provides the implementation for the Pause Media Command.
        /// </summary>
        /// <returns>True if the command was successful</returns>
        private bool CommandPauseMedia()
        {
            if (State.CanPause == false)
                return false;

            MediaCore.PausePlayback();

            foreach (var renderer in MediaCore.Renderers.Values)
                renderer.OnPause();

            MediaCore.ChangePlaybackPosition(SnapPositionToBlockPosition(MediaCore.PlaybackPosition));
            State.UpdateMediaState(MediaPlaybackState.Pause);
            return true;
        }

        /// <summary>
        /// Provides the implementation for the Stop Media Command.
        /// </summary>
        /// <returns>True if the command was successful</returns>
        private bool CommandStopMedia()
        {
            if (State.IsSeekable == false)
                return false;

            MediaCore.ResetPlaybackPosition();

            SeekMedia(new SeekOperation(TimeSpan.MinValue, SeekMode.Stop), CancellationToken.None);

            foreach (var renderer in MediaCore.Renderers.Values)
                renderer.OnStop();

            State.UpdateMediaState(MediaPlaybackState.Stop);
            return true;
        }

        #endregion

        #region Implementation Helpers

        /// <summary>
        /// Returns the value of a discrete frame position of the main media component if possible.
        /// Otherwise, it simply rounds the position to the nearest millisecond.
        /// </summary>
        /// <param name="position">The position.</param>
        /// <returns>The snapped, discrete, normalized position</returns>
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        private TimeSpan SnapPositionToBlockPosition(TimeSpan position)
        {
            if (MediaCore.Container == null)
                return position.Normalize();

            var blocks = MediaCore.Blocks.Main(MediaCore.Container);
            if (blocks == null) return position.Normalize();

            return blocks.GetSnapPosition(position) ?? position.Normalize();
        }

        #endregion
    }
}
