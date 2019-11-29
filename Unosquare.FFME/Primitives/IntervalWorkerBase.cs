namespace Unosquare.FFME.Primitives
{
    using System;
    using System.Runtime.CompilerServices;
    using System.Runtime.InteropServices;
    using System.Threading;
    using System.Threading.Tasks;

    internal abstract class IntervalWorkerBase : IWorker
    {
        private const int WantedTimingResolution = 1;
        private readonly object SyncLock = new object();
        private readonly Thread Thread;
        private readonly RealTimeClock CycleClock = new RealTimeClock();
        private readonly ManualResetEventSlim WantedStateCompleted = new ManualResetEventSlim(true);

        private CancellationTokenSource TokenSource = new CancellationTokenSource();

        private long m_Period;
        private int m_IsDisposed;
        private int m_IsDisposing;
        private int m_WorkerState = (int)WorkerState.Created;
        private int m_WantedWorkerState = (int)WorkerState.Running;

        protected IntervalWorkerBase(string name, TimeSpan period, IntervalWorkerMode mode)
        {
            Name = name;
            Period = period;
            Thread = new Thread(RunThread)
            {
                IsBackground = true,
                Name = $"{name}Thread",
                Priority = mode == IntervalWorkerMode.HighPrecision
                    ? ThreadPriority.AboveNormal
                    : ThreadPriority.Normal,
            };

            // Enable shorter scheduling times to save CPU
            if (TimingConfiguration.IsHighResolution)
            {
                var appliedResolution = TimingConfiguration.MinimumPeriod > WantedTimingResolution
                    ? TimingConfiguration.MinimumPeriod
                    : WantedTimingResolution;

                if (TimingConfiguration.ChangePeriod(appliedResolution))
                {
                    Resolution = appliedResolution;
                }
            }

            Mode = mode;
        }

        public string Name { get; }

        public int Resolution { get; } = 15;

        public IntervalWorkerMode Mode { get; }

        /// <inheritdoc />
        public TimeSpan Period
        {
            get => TimeSpan.FromTicks(Interlocked.Read(ref m_Period));
            set => Interlocked.Exchange(ref m_Period, value.Ticks < 0 ? 0 : value.Ticks);
        }

        /// <inheritdoc />
        public WorkerState WorkerState
        {
            get => (WorkerState)Interlocked.CompareExchange(ref m_WorkerState, 0, 0);
            protected set => Interlocked.Exchange(ref m_WorkerState, (int)value);
        }

        /// <inheritdoc />
        public bool IsDisposed
        {
            get => Interlocked.CompareExchange(ref m_IsDisposed, 0, 0) != 0;
            protected set => Interlocked.Exchange(ref m_IsDisposed, value ? 1 : 0);
        }

        /// <inheritdoc />
        public bool IsDisposing
        {
            get => Interlocked.CompareExchange(ref m_IsDisposing, 0, 0) != 0;
            protected set => Interlocked.Exchange(ref m_IsDisposing, value ? 1 : 0);
        }

        /// <summary>
        /// Gets or sets the desired state of the worker.
        /// </summary>
        protected WorkerState WantedWorkerState
        {
            get => (WorkerState)Interlocked.CompareExchange(ref m_WantedWorkerState, 0, 0);
            set => Interlocked.Exchange(ref m_WantedWorkerState, (int)value);
        }

        /// <summary>
        /// Gets the remaining cycle time.
        /// </summary>
        protected TimeSpan RemainingCycleTime => TimeSpan.FromTicks(Period.Ticks - CycleClock.Position.Ticks);

        /// <inheritdoc />
        public Task<WorkerState> StartAsync()
        {
            var awaitTask = false;
            lock (SyncLock)
            {
                if (IsDisposed || IsDisposing)
                    return Task.FromResult(WorkerState);

                Interrupt();

                if (WorkerState == WorkerState.Created)
                {
                    WantedWorkerState = WorkerState.Running;
                    WorkerState = WorkerState.Running;
                    Thread.Start();
                }
                else if (WorkerState == WorkerState.Paused)
                {
                    awaitTask = true;
                    WantedStateCompleted.Reset();
                    WantedWorkerState = WorkerState.Running;
                }

                if (!awaitTask)
                    return Task.FromResult(WorkerState);
            }

            return Task.Run(() =>
            {
                WantedStateCompleted.Wait();
                return WorkerState;
            });
        }

        /// <inheritdoc />
        public Task<WorkerState> PauseAsync()
        {
            lock (SyncLock)
            {
                if (IsDisposed || IsDisposing)
                    return Task.FromResult(WorkerState);

                Interrupt();

                if (WorkerState != WorkerState.Running)
                    return Task.FromResult(WorkerState);

                WantedStateCompleted.Reset();
                WantedWorkerState = WorkerState.Paused;
            }

            return Task.Run(() =>
            {
                WantedStateCompleted.Wait();
                return WorkerState;
            });
        }

        /// <inheritdoc />
        public Task<WorkerState> ResumeAsync()
        {
            lock (SyncLock)
            {
                if (IsDisposed || IsDisposing)
                    return Task.FromResult(WorkerState);

                Interrupt();

                if (WorkerState != WorkerState.Paused)
                    return Task.FromResult(WorkerState);

                WantedStateCompleted.Reset();
                WantedWorkerState = WorkerState.Running;
            }

            return Task.Run(() =>
            {
                WantedStateCompleted.Wait();
                return WorkerState;
            });
        }

        /// <inheritdoc />
        public Task<WorkerState> StopAsync()
        {
            lock (SyncLock)
            {
                if (IsDisposed || IsDisposing)
                    return Task.FromResult(WorkerState);

                Interrupt();

                if (WorkerState != WorkerState.Running && WorkerState != WorkerState.Paused)
                    return Task.FromResult(WorkerState);

                WantedStateCompleted.Reset();
                WantedWorkerState = WorkerState.Stopped;
            }

            return Task.Run(() =>
            {
                WantedStateCompleted.Wait();
                return WorkerState;
            });
        }

        /// <inheritdoc />
        public virtual void Dispose() => Dispose(true);

        /// <summary>
        /// Releases unmanaged and optionally managed resources.
        /// </summary>
        /// <param name="alsoManaged">Determines if managed resources hsould also be released.</param>
        protected virtual void Dispose(bool alsoManaged)
        {
            StopAsync().Wait();

            lock (SyncLock)
            {
                if (IsDisposed || IsDisposing)
                    return;

                IsDisposing = true;
                OnDisposing();
                CycleClock.Reset();
                WantedStateCompleted.Set();
                WantedStateCompleted.Dispose();
                TokenSource.Dispose();
                IsDisposed = true;
                IsDisposing = false;
            }
        }

        /// <summary>
        /// Handles the cycle logic exceptions.
        /// </summary>
        /// <param name="ex">The exception that was thrown.</param>
        protected abstract void OnCycleException(Exception ex);

        /// <summary>
        /// Represents the user defined logic to be executed on a single worker cycle.
        /// Check the cancellation token continuously if you need responsive interrupts.
        /// </summary>
        /// <param name="ct">The cancellation token.</param>
        protected abstract void ExecuteCycleLogic(CancellationToken ct);

        /// <summary>
        /// This method is called automatically when <see cref="Dispose()"/> is called.
        /// Makes sure you release all resources within this call.
        /// </summary>
        protected abstract void OnDisposing();

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        private void Interrupt() => TokenSource.Cancel();

        /// <summary>
        /// Implements an efficient delay.
        /// </summary>
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        private void Delay()
        {
            while (RemainingCycleTime.TotalMilliseconds > 0d)
            {
                if (WantedWorkerState != WorkerState || TokenSource.IsCancellationRequested)
                    break;

                var remainingMs = (int)RemainingCycleTime.TotalMilliseconds;
                if (Mode == IntervalWorkerMode.HighPrecision)
                {
                    if (remainingMs <= Resolution)
                        continue;

                    if (remainingMs > 0)
                        Thread.Sleep(remainingMs);
                }
                else
                {
                    if (remainingMs > 0)
                        Thread.Sleep(Math.Min(remainingMs, Resolution));
                }
            }

            CycleClock.Restart(RemainingCycleTime.TotalMilliseconds < 0 && Mode == IntervalWorkerMode.HighPrecision
                ? RemainingCycleTime.Negate()
                : TimeSpan.Zero);
        }

        /// <summary>
        /// Perofrms worker operations in a loop.
        /// </summary>
        /// <param name="state">The state.</param>
        private void RunThread(object state)
        {
            // Control variable setup
            CycleClock.Restart();

            while (WorkerState != WorkerState.Stopped)
            {
                lock (SyncLock)
                {
                    if (WantedWorkerState == WorkerState.Stopped)
                        break;

                    WorkerState = WantedWorkerState;
                    WantedStateCompleted.Set();
                }

                if (WorkerState == WorkerState.Running)
                {
                    if (TokenSource.IsCancellationRequested)
                    {
                        TokenSource.Dispose();
                        TokenSource = new CancellationTokenSource();
                    }

                    try
                    {
                        ExecuteCycleLogic(TokenSource.Token);
                    }
                    catch (Exception ex)
                    {
                        OnCycleException(ex);
                    }
                }

                Delay();
            }

            WorkerState = WorkerState.Stopped;
            WantedStateCompleted.Set();
        }

        [StructLayout(LayoutKind.Sequential)]
        private struct PeriodCapabilities
        {
            public int PeriodMin;

            public int PeriodMax;
        }

        private static class TimingConfiguration
        {
            private static readonly object SyncLock = new object();
            private static int? CurrentPeriod;

            static TimingConfiguration()
            {
                try
                {
                    var caps = default(PeriodCapabilities);
                    var result = NativeMethods.GetDeviceCapabilities(ref caps, Marshal.SizeOf<PeriodCapabilities>());
                    MinimumPeriod = caps.PeriodMin;
                    MaximumPeriod = caps.PeriodMax;
                    IsHighResolution = true;
                }
                catch
                {
                    MinimumPeriod = 16;
                    MaximumPeriod = 16;
                    IsHighResolution = false;
                }
            }

            public static bool IsHighResolution { get; }

            public static int MinimumPeriod { get; }

            public static int MaximumPeriod { get; }

            public static int? Period
            {
                get
                {
                    lock (SyncLock)
                        return CurrentPeriod;
                }
            }

            public static bool ChangePeriod(int newPeriod)
            {
                lock (SyncLock)
                {
                    if (!IsHighResolution)
                        return false;

                    if (CurrentPeriod.HasValue && CurrentPeriod.Value == newPeriod)
                        return true;

                    ResetPeriod();
                    var success = NativeMethods.BeginUsingPeriod(newPeriod) == 0;
                    if (success)
                        CurrentPeriod = newPeriod;

                    return success;
                }
            }

            public static bool ResetPeriod()
            {
                lock (SyncLock)
                {
                    if (!CurrentPeriod.HasValue)
                        return false;

                    var success = NativeMethods.EndUsingPeriod(CurrentPeriod.Value) == 0;
                    if (success)
                        CurrentPeriod = null;

                    return success;
                }
            }
        }

        private static class NativeMethods
        {
            private const string WinMM = "winmm.dll";

            [DllImport(WinMM, EntryPoint = "timeGetDevCaps")]
            public static extern int GetDeviceCapabilities(ref PeriodCapabilities ptc, int cbtc);

            [DllImport(WinMM, EntryPoint = "timeBeginPeriod")]
            public static extern int BeginUsingPeriod(int periodMillis);

            [DllImport(WinMM, EntryPoint = "timeEndPeriod")]
            public static extern int EndUsingPeriod(int periodMillis);
        }
    }
}
