namespace Unosquare.FFME.Shared
{
    using System;

    /// <summary>
    /// Connects handlers between the Media Engine and a platfrom-secific implementation
    /// </summary>
    public interface IMediaConnector
    {
        /// <summary>
        /// Called when [media opening].
        /// </summary>
        /// <param name="sender">The sender.</param>
        /// <param name="mediaOptions">The media options.</param>
        /// <param name="mediaInfo">The media information.</param>
        void OnMediaOpening(object sender, MediaOptions mediaOptions, MediaInfo mediaInfo);

        /// <summary>
        /// Called when [media opened].
        /// </summary>
        /// <param name="sender">The sender.</param>
        void OnMediaOpened(object sender);

        /// <summary>
        /// Called when [media closed].
        /// </summary>
        /// <param name="sender">The sender.</param>
        void OnMediaClosed(object sender);

        /// <summary>
        /// Called when [media failed].
        /// </summary>
        /// <param name="sender">The sender.</param>
        /// <param name="e">The e.</param>
        void OnMediaFailed(object sender, Exception e);

        /// <summary>
        /// Called when [media ended].
        /// </summary>
        /// <param name="sender">The sender.</param>
        void OnMediaEnded(object sender);

        /// <summary>
        /// Called when [buffering started].
        /// </summary>
        /// <param name="sender">The sender.</param>
        void OnBufferingStarted(object sender);

        /// <summary>
        /// Called when [buffering ended].
        /// </summary>
        /// <param name="sender">The sender.</param>
        void OnBufferingEnded(object sender);

        /// <summary>
        /// Called when [seeking started].
        /// </summary>
        /// <param name="sender">The sender.</param>
        void OnSeekingStarted(object sender);

        /// <summary>
        /// Called when [seeking ended].
        /// </summary>
        /// <param name="sender">The sender.</param>
        void OnSeekingEnded(object sender);

        /// <summary>
        /// Called when [message logged].
        /// </summary>
        /// <param name="sender">The sender.</param>
        /// <param name="e">The <see cref="MediaLogMessage"/> instance containing the event data.</param>
        void OnMessageLogged(object sender, MediaLogMessage e);

        /// <summary>
        /// Called when [position changed].
        /// </summary>
        /// <param name="sender">The sender.</param>
        /// <param name="position">The position.</param>
        void OnPositionChanged(object sender, TimeSpan position);

        /// <summary>
        /// Called when an underlying media engine property is changed.
        /// This is used to handle property change notifications
        /// </summary>
        /// <param name="sender">The sender.</param>
        /// <param name="propertyName">Name of the property.</param>
        void OnPropertyChanged(object sender, string propertyName);
    }
}
