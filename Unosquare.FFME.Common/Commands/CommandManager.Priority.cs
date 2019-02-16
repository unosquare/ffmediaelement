﻿namespace Unosquare.FFME.Commands
{
    using Primitives;
    using Shared;
    using System;
    using System.Threading;
    using System.Threading.Tasks;

    internal partial class CommandManager
    {
        private readonly AtomicInteger m_PendingPriorityCommand = new AtomicInteger(0);
        private readonly ManualResetEventSlim PriorityCommandCompleted = new ManualResetEventSlim(true);

        private bool CanResumeMedia
        {
            get
            {
                if (MediaCore.State.HasMediaEnded)
                    return false;

                if (State.IsLiveStream)
                    return true;

                if (!State.IsSeekable)
                    return true;

                if (!State.NaturalDuration.HasValue)
                    return true;

                if (State.NaturalDuration == TimeSpan.MinValue)
                    return true;

                return MediaCore.WallClock < State.NaturalDuration;
            }
        }

        #region Execution Helpers

        private Task<bool> QueuePriorityCommand(PriorityCommandType command)
        {
            lock (SyncLock)
            {
                if (IsDisposed || IsDisposing || !MediaCore.State.IsOpen || HasPendingDirectCommands || !PriorityCommandCompleted.IsSet)
                    return Task.FromResult(false);

                PendingPriorityCommand = command;
                PriorityCommandCompleted.Reset();

                var commandTask = new Task<bool>(() =>
                {
                    ResumeAsync().Wait();
                    PriorityCommandCompleted.Wait();
                    return true;
                });

                commandTask.ConfigureAwait(false);
                commandTask.Start();

                return commandTask;
            }
        }

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

        private bool CommandPlayMedia()
        {
            if (!CanResumeMedia)
                return false;

            foreach (var renderer in MediaCore.Renderers.Values)
                renderer.Play();

            MediaCore.ResumePlayback();

            return true;
        }

        private bool CommandPauseMedia()
        {
            if (State.CanPause == false)
                return false;

            MediaCore.Clock.Pause();

            foreach (var renderer in MediaCore.Renderers.Values)
                renderer.Pause();

            MediaCore.ChangePosition(MediaCore.SnapPositionToBlockPosition(MediaCore.WallClock));
            State.UpdateMediaState(PlaybackStatus.Pause);
            return true;
        }

        private bool CommandStopMedia()
        {
            MediaCore.Clock.Reset();
            SeekMedia(new SeekOperation(TimeSpan.Zero, SeekMode.Stop), CancellationToken.None);

            foreach (var renderer in MediaCore.Renderers.Values)
                renderer.Stop();

            State.UpdateMediaState(PlaybackStatus.Stop);
            return true;
        }

        #endregion
    }
}
