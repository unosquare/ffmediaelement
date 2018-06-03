namespace Unosquare.FFME.Windows.Sample
{
    using ClosedCaptions;
    using Events;
    using FFmpeg.AutoGen;
    using Platform;
    using Shared;
    using System;
    using System.Diagnostics;
    using System.Linq;
    using System.Text;
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

            if (string.IsNullOrWhiteSpace(e.Message) == false && e.Message.Contains("Using non-standard frame rate"))
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
                App.Current.MainWindow,
                $"Media Failed: {e.ErrorException.GetType()}\r\n{e.ErrorException.Message}",
                $"{nameof(MediaElement)} Error",
                MessageBoxButton.OK,
                MessageBoxImage.Error,
                MessageBoxResult.OK);
        }

        #endregion

        #region Media Stream Opening Event Handlers

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
                e.Configuration.PrivateOptions["user_agent"] = $"{typeof(ContainerConfiguration).Namespace}/{typeof(ContainerConfiguration).Assembly.GetName().Version}";
                e.Configuration.PrivateOptions["headers"] = $"Referer:https://www.unosquare.com";
                e.Configuration.PrivateOptions["multiple_requests"] = "1";
                e.Configuration.PrivateOptions["reconnect"] = "1";
                e.Configuration.PrivateOptions["reconnect_streamed"] = "1";
                e.Configuration.PrivateOptions["reconnect_delay_max"] = "10"; // in seconds

                // e.Configuration.PrivateOptions["reconnect_at_eof"] = "1"; // This prevents some HLS stresm from opening properly
            }

            // Example of forcing tcp transport on rtsp feeds
            // RTSP is similar to HTTP but it only provides metadata about the underlying stream
            // Most RTSP compatible streams expose RTP data over both UDP and TCP.
            // TCP provides reliable communication while UDP does not
            if (e.Url.StartsWith("rtsp://"))
            {
                e.Configuration.PrivateOptions["rtsp_transport"] = "tcp";
                e.Configuration.GlobalOptions.FlagNoBuffer = true;
            }

            // A few WMV files I have tested don't have continuous enough audio packets to support
            // perfect synchronization between audio and video
            Media.RendererOptions.AudioDisableSync = e.Url.EndsWith(".wmv");

            // In realtime streams these settings can be used to reduce latency (see example from issue #152)
            // e.Options.GlobalOptions.FlagNoBuffer = true;
            // e.Options.GlobalOptions.ProbeSize = 8192;
            // e.Options.GlobalOptions.MaxAnalyzeDuration = System.TimeSpan.FromSeconds(1);
        }

        /// <summary>
        /// Handles the MediaOpening event of the Media control.
        /// </summary>
        /// <param name="sender">The source of the event.</param>
        /// <param name="e">The <see cref="MediaOpeningRoutedEventArgs"/> instance containing the event data.</param>
        private void OnMediaOpening(object sender, MediaOpeningRoutedEventArgs e)
        {
            // You can start off by adjusting subtitles delay
            // e.Options.SubtitlesDelay = TimeSpan.FromSeconds(7); // See issue #216

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

            // You can force video FPS if necessary
            // see: https://github.com/unosquare/ffmediaelement/issues/212
            // e.Options.VideoForcedFps = 25;

            // An example of specifcally selecting a playback stream
            var subtitleStreams = e.Info.Streams.Where(kvp => kvp.Value.CodecType == AVMediaType.AVMEDIA_TYPE_SUBTITLE).Select(kvp => kvp.Value);
            var englishSubtitleStream = subtitleStreams.FirstOrDefault(s => s.Language.StartsWith("en"));
            if (englishSubtitleStream != null)
            {
                e.Options.SubtitleStream = englishSubtitleStream;
            }

            var videoStream = e.Options.VideoStream;
            if (videoStream != null)
            {
                // Check if the video requires deinterlacing
                var requiresDeinterlace = videoStream.FieldOrder != AVFieldOrder.AV_FIELD_PROGRESSIVE
                    && videoStream.FieldOrder != AVFieldOrder.AV_FIELD_UNKNOWN;

                // Hardwrae device priorities
                var deviceCandidates = new AVHWDeviceType[]
                {
                    AVHWDeviceType.AV_HWDEVICE_TYPE_CUDA,
                    AVHWDeviceType.AV_HWDEVICE_TYPE_D3D11VA,
                    AVHWDeviceType.AV_HWDEVICE_TYPE_DXVA2
                };

                // Hardware device selection
                if (videoStream.FPS <= 30)
                {
                    foreach (var deviceType in deviceCandidates)
                    {
                        var accelerator = videoStream.HardwareDevices.FirstOrDefault(d => d.DeviceType == deviceType);
                        if (accelerator != null)
                        {
                            if (GuiContext.Current.IsInDebugMode)
                                e.Options.VideoHardwareDevice = accelerator;

                            break;
                        }
                    }
                }

                var videoFilter = new StringBuilder();

                // The yadif filter deinterlaces the video; we check the field order if we need
                // to deinterlace the video automatically
                if (requiresDeinterlace)
                    videoFilter.Append("yadif,");

                // Scale down to maximum 1080p screen resolution.
                if (videoStream.PixelHeight > 1080)
                {
                    // e.Options.VideoHardwareDevice = null;
                    videoFilter.Append($"scale=-1:1080,");
                }

                e.Options.VideoFilter = videoFilter.ToString().TrimEnd(',');

                Media.ClosedCaptionsChannel = videoStream.HasClosedCaptions ?
                    CaptionsChannel.CC1 : CaptionsChannel.CCP;
            }

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

        #region Other Media Event Handlers

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