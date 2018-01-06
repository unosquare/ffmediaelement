namespace Unosquare.FFME.MacOS.Platform
{
    using System;
    using System.ComponentModel;
    using Unosquare.FFME.Shared;

    internal class MacMediaConnector : IMediaConnector
    {
        private MediaElement Control = null;

        public MacMediaConnector(MediaElement control)
        {
            Control = control;
        }

        public void OnBufferingEnded(object sender)
        {
            // placeholder
        }

        public void OnBufferingStarted(object sender)
        {
            // placeholder
        }

        public void OnMediaClosed(object sender)
        {
            // placeholder
        }

        public void OnMediaEnded(object sender)
        {
            // placeholder
        }

        public void OnMediaFailed(object sender, Exception e)
        {
            // placeholder
        }

        public void OnMediaOpened(object sender)
        {
            // placeholder
        }

        public void OnMediaOpening(object sender, MediaOptions mediaOptions, MediaInfo mediaInfo)
        {
            // placeholder
        }

        public void OnMessageLogged(object sender, MediaLogMessage e)
        {
            if (e.MessageType == MediaLogMessageType.Trace) return;
            Console.WriteLine($"{e.MessageType,10} - {e.Message}");
        }

        public void OnPositionChanged(object sender, TimeSpan position)
        {
            // placeholder
        }

        public void OnPropertyChanged(object sender, PropertyChangedEventArgs e)
        {
            // placeholder
        }

        public void OnSeekingEnded(object sender)
        {
            // placeholder
        }

        public void OnSeekingStarted(object sender)
        {
            // placeholder
        }
    }
}
