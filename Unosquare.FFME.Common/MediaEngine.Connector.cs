namespace Unosquare.FFME
{
    using Shared;
    using System;
    using System.Runtime.CompilerServices;
    using System.Threading.Tasks;

    public partial class MediaEngine
    {
        /// <summary>
        /// Raises the MessageLogged event
        /// </summary>
        /// <param name="message">The <see cref="MediaLogMessage" /> instance containing the message.</param>
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        internal void SendOnMessageLogged(MediaLogMessage message)
        {
            Connector?.OnMessageLogged(this, message);
        }

        /// <summary>
        /// Raises the media failed event.
        /// </summary>
        /// <param name="ex">The ex.</param>
        /// <returns>A <see cref="Task"/> representing the asynchronous operation.</returns>
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        internal Task SendOnMediaFailed(Exception ex)
        {
            Log(MediaLogMessageType.Error, $"Media Failure - {ex?.GetType()}: {ex?.Message}");
            return Connector != null ? Connector.OnMediaFailed(this, ex) : Task.CompletedTask;
        }

        /// <summary>
        /// Raises the media closed event.
        /// </summary>
        /// <returns>A <see cref="Task"/> representing the asynchronous operation.</returns>
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        internal Task SendOnMediaClosed()
        {
            return Connector != null ? Connector.OnMediaClosed(this) : Task.CompletedTask;
        }

        /// <summary>
        /// Raises the media opened event.
        /// </summary>
        /// <returns>A <see cref="Task"/> representing the asynchronous operation.</returns>
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        internal Task SendOnMediaOpened()
        {
            return Connector != null ? Connector.OnMediaOpened(this) : Task.CompletedTask;
        }

        /// <summary>
        /// Raises the media initializing event.
        /// </summary>
        /// <param name="options">The options.</param>
        /// <param name="url">The URL.</param>
        /// <returns>A <see cref="Task"/> representing the asynchronous operation.</returns>
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        internal Task SendOnMediaInitializing(StreamOptions options, string url)
        {
            return Connector != null ? Connector.OnMediaInitializing(this, options, url) : Task.CompletedTask;
        }

        /// <summary>
        /// Raises the media opening event.
        /// </summary>
        /// <returns>A <see cref="Task"/> representing the asynchronous operation.</returns>
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        internal Task SendOnMediaOpening()
        {
            return Connector != null ? Connector.OnMediaOpening(this, Container.MediaOptions, Container.MediaInfo) : Task.CompletedTask;
        }

        /// <summary>
        /// Raises the buffering started event.
        /// </summary>
        /// <returns>A <see cref="Task"/> representing the asynchronous operation.</returns>
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        internal Task SendOnBufferingStarted()
        {
            return Connector != null ? Connector.OnBufferingStarted(this) : Task.CompletedTask;
        }

        /// <summary>
        /// Raises the buffering ended event.
        /// </summary>
        /// <returns>A <see cref="Task"/> representing the asynchronous operation.</returns>
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        internal Task SendOnBufferingEnded()
        {
            return Connector != null ? Connector.OnBufferingEnded(this) : Task.CompletedTask;
        }

        /// <summary>
        /// Raises the Seeking started event.
        /// </summary>
        /// <returns>A <see cref="Task"/> representing the asynchronous operation.</returns>
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        internal Task SendOnSeekingStarted()
        {
            return Connector != null ? Connector.OnSeekingStarted(this) : Task.CompletedTask;
        }

        /// <summary>
        /// Raises the Seeking ended event.
        /// </summary>
        /// <returns>A <see cref="Task"/> representing the asynchronous operation.</returns>
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        internal Task SendOnSeekingEnded()
        {
            return Connector != null ? Connector.OnSeekingEnded(this) : Task.CompletedTask;
        }

        /// <summary>
        /// Raises the media ended event.
        /// </summary>
        /// <returns>A <see cref="Task"/> representing the asynchronous operation.</returns>
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        internal Task SendOnMediaEnded()
        {
            return Connector != null ? Connector.OnMediaEnded(this) : Task.CompletedTask;
        }

        /// <summary>
        /// Sends the on position changed.
        /// </summary>
        /// <param name="oldValue">The old value.</param>
        /// <param name="newValue">The new value.</param>
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        internal void SendOnPositionChanged(TimeSpan oldValue, TimeSpan newValue)
        {
            Connector?.OnPositionChanged(this, oldValue, newValue);
        }

        /// <summary>
        /// Sends the on media state changed.
        /// </summary>
        /// <param name="oldValue">The old value.</param>
        /// <param name="newValue">The new value.</param>
        /// <returns>A <see cref="Task"/> representing the asynchronous operation.</returns>
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        internal Task SendOnMediaStateChanged(PlaybackStatus oldValue, PlaybackStatus newValue)
        {
            return Connector != null ? Connector.OnMediaStateChanged(this, oldValue, newValue) : Task.CompletedTask;
        }
    }
}