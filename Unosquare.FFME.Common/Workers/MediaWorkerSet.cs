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

            Reading = new PacketReadingWorker(mediaCore);
            Decoding = new FrameDecodingWorker(mediaCore);
            Rendering = new BlockRenderingWorker(mediaCore);

            Workers[(int)MediaWorkerType.Read] = Reading;
            Workers[(int)MediaWorkerType.Decode] = Decoding;
            Workers[(int)MediaWorkerType.Render] = Rendering;
        }

        public MediaEngine MediaCore { get; }

        public PacketReadingWorker Reading { get; }

        public FrameDecodingWorker Decoding { get; }

        public BlockRenderingWorker Rendering { get; }

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

        public void Pause(bool wait, bool read, bool decode, bool render)
        {
            if (IsDisposed) return;

            var tasks = new Task[(read ? 1 : 0) + (decode ? 1 : 0) + (render ? 1 : 0)];
            var index = 0;
            if (read)
            {
                tasks[index] = this[MediaWorkerType.Read].PauseAsync();
                index++;
            }

            if (decode)
            {
                tasks[index] = this[MediaWorkerType.Decode].PauseAsync();
                index++;
            }

            if (render)
            {
                tasks[index] = this[MediaWorkerType.Render].PauseAsync();
                index++;
            }

            if (wait)
                Task.WaitAll(tasks);
        }

        public void Pause(bool wait) => Pause(wait, true, true, true);

        public void Resume(bool wait)
        {
            if (IsDisposed) return;

            var tasks = new Task[Workers.Length];
            for (var i = 0; i < Workers.Length; i++)
            {
                tasks[i] = Workers[i].ResumeAsync();
            }

            if (wait)
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

                Pause(true);
                foreach (var worker in Workers)
                    worker.Dispose();
            }
        }
    }
}
