﻿namespace Unosquare.FFME.Events
{
    using Engine;
#if WINDOWS_UWP
    using Windows.UI.Xaml;
#else
    using System.Windows;
#endif

    /// <summary>
    /// Represents the event arguments of the <see cref="MediaElement.MediaOpening"/>
    /// or <see cref="MediaElement.MediaChanging"/> routed events.
    /// </summary>
    /// <seealso cref="RoutedEventArgs" />
    public class MediaOpeningEventArgs : RoutedEventArgs
    {
        /// <summary>
        /// Initializes a new instance of the <see cref="MediaOpeningEventArgs" /> class.
        /// </summary>
        /// <param name="options">The options.</param>
        /// <param name="info">The input information.</param>
        public MediaOpeningEventArgs(MediaOptions options, MediaInfo info)
        {
            Options = options;
            Info = info;
        }

        /// <summary>
        /// Set or change the options before the media is opened.
        /// </summary>
        public MediaOptions Options { get; }

        /// <summary>
        /// Provides internal details of the media, including its component streams.
        /// Typically, options are set based on what this information contains.
        /// </summary>
        public MediaInfo Info { get; }
    }
}
