namespace Unosquare.FFME.Windows.Sample
{
    using Events;
    using FFmpeg.AutoGen;
    using Shared;
    using System;
    using System.Diagnostics;
    using System.Linq;
    using System.Windows;
    using System.Windows.Controls;

    public partial class MainWindow
    {
        #region Logging Event Handlers

        /// <summary>
        /// Handles the MessageLogged event of the Media control.
        /// </summary>
        /// <param name="sender">The source of the event.</param>
        /// <param name="e">The <see cref="MediaLogMessageEventArgs" /> instance containing the event data.</param>
        private void OnMediaMessageLogged(object sender, MediaLogMessageEventArgs e)
        {
            if (e.MessageType == MediaLogMessageType.Trace)
                return;

            Debug.WriteLine($"{e.MessageType,10} - {e.Message}");
        }

        /// <summary>
        /// Handles the FFmpegMessageLogged event of the MediaElement control.
        /// </summary>
        /// <param name="sender">The source of the event.</param>
        /// <param name="e">The <see cref="MediaLogMessageEventArgs"/> instance containing the event data.</param>
        private void OnMediaFFmpegMessageLogged(object sender, MediaLogMessageEventArgs e)
        {
            if (e.MessageType != MediaLogMessageType.Warning && e.MessageType != MediaLogMessageType.Error)
                return;

            Debug.WriteLine($"{e.MessageType,10} - {e.Message}");
        }

        /// <summary>
        /// Handles the MediaFailed event of the Media control.
        /// </summary>
        /// <param name="sender">The source of the event.</param>
        /// <param name="e">The <see cref="ExceptionRoutedEventArgs"/> instance containing the event data.</param>
        private void OnMediaFailed(object sender, ExceptionRoutedEventArgs e)
        {
            MessageBox.Show(
                $"Media Failed: {e.ErrorException.GetType()}\r\n{e.ErrorException.Message}",
                $"{nameof(MediaElement)} Error",
                MessageBoxButton.OK,
                MessageBoxImage.Error,
                MessageBoxResult.OK);
        }

        #endregion

        #region Media Stream Events

        /// <summary>
        /// Handles the MediaInitializing event of the Media control.
        /// </summary>
        /// <param name="sender">The source of the event.</param>
        /// <param name="e">The <see cref="MediaInitializingRoutedEventArgs"/> instance containing the event data.</param>
        private void OnMediaInitializing(object sender, MediaInitializingRoutedEventArgs e)
        {
            // An example of injecting input options for http/https streams
            if (e.Url.StartsWith("http://") || e.Url.StartsWith("https://"))
            {
                e.Options.Input["user_agent"] = $"{typeof(StreamOptions).Namespace}/{typeof(StreamOptions).Assembly.GetName().Version}";
                e.Options.Input["headers"] = $"Referer:https://www.unosquare.com";
                e.Options.Input["multiple_requests"] = "1";
                e.Options.Input["reconnect"] = "1";
                e.Options.Input["reconnect_at_eof"] = "1";
                e.Options.Input["reconnect_streamed"] = "1";
                e.Options.Input["reconnect_delay_max"] = "10"; // in seconds
            }

            // In realtime streams these settings can be used to reduce latency (see example from issue #152)
            // e.Options.Format.FlagNoBuffer = true;
            // e.Options.Format.ProbeSize = 8192;
            // e.Options.Format.MaxAnalyzeDuration = System.TimeSpan.FromSeconds(1);
        }

        /// <summary>
        /// Handles the MediaOpening event of the Media control.
        /// </summary>
        /// <param name="sender">The source of the event.</param>
        /// <param name="e">The <see cref="MediaOpeningRoutedEventArgs"/> instance containing the event data.</param>
        private void OnMediaOpening(object sender, MediaOpeningRoutedEventArgs e)
        {
            // Example of automatically side-loading SRT subs
            try
            {
                var inputUrl = e.Info.InputUrl;
                var url = new Uri(inputUrl);
                if (url.IsFile || url.IsUnc)
                {
                    inputUrl = System.IO.Path.ChangeExtension(url.LocalPath, "srt");
                    if (System.IO.File.Exists(inputUrl))
                        e.Options.SubtitlesUrl = inputUrl;
                }
            }
            catch { }

            // An example of switching to a different stream
            var subtitleStreams = e.Info.Streams.Where(kvp => kvp.Value.CodecType == AVMediaType.AVMEDIA_TYPE_SUBTITLE).Select(kvp => kvp.Value);
            var englishSubtitleStream = subtitleStreams.FirstOrDefault(s => s.Language.StartsWith("en"));
            if (englishSubtitleStream != null)
                e.Options.SubtitleStream = englishSubtitleStream;

            // The yadif filter deinterlaces the video; we check the field order if we need
            // to deinterlace the video automatically
            if (e.Options.VideoStream != null
                && e.Options.VideoStream.FieldOrder != AVFieldOrder.AV_FIELD_PROGRESSIVE
                && e.Options.VideoStream.FieldOrder != AVFieldOrder.AV_FIELD_UNKNOWN)
            {
                e.Options.VideoFilter = "yadif";

                // When enabling HW acceleration, the filtering does not seem to get applied for some reason.
                e.Options.EnableHardwareAcceleration = false;
            }

            // Experimetal HW acceleration support. Remove if not needed.
            e.Options.EnableHardwareAcceleration = false;

            // e.Options.AudioFilter = "aecho=0.8:0.9:1000:0.3";
            // e.Options.AudioFilter = "chorus=0.5:0.9:50|60|40:0.4|0.32|0.3:0.25|0.4|0.3:2|2.3|1.3";
        }

        /// <summary>
        /// Handles the MediaOpened event of the Media control.
        /// </summary>
        /// <param name="sender">The source of the event.</param>
        /// <param name="e">The <see cref="RoutedEventArgs"/> instance containing the event data.</param>
        private void OnMediaOpened(object sender, RoutedEventArgs e)
        {
            // Set a start position (see issue #66)
            // Media.Position = TimeSpan.FromSeconds(5);
            // var playTask = Media.Play(); // fire up the play task asynchronously
        }

        #endregion

        #region Methods: Event Handlers

        /// <summary>
        /// Handles the PositionChanged event of the Media control.
        /// </summary>
        /// <param name="sender">The source of the event.</param>
        /// <param name="e">The <see cref="PositionChangedRoutedEventArgs"/> instance containing the event data.</param>
        private void OnMediaPositionChanged(object sender, PositionChangedRoutedEventArgs e)
        {
            // Debug.WriteLine($"{nameof(Media.Position)} = {e.Position}");
        }

        #endregion
    }
}
