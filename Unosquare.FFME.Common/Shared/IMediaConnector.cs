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
        void OnMediaOpening(MediaEngine sender, MediaOptions mediaOptions, MediaInfo mediaInfo);

        /// <summary>
        /// Called when [media opened].
        /// </summary>
        /// <param name="sender">The sender.</param>
        void OnMediaOpened(MediaEngine sender);

        /// <summary>
        /// Called when [media closed].
        /// </summary>
        /// <param name="sender">The sender.</param>
        void OnMediaClosed(MediaEngine sender);

        /// <summary>
        /// Called when [media failed].
        /// </summary>
        /// <param name="sender">The sender.</param>
        /// <param name="e">The e.</param>
        void OnMediaFailed(MediaEngine sender, Exception e);

        /// <summary>
        /// Called when [media ended].
        /// </summary>
        /// <param name="sender">The sender.</param>
        void OnMediaEnded(MediaEngine sender);

        /// <summary>
        /// Called when [buffering started].
        /// </summary>
        /// <param name="sender">The sender.</param>
        void OnBufferingStarted(MediaEngine sender);

        /// <summary>
        /// Called when [buffering ended].
        /// </summary>
        /// <param name="sender">The sender.</param>
        void OnBufferingEnded(MediaEngine sender);

        /// <summary>
        /// Called when [seeking started].
        /// </summary>
        /// <param name="sender">The sender.</param>
        void OnSeekingStarted(MediaEngine sender);

        /// <summary>
        /// Called when [seeking ended].
        /// </summary>
        /// <param name="sender">The sender.</param>
        void OnSeekingEnded(MediaEngine sender);

        /// <summary>
        /// Called when [message logged].
        /// </summary>
        /// <param name="sender">The sender.</param>
        /// <param name="e">The <see cref="MediaLogMessage"/> instance containing the event data.</param>
        void OnMessageLogged(MediaEngine sender, MediaLogMessage e);

        /// <summary>
        /// Called when [position changed].
        /// </summary>
        /// <param name="sender">The sender.</param>
        /// <param name="position">The position.</param>
        void OnPositionChanged(MediaEngine sender, TimeSpan position);

        /// <summary>
        /// Called when an underlying media engine property is changed.
        /// This is used to handle property change notifications
        /// </summary>
        /// <param name="sender">The sender.</param>
        /// <param name="propertyNames">Name of the properties with changes.</param>
        void OnPropertiesChanged(MediaEngine sender, string[] propertyNames);
    }
}
