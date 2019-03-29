namespace Unosquare.FFME.Engine
{
    using Common;
    using Diagnostics;
    using System;
    using System.Runtime.CompilerServices;

    internal partial class MediaEngine
    {
        /// <summary>
        /// Raises the MessageLogged event.
        /// </summary>
        /// <param name="message">The <see cref="LoggingMessage" /> instance containing the message.</param>
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        internal void SendOnMessageLogged(LoggingMessage message) =>
            Connector?.OnMessageLogged(this, message);

        /// <summary>
        /// Raises the media failed event.
        /// </summary>
        /// <param name="ex">The ex.</param>
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        internal void SendOnMediaFailed(Exception ex)
        {
            this.LogError(Aspects.Connector, "Media Failure", ex);
            Connector?.OnMediaFailed(this, ex);
        }

        /// <summary>
        /// Raises the media closed event.
        /// </summary>
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        internal void SendOnMediaClosed() =>
            Connector?.OnMediaClosed(this);

        /// <summary>
        /// Raises the media opened event.
        /// </summary>
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        internal void SendOnMediaOpened() =>
            Connector?.OnMediaOpened(this, Container?.MediaInfo);

        /// <summary>
        /// Raises the media initializing event.
        /// </summary>
        /// <param name="config">The container configuration options.</param>
        /// <param name="url">The URL.</param>
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        internal void SendOnMediaInitializing(ContainerConfiguration config, string url) =>
            Connector?.OnMediaInitializing(this, config, url);

        /// <summary>
        /// Raises the media opening event.
        /// </summary>
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        internal void SendOnMediaOpening() =>
            Connector?.OnMediaOpening(this, MediaOptions, Container.MediaInfo);

        /// <summary>
        /// Raises the media changing event.
        /// </summary>
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        internal void SendOnMediaChanging() =>
            Connector?.OnMediaChanging(this, MediaOptions, Container.MediaInfo);

        /// <summary>
        /// Raises the media changed event.
        /// </summary>
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        internal void SendOnMediaChanged() =>
            Connector?.OnMediaChanged(this, MediaInfo);

        /// <summary>
        /// Raises the buffering started event.
        /// </summary>
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        internal void SendOnBufferingStarted() =>
            Connector?.OnBufferingStarted(this);

        /// <summary>
        /// Raises the buffering ended event.
        /// </summary>
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        internal void SendOnBufferingEnded() =>
            Connector?.OnBufferingEnded(this);

        /// <summary>
        /// Raises the Seeking started event.
        /// </summary>
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        internal void SendOnSeekingStarted() =>
            Connector?.OnSeekingStarted(this);

        /// <summary>
        /// Raises the Seeking ended event.
        /// </summary>
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        internal void SendOnSeekingEnded() =>
            Connector?.OnSeekingEnded(this);

        /// <summary>
        /// Raises the media ended event.
        /// </summary>
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        internal void SendOnMediaEnded() =>
            Connector?.OnMediaEnded(this);

        /// <summary>
        /// Sends the on position changed.
        /// </summary>
        /// <param name="oldValue">The old value.</param>
        /// <param name="newValue">The new value.</param>
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        internal void SendOnPositionChanged(TimeSpan oldValue, TimeSpan newValue) =>
            Connector?.OnPositionChanged(this, oldValue, newValue);

        /// <summary>
        /// Sends the on media state changed.
        /// </summary>
        /// <param name="oldValue">The old value.</param>
        /// <param name="newValue">The new value.</param>
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        internal void SendOnMediaStateChanged(MediaPlaybackState oldValue, MediaPlaybackState newValue) =>
            Connector?.OnMediaStateChanged(this, oldValue, newValue);
    }
}