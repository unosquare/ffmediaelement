namespace Unosquare.FFME.Workers
{
    using System;
    using System.Threading.Tasks;

    /// <summary>
    /// Provides a easy accessors to the Read, Decode and Render workers.
    /// </summary>
    /// <seealso cref="IDisposable" />
    internal sealed class MediaWorkerSet : IDisposable
    {
        private readonly object SyncLock = new object();
        private readonly IMediaWorker[] Workers = new IMediaWorker[3];
        private bool m_IsDisposed;

        /// <summary>
        /// Initializes a new instance of the <see cref="MediaWorkerSet"/> class.
        /// </summary>
        /// <param name="mediaCore">The media core.</param>
        public MediaWorkerSet(MediaEngine mediaCore)
        {
            MediaCore = mediaCore;

            Reading = new PacketReadingWorker(mediaCore);
            Decoding = new FrameDecodingWorker(mediaCore);
            Rendering = new BlockRenderingWorker(mediaCore);

            Workers[(int)MediaWorkerType.Read] = Reading;
            Workers[(int)MediaWorkerType.Decode] = Decoding;
            Workers[(int)MediaWorkerType.Render] = Rendering;
        }

        /// <summary>
        /// Gets the media engine that owns this set of workers.
        /// </summary>
        public MediaEngine MediaCore { get; }

        /// <summary>
        /// Gets the packet reading worker.
        /// </summary>
        public PacketReadingWorker Reading { get; }

        /// <summary>
        /// Gets the frame decoding worker.
        /// </summary>
        public FrameDecodingWorker Decoding { get; }

        /// <summary>
        /// Gets the block rendering worker.
        /// </summary>
        public BlockRenderingWorker Rendering { get; }

        /// <summary>
        /// Gets a value indicating whether this instance is disposed.
        /// </summary>
        public bool IsDisposed
        {
            get { lock (SyncLock) return m_IsDisposed; }
            private set { lock (SyncLock) m_IsDisposed = value; }
        }

        /// <summary>
        /// Gets the <see cref="IMediaWorker"/> with the specified worker type.
        /// </summary>
        /// <value>
        /// The <see cref="IMediaWorker"/>.
        /// </value>
        /// <param name="workerType">Type of the worker.</param>
        /// <returns>The matching worker</returns>
        public IMediaWorker this[MediaWorkerType workerType] => Workers[(int)workerType];

        /// <inheritdoc />
        public void Dispose() => Dispose(true);

        /// <summary>
        /// Pauses the specified wrokers.
        /// </summary>
        /// <param name="wait">if set to <c>true</c> waits for the operation to complete.</param>
        /// <param name="read">if set to <c>true</c> executes the opration on the reading worker.</param>
        /// <param name="decode">if set to <c>true</c> executes the opration on the decoding worker.</param>
        /// <param name="render">if set to <c>true</c> executes the opration on the rendering worker.</param>
        public void Pause(bool wait, bool read, bool decode, bool render)
        {
            if (IsDisposed) return;

            var tasks = new Task[(read ? 1 : 0) + (decode ? 1 : 0) + (render ? 1 : 0)];
            var index = 0;
            if (read)
            {
                tasks[index] = Reading.PauseAsync();
                index++;
            }

            if (decode)
            {
                tasks[index] = Decoding.PauseAsync();
                index++;
            }

            if (render)
            {
                tasks[index] = Rendering.PauseAsync();
            }

            if (wait)
                Task.WaitAll(tasks);
        }

        /// <summary>
        /// Pauses the specified wait.
        /// </summary>
        /// <param name="wait">if set to <c>true</c> waits for the operation to complete.</param>
        public void Pause(bool wait) => Pause(wait, true, true, true);

        /// <summary>
        /// Resumes the specified wrokers.
        /// </summary>
        /// <param name="wait">if set to <c>true</c> waits for the operation to complete.</param>
        /// <param name="read">if set to <c>true</c> executes the opration on the reading worker.</param>
        /// <param name="decode">if set to <c>true</c> executes the opration on the decoding worker.</param>
        /// <param name="render">if set to <c>true</c> executes the opration on the rendering worker.</param>
        public void Resume(bool wait, bool read, bool decode, bool render)
        {
            if (IsDisposed) return;

            var tasks = new Task[(read ? 1 : 0) + (decode ? 1 : 0) + (render ? 1 : 0)];
            var index = 0;
            if (read)
            {
                tasks[index] = Reading.ResumeAsync();
                index++;
            }

            if (decode)
            {
                tasks[index] = Decoding.ResumeAsync();
                index++;
            }

            if (render)
            {
                tasks[index] = Rendering.ResumeAsync();
            }

            if (wait)
                Task.WaitAll(tasks);
        }

        /// <summary>
        /// Resumes the workers.
        /// </summary>
        /// <param name="wait">if set to <c>true</c> waits for the operation to complete.</param>
        public void Resume(bool wait) => Resume(wait, true, true, true);

        /// <summary>
        /// Starts the workers.
        /// </summary>
        public void Start()
        {
            if (IsDisposed) return;

            var tasks = new Task[Workers.Length];
            for (var i = 0; i < Workers.Length; i++)
            {
                tasks[i] = Workers[i].StartAsync();
            }

            Task.WaitAll(tasks);
        }

        /// <summary>
        /// Releases unmanaged and - optionally - managed resources.
        /// </summary>
        /// <param name="alsoManaged"><c>true</c> to release both managed and unmanaged resources; <c>false</c> to release only unmanaged resources.</param>
        private void Dispose(bool alsoManaged)
        {
            lock (SyncLock)
            {
                if (IsDisposed) return;
                IsDisposed = true;

                if (alsoManaged == false) return;

                Pause(true);
                foreach (var worker in Workers)
                    worker.Dispose();
            }
        }
    }
}
