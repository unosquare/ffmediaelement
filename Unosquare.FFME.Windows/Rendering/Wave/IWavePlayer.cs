namespace Unosquare.FFME.Rendering.Wave
{
    using System;

    /// <summary>
    /// Represents the interface to a device that can play a Wave data
    /// </summary>
    internal interface IWavePlayer : IDisposable
    {
        /// <summary>
        /// Current playback state
        /// </summary>
        PlaybackState PlaybackState { get; }

        /// <summary>
        /// Gets a value indicating whether the audio playback is running.
        /// </summary>
        bool IsRunning { get; }

        /// <summary>
        /// Gets or sets the desired latency in milliseconds
        /// Should be set before a call to Init
        /// </summary>
        int DesiredLatency { get; }

        /// <summary>
        /// Gets the renderer that owns this wave player.
        /// </summary>
        AudioRenderer Renderer { get; }

        /// <summary>
        /// Begin playback
        /// </summary>
        void Start();

        /// <summary>
        /// Clears the internal audio data with silence data.
        /// </summary>
        void Clear();
    }
}
