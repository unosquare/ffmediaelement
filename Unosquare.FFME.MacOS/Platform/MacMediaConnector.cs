namespace Unosquare.FFME.MacOS.Platform
{
    using System;
    using System.ComponentModel;
    using Unosquare.FFME.Shared;
    using System.Threading.Tasks;

    internal class MacMediaConnector : IMediaConnector
    {
        private readonly MediaElement Control = null;

        public MacMediaConnector(MediaElement control)
        {
            Control = control;
        }

        public Task OnBufferingEnded(MediaEngine sender)
        {
            return Task.CompletedTask;
        }

        public Task OnBufferingStarted(MediaEngine sender)
        {
            return Task.CompletedTask;
        }

        public Task OnMediaClosed(MediaEngine sender)
        {
            return Task.CompletedTask;
        }

        public Task OnMediaEnded(MediaEngine sender)
        {
            return Task.CompletedTask;
        }

        public Task OnMediaFailed(MediaEngine sender, Exception e)
        {
            return Task.CompletedTask;
        }

        public Task OnMediaOpened(MediaEngine sender, MediaInfo info)
        {
            return Task.CompletedTask;
        }

        public Task OnMediaOpening(MediaEngine sender, MediaOptions options, MediaInfo mediaInfo)
        {
            return Task.CompletedTask;
        }

        public Task OnMediaInitializing(MediaEngine sender, ContainerConfiguration config, string url)
        {
            return Task.CompletedTask;
        }

        public void OnMessageLogged(MediaEngine sender, MediaLogMessage e)
        {
            if (e.MessageType == MediaLogMessageType.Trace) return;
            Console.WriteLine($"{e.MessageType,10} - {e.Message}");
        }

        public Task OnSeekingEnded(MediaEngine sender)
        {
            return Task.CompletedTask;
        }

        public Task OnSeekingStarted(MediaEngine sender)
        {
            return Task.CompletedTask;
        }

        public void OnPositionChanged(MediaEngine sender, TimeSpan oldValue, TimeSpan newValue)
        {
            // placeholder
        }

        public Task OnMediaStateChanged(MediaEngine sender, PlaybackStatus oldValue, PlaybackStatus newValue)
        {
            return Task.CompletedTask;
        }

        public Task OnMediaChanging(MediaEngine sender, MediaOptions mediaOptions, MediaInfo mediaInfo)
        {
            return Task.CompletedTask;
        }

        public Task OnMediaChanged(MediaEngine sender, MediaInfo info)
        {
            return Task.CompletedTask;
        }
    }
}
