namespace Unosquare.FFME.MacOS.Platform
{
    using Shared;
    using System;
    using System.Diagnostics;

    internal class MacMediaConnector : IMediaConnector
    {
        private readonly MediaElement Control;

        public MacMediaConnector(MediaElement control)
        {
            Control = control;
        }

        public void OnMessageLogged(MediaEngine sender, MediaLogMessage e)
        {
            if (e.MessageType == MediaLogMessageType.Trace) return;
            Debug.WriteLine($"{e.MessageType,10} - {e.Message}");
        }

        public void OnBufferingEnded(MediaEngine sender) { /* Placeholder */ }

        public void OnBufferingStarted(MediaEngine sender) { /* Placeholder */ }

        public void OnMediaClosed(MediaEngine sender) { /* Placeholder */ }

        public void OnMediaEnded(MediaEngine sender) { /* Placeholder */ }

        public void OnMediaFailed(MediaEngine sender, Exception e) { /* Placeholder */ }

        public void OnMediaOpened(MediaEngine sender, MediaInfo info) { /* Placeholder */ }

        public void OnMediaOpening(MediaEngine sender, MediaOptions options, MediaInfo mediaInfo) { /* Placeholder */ }

        public void OnMediaInitializing(MediaEngine sender, ContainerConfiguration config, string url) { /* Placeholder */ }

        public void OnSeekingEnded(MediaEngine sender) { /* Placeholder */ }

        public void OnSeekingStarted(MediaEngine sender) { /* Placeholder */ }

        public void OnPositionChanged(MediaEngine sender, TimeSpan oldValue, TimeSpan newValue) { /* Placeholder */ }

        public void OnMediaStateChanged(MediaEngine sender, PlaybackStatus oldValue, PlaybackStatus newValue) { /* Placeholder */ }

        public void OnMediaChanging(MediaEngine sender, MediaOptions mediaOptions, MediaInfo mediaInfo) { /* Placeholder */ }

        public void OnMediaChanged(MediaEngine sender, MediaInfo info) { /* Placeholder */ }
    }
}
