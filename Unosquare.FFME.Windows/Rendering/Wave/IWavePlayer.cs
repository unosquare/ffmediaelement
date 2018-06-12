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
        /// Gets or sets the desired latency in milliseconds
        /// Should be set before a call to Init
        /// </summary>
        int DesiredLatency { get; set; }

        /// <summary>
        /// Gets the renderer that owns this wave player.
        /// </summary>
        AudioRenderer Renderer { get; }

        /// <summary>
        /// Begin playback
        /// </summary>
        void Play();

        /// <summary>
        /// Stop playback
        /// </summary>
        void Stop();

        /// <summary>
        /// Pause Playback
        /// </summary>
        void Pause();

        /// <summary>
        /// Initialise playback
        /// </summary>
        /// <param name="waveProvider">The waveprovider to be played</param>
        void Init(IWaveProvider waveProvider);
    }
}
