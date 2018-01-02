namespace Unosquare.FFME
{
    using System;
    using System.ComponentModel;

    /// <summary>
    /// Connects handlers between the MediaEngine and a platfrom-secific implementation
    /// </summary>
    internal interface IEventConnector
    {
        void OnMediaOpening(object sender, MediaOpeningEventArgs e);
        void OnMediaOpened(object sender, EventArgs e);
        void OnMediaClosed(object sender, EventArgs e);
        void OnMediaFailed(object sender, ExceptionEventArgs e);
        void OnMediaEnded(object sender, EventArgs e);
        void OnBufferingStarted(object sender, EventArgs e);
        void OnBufferingEnded(object sender, EventArgs e);
        void OnSeekingStarted(object sender, EventArgs e);
        void OnSeekingEnded(object sender, EventArgs e);
        void OnMessageLogged(object sender, MediaLogMessagEventArgs e);
        void OnPositionChanged(object sender, PositionChangedEventArgs e);
        void OnPropertyChanged(object sender, PropertyChangedEventArgs e);
    }
}
