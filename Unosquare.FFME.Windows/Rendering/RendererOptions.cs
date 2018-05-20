namespace Unosquare.FFME.Rendering
{
    /// <summary>
    /// Provides access to various internal media renderer options
    /// </summary>
    public sealed class RendererOptions
    {
        /// <summary>
        /// By default, the audio renderer will skip or wait for samples to
        /// synchronize to video.
        /// </summary>
        public bool AudioDisableSync { get; set; } = false;
    }
}
