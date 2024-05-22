namespace Unosquare.FFME.Primitives;

using System;
using System.Diagnostics;
using System.Runtime.CompilerServices;
using System.Threading;
using System.Threading.Tasks;

internal abstract class WorkerBase : IWorker
{
    private readonly object SyncLock = new();
    private readonly Stopwatch CycleClock = new();
    private readonly ManualResetEventSlim WantedStateCompleted = new(true);

    private int m_IsDisposed;
    private int m_IsDisposing;
    private int m_WorkerState = (int)WorkerState.Created;
    private int m_WantedWorkerState = (int)WorkerState.Running;
    private CancellationTokenSource TokenSource = new();

    protected WorkerBase(string name)
    {
        Name = name;
        CycleClock.Restart();
    }

    /// <summary>
    /// Gets the name of the worker.
    /// </summary>
    public string Name { get; }

    /// <inheritdoc />
    public WorkerState WorkerState
    {
        get => (WorkerState)Interlocked.CompareExchange(ref m_WorkerState, 0, 0);
        private set => Interlocked.Exchange(ref m_WorkerState, (int)value);
    }

    /// <inheritdoc />
    public bool IsDisposed
    {
        get => Interlocked.CompareExchange(ref m_IsDisposed, 0, 0) != 0;
        private set => Interlocked.Exchange(ref m_IsDisposed, value ? 1 : 0);
    }

    /// <summary>
    /// Gets a value indicating whether this instance is currently being disposed.
    /// </summary>
    /// <value>
    ///   <c>true</c> if this instance is disposing; otherwise, <c>false</c>.
    /// </value>
    protected bool IsDisposing
    {
        get => Interlocked.CompareExchange(ref m_IsDisposing, 0, 0) != 0;
        private set => Interlocked.Exchange(ref m_IsDisposing, value ? 1 : 0);
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
    /// Gets the elapsed time of the last cycle.
    /// </summary>
    protected TimeSpan LastCycleElapsed { get; private set; }

    /// <summary>
    /// Gets the elapsed time of the current cycle.
    /// </summary>
    protected TimeSpan CurrentCycleElapsed => CycleClock.Elapsed;

    /// <inheritdoc />
    public Task<WorkerState> StartAsync()
    {
        lock (SyncLock)
        {
            if (IsDisposed || IsDisposing)
                return Task.FromResult(WorkerState);

            if (WorkerState == WorkerState.Created)
            {
                WantedWorkerState = WorkerState.Running;
                WorkerState = WorkerState.Running;
                return Task.FromResult(WorkerState);
            }
            else if (WorkerState == WorkerState.Paused)
            {
                WantedStateCompleted.Reset();
                WantedWorkerState = WorkerState.Running;
            }
        }

        return RunWaitForWantedState();
    }

    /// <inheritdoc />
    public Task<WorkerState> PauseAsync()
    {
        // 2021-12-16 Moved this outside of the sync block, to avoid deadlock (#576)
        if (IsDisposed || IsDisposing)
            return Task.FromResult(WorkerState);
        lock (SyncLock)
        {
            if (WorkerState != WorkerState.Running)
                return Task.FromResult(WorkerState);

            WantedStateCompleted.Reset();
            WantedWorkerState = WorkerState.Paused;
        }

        return RunWaitForWantedState();
    }

    /// <inheritdoc />
    public Task<WorkerState> ResumeAsync()
    {
        lock (SyncLock)
        {
            if (IsDisposed || IsDisposing)
                return Task.FromResult(WorkerState);

            if (WorkerState != WorkerState.Paused)
                return Task.FromResult(WorkerState);

            WantedStateCompleted.Reset();
            WantedWorkerState = WorkerState.Running;
        }

        return RunWaitForWantedState();
    }

    /// <inheritdoc />
    public Task<WorkerState> StopAsync()
    {
        lock (SyncLock)
        {
            if (IsDisposed || IsDisposing)
                return Task.FromResult(WorkerState);

            if (WorkerState != WorkerState.Running && WorkerState != WorkerState.Paused)
                return Task.FromResult(WorkerState);

            WantedStateCompleted.Reset();
            WantedWorkerState = WorkerState.Stopped;
            Interrupt();
        }

        return RunWaitForWantedState();
    }

    /// <inheritdoc />
    public virtual void Dispose() => Dispose(true);

    /// <summary>
    /// Releases unmanaged and optionally managed resources.
    /// </summary>
    /// <param name="alsoManaged">Determines if managed resources hsould also be released.</param>
    protected virtual void Dispose(bool alsoManaged)
    {
        StopAsync().Wait(TimeSpan.FromSeconds(2));

        lock (SyncLock)
        {
            if (IsDisposed || IsDisposing)
                return;

            IsDisposing = true;
            WantedStateCompleted.Set();
            try { OnDisposing(); } catch { /* Ignore */ }
            CycleClock.Reset();
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
    protected virtual void OnCycleException(Exception ex)
    {
        // placeholder
    }

    /// <summary>
    /// This method is called automatically when <see cref="Dispose()"/> is called.
    /// Makes sure you release all resources within this call.
    /// </summary>
    protected virtual void OnDisposing()
    {
        // placeholder
    }

    /// <summary>
    /// Represents the user defined logic to be executed on a single worker cycle.
    /// Check the cancellation token continuously if you need responsive interrupts.
    /// </summary>
    /// <param name="ct">The cancellation token.</param>
    protected abstract void ExecuteCycleLogic(CancellationToken ct);

    /// <summary>
    /// Interrupts a cycle or a wait operation.
    /// </summary>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    protected void Interrupt() => TokenSource.Cancel();

    /// <summary>
    /// Tries to acquire a cycle for execution.
    /// </summary>
    /// <returns>True if a cycle should be executed.</returns>
    protected bool TryBeginCycle()
    {
        if (WorkerState == WorkerState.Created || WorkerState == WorkerState.Stopped)
            return false;

        LastCycleElapsed = CycleClock.Elapsed;
        CycleClock.Restart();

        lock (SyncLock)
        {
            WorkerState = WantedWorkerState;
            WantedStateCompleted.Set();

            if (WorkerState == WorkerState.Stopped)
                return false;
        }

        return true;
    }

    /// <summary>
    /// Executes the cyle calling the user-defined code.
    /// </summary>
    protected void ExecuteCyle()
    {
        lock (SyncLock)
        {
            // Recreate the token source -- applies to cycle logic and delay
            var ts = TokenSource;
            if (ts.IsCancellationRequested)
            {
                TokenSource = new CancellationTokenSource();
                ts.Dispose();
            }
        }

        if (WorkerState == WorkerState.Running)
        {
            try
            {
                ExecuteCycleLogic(TokenSource.Token);
            }
            catch (Exception ex)
            {
                OnCycleException(ex);
            }
        }
    }

    /// <summary>
    /// Returns a hot task that waits for the state of the worker to change.
    /// </summary>
    /// <returns>The awaitable state change task.</returns>
    private Task<WorkerState> RunWaitForWantedState() => Task.Run(() =>
    {
        while (!WantedStateCompleted.Wait(Constants.DefaultTimingPeriod))
            Interrupt();

        return WorkerState;
    });
}
