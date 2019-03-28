﻿namespace Unosquare.FFME.Primitives
{
    using System;
    using System.Threading;

    /// <summary>
    /// Acts as a <see cref="CancellationTokenSource"/> but with reusable tokens.
    /// </summary>
    internal sealed class CancellationTokenOwner : IDisposable
    {
        private readonly object SyncLock = new object();
        private bool m_IsDisposed;
        private CancellationTokenSource TokenSource = new CancellationTokenSource();

        /// <summary>
        /// Gets the token of the current.
        /// </summary>
        public CancellationToken Token
        {
            get
            {
                lock (SyncLock)
                {
                    return m_IsDisposed
                        ? CancellationToken.None
                        : TokenSource.Token;
                }
            }
        }

        /// <summary>
        /// Cancels the last referenced token and creates a new token source.
        /// </summary>
        public void Cancel()
        {
            lock (SyncLock)
            {
                if (m_IsDisposed) return;
                TokenSource.Cancel();
                TokenSource.Dispose();
                TokenSource = new CancellationTokenSource();
            }
        }

        /// <inheritdoc />
        public void Dispose() => Dispose(true);

        /// <summary>
        /// Releases unmanaged and - optionally - managed resources.
        /// </summary>
        /// <param name="alsoManaged"><c>true</c> to release both managed and unmanaged resources; <c>false</c> to release only unmanaged resources.</param>
        private void Dispose(bool alsoManaged)
        {
            lock (SyncLock)
            {
                if (!m_IsDisposed)
                {
                    if (alsoManaged)
                    {
                        TokenSource.Cancel();
                        TokenSource.Dispose();
                    }

                    m_IsDisposed = true;
                }
            }
        }
    }
}
