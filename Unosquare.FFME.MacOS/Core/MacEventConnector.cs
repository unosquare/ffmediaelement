using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.Text;

namespace Unosquare.FFME.MacOS.Core
{
    internal class MacEventConnector : IEventConnector
    {
        private MediaElement Control = null;

        public MacEventConnector(MediaElement control)
        {
            Control = control;
        }

        public void OnBufferingEnded(object sender, EventArgs e)
        {
            // placeholder
        }

        public void OnBufferingStarted(object sender, EventArgs e)
        {
            // placeholder
        }

        public void OnMediaClosed(object sender, EventArgs e)
        {
            // placeholder
        }

        public void OnMediaEnded(object sender, EventArgs e)
        {
            // placeholder
        }

        public void OnMediaFailed(object sender, ExceptionEventArgs e)
        {
            // placeholder
        }

        public void OnMediaOpened(object sender, EventArgs e)
        {
            // placeholder
        }

        public void OnMediaOpening(object sender, MediaOpeningEventArgs e)
        {
            // placeholder
        }

        public void OnMessageLogged(object sender, MediaLogMessagEventArgs e)
        {
            if (e.MessageType == MediaLogMessageType.Trace) return;
            Console.WriteLine($"{e.MessageType,10} - {e.Message}");
        }

        public void OnPositionChanged(object sender, PositionChangedEventArgs e)
        {
            // placeholder
        }

        public void OnPropertyChanged(object sender, PropertyChangedEventArgs e)
        {
            // placeholder
        }

        public void OnSeekingEnded(object sender, EventArgs e)
        {
            // placeholder
        }

        public void OnSeekingStarted(object sender, EventArgs e)
        {
            // placeholder
        }
    }
}
