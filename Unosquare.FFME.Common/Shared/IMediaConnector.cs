﻿namespace Unosquare.FFME.Shared
{
    using System;
    using System.Threading.Tasks;

    /// <summary>
    /// Connects handlers between the Media Engine event signals and a platfrom-secific implementation
    /// </summary>
    public interface IMediaConnector
    {
        /// <summary>
        /// Called when [media initializing].
        /// </summary>
        /// <param name="sender">The sender.</param>
        /// <param name="config">The container configuration options.</param>
        /// <param name="mediaUrl">The media URL.</param>
        /// <returns>A <see cref="Task"/> representing the asynchronous operation.</returns>
        Task OnMediaInitializing(MediaEngine sender, ContainerConfiguration config, string mediaUrl);

        /// <summary>
        /// Called when [media opening].
        /// </summary>
        /// <param name="sender">The sender.</param>
        /// <param name="mediaOptions">The media options.</param>
        /// <param name="mediaInfo">The media information.</param>
        /// <returns>A <see cref="Task"/> representing the asynchronous operation.</returns>
        Task OnMediaOpening(MediaEngine sender, MediaOptions mediaOptions, MediaInfo mediaInfo);

        /// <summary>
        /// Called when [media opened].
        /// </summary>
        /// <param name="sender">The sender.</param>
        /// <returns>A <see cref="Task"/> representing the asynchronous operation.</returns>
        Task OnMediaOpened(MediaEngine sender);

        /// <summary>
        /// Called when [media closed].
        /// </summary>
        /// <param name="sender">The sender.</param>
        /// <returns>A <see cref="Task"/> representing the asynchronous operation.</returns>
        Task OnMediaClosed(MediaEngine sender);

        /// <summary>
        /// Called when [media failed].
        /// </summary>
        /// <param name="sender">The sender.</param>
        /// <param name="e">The e.</param>
        /// <returns>A <see cref="Task"/> representing the asynchronous operation.</returns>
        Task OnMediaFailed(MediaEngine sender, Exception e);

        /// <summary>
        /// Called when [media ended].
        /// </summary>
        /// <param name="sender">The sender.</param>
        /// <returns>A <see cref="Task"/> representing the asynchronous operation.</returns>
        Task OnMediaEnded(MediaEngine sender);

        /// <summary>
        /// Called when [buffering started].
        /// </summary>
        /// <param name="sender">The sender.</param>
        /// <returns>A <see cref="Task"/> representing the asynchronous operation.</returns>
        Task OnBufferingStarted(MediaEngine sender);

        /// <summary>
        /// Called when [buffering ended].
        /// </summary>
        /// <param name="sender">The sender.</param>
        /// <returns>A <see cref="Task"/> representing the asynchronous operation.</returns>
        Task OnBufferingEnded(MediaEngine sender);

        /// <summary>
        /// Called when [seeking started].
        /// </summary>
        /// <param name="sender">The sender.</param>
        /// <returns>A <see cref="Task"/> representing the asynchronous operation.</returns>
        Task OnSeekingStarted(MediaEngine sender);

        /// <summary>
        /// Called when [seeking ended].
        /// </summary>
        /// <param name="sender">The sender.</param>
        /// <returns>A <see cref="Task"/> representing the asynchronous operation.</returns>
        Task OnSeekingEnded(MediaEngine sender);

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
        /// <param name="oldValue">The old value.</param>
        /// <param name="newValue">The new value.</param>
        void OnPositionChanged(MediaEngine sender, TimeSpan oldValue, TimeSpan newValue);

        /// <summary>
        /// Called when [media state changed].
        /// </summary>
        /// <param name="sender">The sender.</param>
        /// <param name="oldValue">The old value.</param>
        /// <param name="newValue">The new value.</param>
        /// <returns>A <see cref="Task"/> representing the asynchronous operation.</returns>
        Task OnMediaStateChanged(MediaEngine sender, PlaybackStatus oldValue, PlaybackStatus newValue);
    }
}
