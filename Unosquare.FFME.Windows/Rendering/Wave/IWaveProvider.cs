﻿namespace Unosquare.FFME.Rendering.Wave
{
    /// <summary>
    /// Generic interface for all WaveProviders.
    /// </summary>
    internal interface IWaveProvider
    {
        /// <summary>
        /// Gets the WaveFormat of this WaveProvider.
        /// </summary>
        WaveFormat WaveFormat { get; }

        /// <summary>
        /// Fill the specified buffer with wave data.
        /// </summary>
        /// <param name="buffer">The buffer to fill of wave data.</param>
        /// <param name="offset">Offset into buffer.</param>
        /// <param name="count">The number of bytes to read.</param>
        /// <returns>
        /// the number of bytes written to the buffer.
        /// </returns>
        int Read(byte[] buffer, int offset, int count);
    }
}
