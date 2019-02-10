namespace Unosquare.FFME.Workers
{
    using System;
    using System.Threading.Tasks;

    internal sealed class MediaWorkerSet : IDisposable
    {
        private readonly object SyncLock = new object();
        private readonly IMediaWorker[] Workers = new IMediaWorker[3];
        private bool m_IsDisposed = false;

        public MediaWorkerSet(MediaEngine mediaCore)
        {
            MediaCore = mediaCore;

            ReadingWorker = new PacketReadingWorker(mediaCore);
            DecodeWorker = new FrameDecodingWorker(mediaCore);
            RenderWorker = new BlockRenderingWorker(mediaCore);

            Workers[(int)MediaWorkerType.Read] = ReadingWorker;
            Workers[(int)MediaWorkerType.Decode] = DecodeWorker;
            Workers[(int)MediaWorkerType.Render] = RenderWorker;
        }

        public MediaEngine MediaCore { get; }

        public PacketReadingWorker ReadingWorker { get; }

        public FrameDecodingWorker DecodeWorker { get; }

        public BlockRenderingWorker RenderWorker { get; }

        /// <summary>
        /// Gets a value indicating whether this instance is disposed.
        /// </summary>
        /// <value>
        ///   <c>true</c> if this instance is disposed; otherwise, <c>false</c>.
        /// </value>
        public bool IsDisposed
        {
            get { lock (SyncLock) return m_IsDisposed; }
            private set { lock (SyncLock) m_IsDisposed = value; }
        }

        public IMediaWorker this[MediaWorkerType workerType] => Workers[(int)workerType];

        /// <summary>
        /// Performs application-defined tasks associated with freeing, releasing, or resetting unmanaged resources.
        /// </summary>
        public void Dispose() => Dispose(true);

        public void Pause()
        {
            if (IsDisposed) return;

            var tasks = new Task[Workers.Length];
            for (var i = 0; i < Workers.Length; i++)
            {
                tasks[i] = Workers[i].PauseAsync();
            }

            Task.WaitAll(tasks);
        }

        public void Resume()
        {
            if (IsDisposed) return;

            var tasks = new Task[Workers.Length];
            for (var i = 0; i < Workers.Length; i++)
            {
                tasks[i] = Workers[i].ResumeAsync();
            }

            Task.WaitAll(tasks);
        }

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

                Pause();
                foreach (var worker in Workers)
                    worker.Dispose();
            }
        }
    }
}
