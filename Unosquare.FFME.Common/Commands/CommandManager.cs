namespace Unosquare.FFME.Commands
{
    using Core;
    using Primitives;
    using Shared;
    using System;
    using System.Runtime.CompilerServices;
    using System.Text;
    using System.Threading;
    using System.Threading.Tasks;
    using Workers;

    internal sealed partial class CommandManager : TimerWorkerBase, IMediaWorker, ILoggingSource
    {
        private readonly object SyncLock = new object();

        public CommandManager(MediaEngine mediaCore)
            : base(nameof(CommandManager), Constants.Interval.HighPriority)
        {
            MediaCore = mediaCore;
        }

        public MediaEngine MediaCore { get; }

        ILoggingHandler ILoggingSource.LoggingHandler => MediaCore;

        private MediaEngineState State => MediaCore.State;

        private PriorityCommandType PendingPriorityCommand
        {
            get => (PriorityCommandType)m_PendingPriorityCommand.Value;
            set => m_PendingPriorityCommand.Value = (int)value;
        }

        #region Public API

        public Task<bool> OpenMediaAsync(Uri uri)
        {
            lock (SyncLock)
            {
                if (IsDisposed || IsDisposing || MediaCore.State.IsOpen || HasPendingDirectCommands)
                    return Task.FromResult(false);

                return ExecuteDirectCommand(DirectCommandType.Open, () => CommandOpenMedia(null, uri));
            }
        }

        public Task<bool> OpenMediaAsync(IMediaInputStream stream)
        {
            lock (SyncLock)
            {
                if (IsDisposed || IsDisposing || MediaCore.State.IsOpen || HasPendingDirectCommands)
                    return Task.FromResult(false);

                return ExecuteDirectCommand(DirectCommandType.Open, () => CommandOpenMedia(stream, stream.StreamUri));
            }
        }

        public Task<bool> CloseMediaAsync()
        {
            lock (SyncLock)
            {
                if (IsDisposed || IsDisposing || !MediaCore.State.IsOpen || HasPendingDirectCommands)
                    return Task.FromResult(false);

                return ExecuteDirectCommand(DirectCommandType.Close, () => CommandCloseMedia());
            }
        }

        public Task<bool> ChangeMediaAsync()
        {
            lock (SyncLock)
            {
                if (IsDisposed || IsDisposing || MediaCore.State.IsOpen == false || HasPendingDirectCommands)
                    return Task.FromResult(false);

                return ExecuteDirectCommand(DirectCommandType.Change, () => CommandChangeMedia(State.MediaState == PlaybackStatus.Play));
            }
        }

        public Task<bool> PlayMediaAsync() => QueuePriorityCommand(PriorityCommandType.Play);

        public Task<bool> PauseMediaAsync() => QueuePriorityCommand(PriorityCommandType.Pause);

        public Task<bool> StopMediaAsync() => QueuePriorityCommand(PriorityCommandType.Stop);

        public Task<bool> SeekMediaAsync(TimeSpan seekTarget) => QueueSeekCommand(seekTarget, SeekMode.Normal);

        public Task<bool> StepForwardAsync() => QueueSeekCommand(TimeSpan.Zero, SeekMode.StepForward);

        public Task<bool> StepBackwardAsync() => QueueSeekCommand(TimeSpan.Zero, SeekMode.StepBackward);

        public void WaitForSeekBlocks(CancellationToken ct) => SeekBlocksAvailable.Wait(ct);

        #endregion

        protected override void ExecuteCycleLogic(CancellationToken ct)
        {
            var priorityCommand = PendingPriorityCommand;

            if (priorityCommand != PriorityCommandType.None)
            {
                MediaCore.Workers.Pause(true);
            }

            switch (priorityCommand)
            {
                case PriorityCommandType.Play:
                    CommandPlayMedia();
                    break;
                case PriorityCommandType.Pause:
                    CommandPauseMedia();
                    break;
                case PriorityCommandType.Stop:
                    CommandStopMedia();
                    break;
                default:
                    break;
            }

            if (priorityCommand != PriorityCommandType.None)
            {
                ClearSeekCommands();
                ClearPriorityCommands();
                MediaCore.Workers.Resume(true);
                return;
            }

            while (true)
            {
                SeekOperation seekOperation;
                lock (SyncLock)
                {
                    seekOperation = QueuedSeekOperation;
                    QueuedSeekOperation = null;
                    QueuedSeekTask = null;
                }

                if (seekOperation == null)
                    break;

                SeekMedia(seekOperation, ct);
            }

            lock (SyncLock)
            {
                if (IsSeeking && QueuedSeekOperation == null)
                {
                    IsSeeking = false;

                    // Resume if requested
                    if (PlayAfterSeek == true)
                    {
                        PlayAfterSeek = false;
                        MediaCore.ResumePlayback();
                    }
                    else
                    {
                        if (MediaCore.State.MediaState != PlaybackStatus.Stop)
                            MediaCore.State.UpdateMediaState(PlaybackStatus.Pause);
                    }

                    MediaCore.SendOnSeekingEnded();
                    MediaCore.Workers.Resume(false);
                }
            }
        }

        protected override void OnCycleException(Exception ex)
        {
            throw new NotImplementedException();
        }

        protected override void OnDisposing()
        {
            // TODO: still need to call this from MediaCore.Dispose method.
            base.OnDisposing();
            DirectCommandCompleted.Set();
            ClearPriorityCommands();
            ClearSeekCommands();
            SeekBlocksAvailable.Set();

            DirectCommandCompleted.Dispose();
            PriorityCommandCompleted.Dispose();
            SeekBlocksAvailable.Dispose();
        }

        /// <summary>
        /// Outputs Reference Counter Results
        /// </summary>
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        private void LogReferenceCounter()
        {
            if (MediaEngine.Platform?.IsInDebugMode ?? true) return;
            if (RC.Current.InstancesByLocation.Count <= 0) return;

            var builder = new StringBuilder();
            builder.AppendLine("Unmanaged references were left alive. This is an indication that there is a memory leak.");
            foreach (var kvp in RC.Current.InstancesByLocation)
                builder.AppendLine($"    {kvp.Key,30} - Instances: {kvp.Value}");

            this.LogError(Aspects.ReferenceCounter, builder.ToString());
        }
    }
}
