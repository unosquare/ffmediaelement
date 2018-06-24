namespace Unosquare.FFME.Commands
{
    using Primitives;
    using Shared;
    using System;
    using System.Collections.Generic;
    using System.Linq;
    using System.Threading.Tasks;

    internal sealed class MediaCommandManager : IDisposable
    {
        private readonly List<MediaCommand> CommandQueue = new List<MediaCommand>(32);
        private readonly IWaitEvent DirectCommandEvent = null;
        private readonly object DirectLock = new object();
        private readonly object QueueLock = new object();
        private readonly object StatusLock = new object();

        private bool m_IsClosing = default;
        private bool m_IsOpening = default;
        private bool m_IsChanging = default;

        private DirectMediaCommand CurrentDirectCommand = null;
        private MediaCommand CurrentQueueCommand = null;

        public MediaCommandManager(MediaEngine mediaCore)
        {
            DirectCommandEvent = WaitEventFactory.Create(isCompleted: true, useSlim: true);
            MediaCore = mediaCore;
        }

        #region Properties

        public MediaEngine MediaCore { get; }

        public bool IsExecutingDirectCommand => DirectCommandEvent.IsInProgress;

        public bool IsClosing
        {
            get { lock (StatusLock) return m_IsClosing; }
        }

        public bool IsOpening
        {
            get { lock (StatusLock) return m_IsOpening; }
        }

        public bool IsChanging
        {
            get { lock (StatusLock) return m_IsChanging; }
        }

        public bool HasQueuedCommands
        {
            get { lock (QueueLock) return CommandQueue.Count > 0; }
        }

        public bool HasQueuedSeekCommands
        {
            get { lock (QueueLock) return CommandQueue.Any(c => c.CommandType == MediaCommandType.Seek); }
        }

        public bool CanExecuteQueuedCommands
        {
            get
            {
                lock (StatusLock)
                {
                    if (m_IsClosing || IsDisposed) return false;
                    if ((m_IsOpening || MediaCore.State.IsOpen) == false) return false;

                    return true;
                }
            }
        }

        public bool IsDisposed { get; private set; }

        #endregion

        #region Public API

        public async Task<bool> OpenAsync(Uri uri)
        {
            if (MediaCore.State.IsOpen || IsDisposed || IsExecutingDirectCommand)
                return false;

            return await ExecuteDirectCommand(new DirectOpenCommand(MediaCore, uri));
        }

        public async Task<bool> OpenAsync(IMediaInputStream stream)
        {
            if (MediaCore.State.IsOpen || IsDisposed || IsExecutingDirectCommand)
                return false;

            return await ExecuteDirectCommand(new DirectOpenCommand(MediaCore, stream));
        }

        public async Task<bool> CloseAsync()
        {
            if (MediaCore.State.IsOpen == false || IsDisposed)
                return false;

            var currentCommand = GetCurrentDirectCommand(MediaCommandType.Close);

            if (currentCommand != null)
                return await currentCommand.Awaiter;
            else if (IsExecutingDirectCommand)
                return false;
            else
                return await ExecuteDirectCommand(new DirectCloseCommand(MediaCore));
        }

        public async Task<bool> ChangeMediaAsync()
        {
            if (MediaCore.State.IsOpen == false || IsDisposed)
                return false;

            var currentCommand = GetCurrentDirectCommand(MediaCommandType.ChangeMedia);

            if (currentCommand != null)
                return await currentCommand.Awaiter;
            else if (IsExecutingDirectCommand)
                return false;
            else
                return await ExecuteDirectCommand(new DirectChangeCommand(MediaCore));
        }

        public async Task<bool> PlayAsync() =>
            await ExecuteQueuedProrityCommand(MediaCommandType.Play);

        public async Task<bool> PauseAsync() =>
            await ExecuteQueuedProrityCommand(MediaCommandType.Pause);

        public async Task<bool> StopAsync() =>
            await ExecuteQueuedProrityCommand(MediaCommandType.Stop);

        public async Task<bool> SeekAsync(TimeSpan target) =>
            await ExecuteQueuedDelayedCommand(MediaCommandType.Seek, target);

        public async Task<bool> SetSpeedRatioAsync(double target) =>
            await ExecuteQueuedDelayedCommand(MediaCommandType.SpeedRatio, target);

        public void WaitForDirectCommand() =>
            DirectCommandEvent.Wait();

        public void ClearQueuedCommands()
        {
            lock (QueueLock)
            {
                for (var i = CommandQueue.Count - 1; i >= 0; i--)
                    CommandQueue[i].Dispose();

                CommandQueue.Clear();
            }
        }

        public void ExecuteNextQueuedCommand()
        {
            if (DirectCommandEvent.IsInProgress)
                return;

            MediaCommand command = null;
            lock (QueueLock)
            {
                if (CommandQueue.Count > 0)
                {
                    command = CommandQueue[0];
                    CommandQueue.RemoveAt(0);
                    CurrentQueueCommand = command;
                }
            }

            try { command?.Execute(); }
            catch { throw; }
            finally { lock (QueueLock) { CurrentQueueCommand = null; } }
        }

        public void Dispose() =>
            Dispose(true);

        #endregion

        #region Private Methods

        private MediaCommand GetCurrentDirectCommand(MediaCommandType commandType)
        {
            var currentCommand = default(MediaCommand);
            lock (DirectLock)
            {
                if (CurrentDirectCommand != null &&
                    CurrentDirectCommand.CommandType == MediaCommandType.Close)
                {
                    currentCommand = CurrentDirectCommand;
                }
            }

            return currentCommand;
        }

        private async Task<bool> ExecuteDirectCommand(DirectMediaCommand command)
        {
            lock (DirectLock)
            {
                // Prevent running a new priority event if one is already in progress
                if (DirectCommandEvent.IsInProgress)
                {
                    command.Dispose();
                    return false;
                }

                // Signal the workers they need to wait
                DirectCommandEvent.Begin();
                CurrentDirectCommand = command;

                // Update the state
                lock (StatusLock)
                {
                    m_IsOpening = command.CommandType == MediaCommandType.Open;
                    m_IsClosing = command.CommandType == MediaCommandType.Close;
                    m_IsChanging = command.CommandType == MediaCommandType.ChangeMedia;
                }
            }

            // Wait for cycles to complete.
            // Cycles must wait for priority commands before continuing
            if (MediaCore.State.IsOpen)
            {
                MediaCore.FrameDecodingCycle.Wait();
                MediaCore.PacketReadingCycle.Wait();
            }

            ClearQueuedCommands();
            command.BeginExecute();

            try { return await command.Awaiter; }
            catch { throw; }
            finally
            {
                lock (StatusLock)
                {
                    m_IsOpening = false;
                    m_IsClosing = false;
                    m_IsChanging = false;
                }

                lock (DirectLock)
                {
                    command.PostProcess();
                    DirectCommandEvent.Complete();
                    CurrentDirectCommand = null;
                }
            }
        }

        private async Task<bool> ExecuteQueuedProrityCommand(MediaCommandType commandType)
        {
            if (CanExecuteQueuedCommands == false) return false;

            MediaCommand currentCommand = null;
            lock (QueueLock)
            {
                if (CurrentQueueCommand != null &&
                    CurrentQueueCommand.CommandType == commandType)
                {
                    currentCommand = CurrentQueueCommand;
                }

                if (currentCommand == null)
                {
                    var queuedCommand = CommandQueue
                        .FirstOrDefault(c => c.CommandType == commandType);

                    if (queuedCommand != null)
                        currentCommand = queuedCommand;
                }
            }

            if (currentCommand != null)
                return await currentCommand.Awaiter;

            MediaCommand command = null;

            switch (commandType)
            {
                case MediaCommandType.Play:
                    command = new PlayCommand(MediaCore);
                    break;
                case MediaCommandType.Pause:
                    command = new PauseCommand(MediaCore);
                    break;
                case MediaCommandType.Stop:
                    command = new StopCommand(MediaCore);
                    break;
                default:
                    throw new ArgumentException($"{nameof(commandType)} is of invalid type '{commandType}'");
            }

            lock (QueueLock)
            {
                ClearQueuedCommands();
                CommandQueue.Add(command);
            }

            return await command.Awaiter;
        }

        private async Task<bool> ExecuteQueuedDelayedCommand<T>(MediaCommandType commandType, T argument)
        {
            if (CanExecuteQueuedCommands == false) return false;

            var timeSpanArgument = commandType == MediaCommandType.Seek ?
                (TimeSpan)Convert.ChangeType(argument, typeof(TimeSpan)) :
                TimeSpan.Zero;

            var doubleArgument = commandType == MediaCommandType.SpeedRatio ?
                (double)Convert.ChangeType(argument, typeof(double)) : default;

            MediaCommand currentCommand = null;
            lock (QueueLock)
            {
                currentCommand = CommandQueue
                    .FirstOrDefault(c => c.CommandType == commandType);

                if (currentCommand != null)
                {
                    switch (commandType)
                    {
                        case MediaCommandType.Seek:
                            (currentCommand as SeekCommand).TargetPosition = timeSpanArgument;
                            break;
                        case MediaCommandType.SpeedRatio:
                            (currentCommand as SpeedRatioCommand).SpeedRatio = doubleArgument;
                            break;
                        default:
                            throw new ArgumentException($"{nameof(commandType)} is of invalid type '{commandType}'");
                    }
                }
            }

            if (currentCommand != null)
                return await currentCommand.Awaiter;

            MediaCommand command = null;

            switch (commandType)
            {
                case MediaCommandType.Seek:
                    command = new SeekCommand(MediaCore, timeSpanArgument);
                    break;
                case MediaCommandType.SpeedRatio:
                    command = new SpeedRatioCommand(MediaCore, doubleArgument);
                    break;
                default:
                    throw new ArgumentException($"{nameof(commandType)} is of invalid type '{commandType}'");
            }

            lock (QueueLock)
                CommandQueue.Add(command);

            return await command.Awaiter;
        }

        private void Dispose(bool alsoManaged)
        {
            if (!IsDisposed)
            {
                if (alsoManaged)
                {
                    DirectCommandEvent.Wait();
                    ExecuteDirectCommand(new DirectCloseCommand(MediaCore)).GetAwaiter().GetResult();
                    ClearQueuedCommands(); // TODO: might be unncessary

                    // TODO: dispose managed state (managed objects).
                }

                // TODO: free unmanaged resources (unmanaged objects) and override a finalizer below.
                // TODO: set large fields to null.
                IsDisposed = true;
            }
        }

        #endregion
    }
}
