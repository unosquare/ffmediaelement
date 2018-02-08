namespace Unosquare.FFME.Windows.Sample
{
    using Events;
    using FFmpeg.AutoGen;
    using Shared;
    using System;
    using System.ComponentModel;
    using System.Diagnostics;
    using System.Linq;
    using System.Windows;
    using System.Windows.Controls;
    using System.Windows.Media;
    using Unosquare.FFME.Windows.Sample.Kernel;

    public partial class MainWindow
    {
        #region Methods: Event Handlers

        /// <summary>
        /// Handles the RenderingVideo event of the Media control.
        /// </summary>
        /// <param name="sender">The source of the event.</param>
        /// <param name="e">The <see cref="RenderingVideoEventArgs"/> instance containing the event data.</param>
        private void Media_RenderingVideo(object sender, RenderingVideoEventArgs e)
        {
            if (HasTakenThumbnail) return;

            if (Media.HasMediaEnded || Media.Position.TotalSeconds >= 3 || (Media.NaturalDuration.HasTimeSpan && Media.NaturalDuration.TimeSpan.TotalSeconds <= 3))
            {
                HasTakenThumbnail = true;
                PlaylistManager.AddOrUpdateEntryThumbnail(Media.Source.ToString(), e.Bitmap);
                PlaylistManager.SaveEntries();
            }
        }

        private void Media_PositionChanged(object sender, PositionChangedRoutedEventArgs e)
        {
            // Debug.WriteLine($"{nameof(Media.Position)} = {e.Position}");
        }

        /// <summary>
        /// Handles the Loaded event of the MainWindow control.
        /// </summary>
        /// <param name="sender">The source of the event.</param>
        /// <param name="e">The <see cref="RoutedEventArgs"/> instance containing the event data.</param>
        private void MainWindow_Loaded(object sender, RoutedEventArgs e)
        {
            var presenter = VisualTreeHelper.GetParent(Content as UIElement) as ContentPresenter;
            presenter.MinWidth = MinWidth;
            presenter.MinHeight = MinHeight;

            SizeToContent = SizeToContent.WidthAndHeight;
            MinWidth = ActualWidth;
            MinHeight = ActualHeight;
            SizeToContent = SizeToContent.Manual;

            foreach (var kvp in PropertyUpdaters)
            {
                kvp.Value.Invoke();
                PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(kvp.Key));
            }

            Loaded -= MainWindow_Loaded;

            PlaylistManager.LoadEntries();
        }

        /// <summary>
        /// Handles the PropertyChanged event of the Media control.
        /// </summary>
        /// <param name="sender">The source of the event.</param>
        /// <param name="e">The <see cref="PropertyChangedEventArgs"/> instance containing the event data.</param>
        private void Media_PropertyChanged(object sender, PropertyChangedEventArgs e)
        {
            if (PropertyTriggers.ContainsKey(e.PropertyName) == false) return;
            foreach (var propertyName in PropertyTriggers[e.PropertyName])
            {
                if (PropertyUpdaters.ContainsKey(propertyName) == false)
                    continue;

                PropertyUpdaters[propertyName]?.Invoke();
                PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(propertyName));
            }
        }

        /// <summary>
        /// Handles the MessageLogged event of the Media control.
        /// </summary>
        /// <param name="sender">The source of the event.</param>
        /// <param name="e">The <see cref="MediaLogMessageEventArgs" /> instance containing the event data.</param>
        private void Media_MessageLogged(object sender, MediaLogMessageEventArgs e)
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
        private void MediaElement_FFmpegMessageLogged(object sender, MediaLogMessageEventArgs e)
        {
            if (e.Message.Contains("] Reinit context to ")
                || e.Message.Contains("Using non-standard frame rate"))
            {
                return;
            }

            Debug.WriteLine($"{e.MessageType,10} - {e.Message}");
        }

        /// <summary>
        /// Handles the MediaFailed event of the Media control.
        /// </summary>
        /// <param name="sender">The source of the event.</param>
        /// <param name="e">The <see cref="ExceptionRoutedEventArgs"/> instance containing the event data.</param>
        private void Media_MediaFailed(object sender, ExceptionRoutedEventArgs e)
        {
            MessageBox.Show(
                $"Media Failed: {e.ErrorException.GetType()}\r\n{e.ErrorException.Message}",
                "MediaElement Error",
                MessageBoxButton.OK,
                MessageBoxImage.Error,
                MessageBoxResult.OK);
        }

        /// <summary>
        /// Handles the MediaOpened event of the Media control.
        /// </summary>
        /// <param name="sender">The source of the event.</param>
        /// <param name="e">The <see cref="RoutedEventArgs"/> instance containing the event data.</param>
        private void Media_MediaOpened(object sender, RoutedEventArgs e)
        {
            // Set a start position (see issue #66)
            // Media.Position = TimeSpan.FromSeconds(5);
            // var playTask = Media.Play(); // fire up the play task asynchronously

            // Reset the Zoom
            MediaZoom = 1d;

            // Update the COntrols
            var source = Media.Source.ToString();
            AddToggleButton.IsChecked = false;
            OpenMenuButton.IsChecked = false;
            OpenFileTextBox.Text = source;
            OpenFileTextBox.SelectAll();
            HasTakenThumbnail = false;
        }

        /// <summary>
        /// Handles the MediaInitializing event of the Media control.
        /// </summary>
        /// <param name="sender">The source of the event.</param>
        /// <param name="e">The <see cref="MediaInitializingRoutedEventArgs"/> instance containing the event data.</param>
        private void Media_MediaInitializing(object sender, MediaInitializingRoutedEventArgs e)
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
        private void Media_MediaOpening(object sender, MediaOpeningRoutedEventArgs e)
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

            PlaylistManager.AddOrUpdateEntry(Media.Source?.ToString() ?? e.Info.InputUrl, e.Info);
            PlaylistManager.SaveEntries();

#if APPLY_AUDIO_FILTER
            // e.Options.AudioFilter = "aecho=0.8:0.9:1000:0.3";
            e.Options.AudioFilter = "chorus=0.5:0.9:50|60|40:0.4|0.32|0.3:0.25|0.4|0.3:2|2.3|1.3";
#endif
        }

        #endregion
    }
}
