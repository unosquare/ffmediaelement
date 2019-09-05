namespace Unosquare.FFME.Platform
{
    using Common;
    using Diagnostics;
    using Engine;
    using FFmpeg.AutoGen;
    using System;

    /// <summary>
    /// The Media engine connector.
    /// </summary>
    /// <seealso cref="IMediaConnector" />
    internal sealed partial class MediaConnector : IMediaConnector
    {
        private readonly MediaElement Parent;

        /// <summary>
        /// Initializes a new instance of the <see cref="MediaConnector"/> class.
        /// </summary>
        /// <param name="parent">The control.</param>
        public MediaConnector(MediaElement parent)
        {
            Parent = parent;
        }

        /// <inheritdoc />
        public void OnBufferingEnded(MediaEngine sender) =>
            Parent?.PostBufferingEndedEvent();

        /// <inheritdoc />
        public void OnBufferingStarted(MediaEngine sender) =>
            Parent?.PostBufferingStartedEvent();

        /// <inheritdoc />
        public void OnMediaClosed(MediaEngine sender) =>
            Parent?.PostMediaClosedEvent();

        /// <inheritdoc />
        public void OnMediaEnded(MediaEngine sender)
        {
            if (Parent == null || sender == null) return;

            Library.GuiContext.EnqueueInvoke(async () =>
            {
                Parent.PostMediaEndedEvent();
                var behavior = Parent.LoopingBehavior;

                if (behavior == MediaPlaybackState.Close)
                {
                    await sender.Close();
                }
                else if (behavior == MediaPlaybackState.Play)
                {
                    await sender.Stop();
                    await sender.Play();
                }
                else if (behavior == MediaPlaybackState.Stop)
                {
                    await sender.Stop();
                }
            });
        }

        /// <inheritdoc />
        public void OnMediaFailed(MediaEngine sender, Exception e) =>
            Parent?.PostMediaFailedEvent(e);

        /// <inheritdoc />
        public void OnMediaOpened(MediaEngine sender, MediaInfo mediaInfo)
        {
            if (Parent == null || sender == null) return;

            Library.GuiContext.EnqueueInvoke(async () =>
            {
                // Set initial controller properties
                // Has to be on the GUI thread as we are reading dependency properties
                sender.State.Volume = Parent.Volume;
                sender.State.IsMuted = Parent.IsMuted;
                sender.State.Balance = Parent.Balance;

                // Notify the end user media has opened successfully
                Parent.PostMediaOpenedEvent(mediaInfo);

                try
                {
                    // Start playback if we don't support pausing
                    if (sender.State.CanPause == false)
                    {
                        await sender.Play();
                        return;
                    }

                    if (Parent.LoadedBehavior == MediaPlaybackState.Play)
                        await sender.Play();
                    else if (Parent.LoadedBehavior == MediaPlaybackState.Pause)
                        await sender.Pause();
                }
                finally
                {
                    Parent.PostMediaReadyEvent();
                }
            });
        }

        /// <inheritdoc />
        public void OnMediaOpening(MediaEngine sender, MediaOptions options, MediaInfo mediaInfo) =>
            Parent?.RaiseMediaOpeningEvent(options, mediaInfo);

        /// <inheritdoc />
        public void OnMediaChanging(MediaEngine sender, MediaOptions options, MediaInfo mediaInfo) =>
            Parent?.RaiseMediaChangingEvent(options, mediaInfo);

        /// <inheritdoc />
        public void OnMediaChanged(MediaEngine sender, MediaInfo mediaInfo) =>
            Parent?.PostMediaChangedEvent(mediaInfo);

        /// <inheritdoc />
        public void OnMediaInitializing(MediaEngine sender, ContainerConfiguration config, string url) =>
            Parent?.RaiseMediaInitializingEvent(config, url);

        /// <inheritdoc />
        public void OnMessageLogged(MediaEngine sender, LoggingMessage e) =>
            Parent?.RaiseMessageLoggedEvent(e);

        /// <inheritdoc />
        public void OnSeekingEnded(MediaEngine sender) =>
            Parent?.PostSeekingEndedEvent();

        /// <inheritdoc />
        public void OnSeekingStarted(MediaEngine sender) =>
            Parent?.PostSeekingStartedEvent();

        /// <inheritdoc />
        public void OnPositionChanged(MediaEngine sender, TimeSpan oldValue, TimeSpan newValue) =>
            Parent?.PostPositionChangedEvent(oldValue, newValue);

        /// <inheritdoc />
        public void OnMediaStateChanged(MediaEngine sender, MediaPlaybackState oldValue, MediaPlaybackState newValue) =>
            Parent?.PostMediaStateChangedEvent(oldValue, newValue);

        /// <inheritdoc />
        public unsafe void OnPacketRead(AVPacket* packet, AVFormatContext* context) =>
            Parent?.RaisePacketReadEvent(packet, context);

        /// <inheritdoc />
        public unsafe void OnVideoFrameDecoded(AVFrame* videoFrame, AVFormatContext* context) =>
            Parent?.RaiseVideoFrameDecodedEvent(videoFrame, context);

        /// <inheritdoc />
        public unsafe void OnAudioFrameDecoded(AVFrame* audioFrame, AVFormatContext* context) =>
            Parent?.RaiseAudioFrameDecodedEvent(audioFrame, context);

        /// <inheritdoc />
        public unsafe void OnSubtitleDecoded(AVSubtitle* subtitle, AVFormatContext* context) =>
            Parent?.RaiseSubtitleDecodedEvent(subtitle, context);

        /// <inheritdoc />
        public unsafe void OnDataFrameDecoded(AVFrame* dataFrame, AVFormatContext* context) =>
            Parent?.RaiseDataFrameDecodedEvent(dataFrame, context);
    }
}
