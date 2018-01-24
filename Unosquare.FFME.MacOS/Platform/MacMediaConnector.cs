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

        public void OnBufferingEnded(MediaEngine sender)
        {
            // placeholder
        }

        public void OnBufferingStarted(MediaEngine sender)
        {
            // placeholder
        }

        public void OnMediaClosed(MediaEngine sender)
        {
            // placeholder
        }

        public void OnMediaEnded(MediaEngine sender)
        {
            // placeholder
        }

        public void OnMediaFailed(MediaEngine sender, Exception e)
        {
            // placeholder
        }

        public void OnMediaOpened(MediaEngine sender)
        {
            // placeholder
        }

        public void OnMediaOpening(MediaEngine sender, MediaOptions mediaOptions, MediaInfo mediaInfo)
        {
            // placeholder
        }

        public void OnMessageLogged(MediaEngine sender, MediaLogMessage e)
        {
            if (e.MessageType == MediaLogMessageType.Trace) return;
            Console.WriteLine($"{e.MessageType,10} - {e.Message}");
        }

        public void OnPositionChanged(MediaEngine sender, TimeSpan position)
        {
            // placeholder
        }

        public void OnPropertiesChanged(MediaEngine sender, string[] propertyNames)
        {
            // placeholder
        }

        public void OnSeekingEnded(MediaEngine sender)
        {
            // placeholder
        }

        public void OnSeekingStarted(MediaEngine sender)
        {
            // placeholder
        }
    }
}
