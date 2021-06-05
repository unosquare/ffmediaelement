namespace Unosquare.FFME.Windows.Sample
{
    using ClosedCaptions;
    using FFmpeg.AutoGen;
    using System;
    using System.Diagnostics;
    using System.IO;
    using System.Linq;
    using System.Text;
    using System.Windows;
    using System.Windows.Shell;
    using Unosquare.FFME.Common;

    public partial class MainWindow
    {
        #region Seek indexing example

        /// <summary>
        /// Loads or creates a seek index for short media files.
        /// </summary>
        /// <param name="mediaFilePath">The URL.</param>
        /// <param name="streamIndex">The associated stream index.</param>
        /// <param name="durationSeconds">The duration in seconds.</param>
        /// <returns>
        /// The seek index.
        /// </returns>
        private static VideoSeekIndex LoadOrCreateVideoSeekIndex(string mediaFilePath, int streamIndex, double durationSeconds)
        {
            var seekFileName = $"{Path.GetFileNameWithoutExtension(mediaFilePath)}.six";
            var seekFilePath = Path.Combine(App.ViewModel.Playlist.IndexDirectory, seekFileName);
            if (string.IsNullOrWhiteSpace(seekFilePath)) return null;

            if (File.Exists(seekFilePath))
            {
                using var stream = File.OpenRead(seekFilePath);
                return VideoSeekIndex.Load(stream);
            }
            else
            {
                if (!Debugger.IsAttached || durationSeconds <= 0 || durationSeconds >= 60)
                    return null;

                var seekIndex = Library.CreateVideoSeekIndex(mediaFilePath, streamIndex);
                if (seekIndex.Entries.Count <= 0) return null;

                using (var stream = File.OpenWrite(seekFilePath))
                    seekIndex.Save(stream);

                return seekIndex;
            }
        }

        #endregion

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

            Debug.WriteLine(e);
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

            if (string.IsNullOrWhiteSpace(e.Message) == false && e.Message.ContainsOrdinal("Using non-standard frame rate"))
                return;

            Debug.WriteLine(e);
        }

        /// <summary>
        /// Handles the MediaFailed event of the Media control.
        /// </summary>
        /// <param name="sender">The source of the event.</param>
        /// <param name="e">The <see cref="MediaFailedEventArgs"/> instance containing the event data.</param>
        private void OnMediaFailed(object sender, MediaFailedEventArgs e)
        {
            var errorCategory = e.ErrorException is TimeoutException
                ? "Timeout"
                : "General";

            MessageBox.Show(
                Application.Current.MainWindow,
                $"Media Failed ({errorCategory}): {e.ErrorException.GetType()}\r\n{e.ErrorException.Message}",
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
        /// <param name="e">The <see cref="MediaInitializingEventArgs"/> instance containing the event data.</param>
        private void OnMediaInitializing(object sender, MediaInitializingEventArgs e)
        {
            ViewModel.NotificationMessage = "Media is opening . . .";

            // An example of injecting input options for http/https streams
            // A simple website to get live stream examples: https://pwn.sh/tools/getstream.html
            if (e.MediaSource.StartsWith("http://", StringComparison.OrdinalIgnoreCase) ||
                e.MediaSource.StartsWith("https://", StringComparison.OrdinalIgnoreCase))
            {
                e.Configuration.PrivateOptions["user_agent"] = $"{typeof(ContainerConfiguration).Namespace}/{typeof(ContainerConfiguration).Assembly.GetName().Version}";
                e.Configuration.PrivateOptions["headers"] = "Referer:https://www.unosquare.com";
            }

            // Example of forcing tcp transport on rtsp feeds
            // RTSP is similar to HTTP but it only provides metadata about the underlying stream
            // Most RTSP compatible streams expose RTP data over both UDP and TCP.
            // TCP provides reliable communication while UDP does not
            if (e.MediaSource.StartsWith("rtsp://", StringComparison.OrdinalIgnoreCase))
            {
                e.Configuration.PrivateOptions["rtsp_transport"] = "tcp";
                e.Configuration.GlobalOptions.FlagNoBuffer = true;

                // You can change the open/read timeout before the packet reading
                // operation fails. Reaching a tiemout limit will fire the MediaFailed event
                // with a TiemoutException
                e.Configuration.ReadTimeout = TimeSpan.FromSeconds(10);
            }

            // Example of setting extra IPs for NDI (needs compatible build and Newtek binaries)
            if (e.Configuration.ForcedInputFormat == "libndi_newtek")
            {
                // Sample URL: device://libndi_newtek?COMPUTERNAME-HERE (Test Pattern)
                e.Configuration.PrivateOptions["extra_ips"] = "127.0.0.1";
            }

            // A decryption key can be set by specifying a hexadecimal string.
            if (e.MediaSource.EndsWith("03-encrypted.mp4", StringComparison.InvariantCultureIgnoreCase))
            {
                e.Configuration.PrivateOptions["decryption_key"] = "76a6c65c5ea762046bd749a2e632ccbb";
            }

            // In realtime streams these settings can be used to reduce latency (see example from issue #152)
            // e.Configuration.GlobalOptions.FlagNoBuffer = true;
            // e.Configuration.GlobalOptions.ProbeSize = 8192;
            // e.Configuration.GlobalOptions.MaxAnalyzeDuration = TimeSpan.FromSeconds(1);
        }

        /// <summary>
        /// Handles the MediaOpening event of the Media control.
        /// </summary>
        /// <param name="sender">The source of the event.</param>
        /// <param name="e">The <see cref="MediaOpeningEventArgs"/> instance containing the event data.</param>
        private void OnMediaOpening(object sender, MediaOpeningEventArgs e)
        {
            // Capture a reference to the MediaOptions object for real-time change
            // This usage of MediaOptions is unsupported.
            ViewModel.CurrentMediaOptions = e.Options;

            // the event sender is the MediaElement itself
            var media = sender as MediaElement;

            // You can start off by adjusting subtitles delay
            // This defaults to 0 but you can delay (or advance with a negative delay)
            // the subtitle timestamps.
            e.Options.SubtitlesDelay = TimeSpan.Zero; // See issue #216

            // You can render audio and video as it becomes available but the downside of disabling time
            // synchronization is that video and audio will run on their own independent clocks.
            // Do not disable Time Sync for streams that need synchronized audio and video.
            e.Options.IsTimeSyncDisabled =
                e.Info.Format == "libndi_newtek" ||
                e.Info.MediaSource.StartsWith("rtsp://uno", StringComparison.OrdinalIgnoreCase);

            // You can disable the requirement of buffering packets by setting the playback
            // buffer percent to 0. Values of less than 0.5 for live or network streams are not recommended.
            e.Options.MinimumPlaybackBufferPercent = e.Info.Format == "libndi_newtek" ? 0 : 0.5;

            // The audio renderer will try to keep the audio hardware synchronized
            // to the playback position by default.
            // A few WMV files I have tested don't have continuous enough audio packets to support
            // perfect synchronization between audio and video so we simply disable it.
            // Also if time synchronization is disabled, the recommendation is to also disable audio synchronization.
            media.RendererOptions.AudioDisableSync =
                e.Options.IsTimeSyncDisabled ||
                e.Info.MediaSource.EndsWith(".wmv", StringComparison.OrdinalIgnoreCase);

            // Legacy audio out is the use of the WinMM api as opposed to using DirectSound
            // Enable legacy audio out if you are having issues with the DirectSound driver.
            media.RendererOptions.UseLegacyAudioOut = e.Info.MediaSource.EndsWith(".wmv", StringComparison.OrdinalIgnoreCase);

            // You can limit how often the video renderer updates the picture.
            // We keep it as 0 to refresh the video according to the native stream specification.
            media.RendererOptions.VideoRefreshRateLimit = 0;

            // Get the local file path from the URL (if possible)
            var mediaFilePath = string.Empty;
            try
            {
                var url = new Uri(e.Info.MediaSource);
                mediaFilePath = url.IsFile || url.IsUnc ? Path.GetFullPath(url.LocalPath) : string.Empty;
            }
            catch { /* Ignore Exceptions */ }

            // Example of automatically side-loading SRT subs
            if (string.IsNullOrWhiteSpace(mediaFilePath) == false)
            {
                var srtFilePath = Path.ChangeExtension(mediaFilePath, "srt");
                if (File.Exists(srtFilePath))
                    e.Options.SubtitlesSource = srtFilePath;
            }

            // You can also force video FPS if necessary
            // see: https://github.com/unosquare/ffmediaelement/issues/212
            // e.Options.VideoForcedFps = 25;

            // An example of selecting a specific subtitle stream
            var subtitleStreams = e.Info.Streams.Where(kvp => kvp.Value.CodecType == AVMediaType.AVMEDIA_TYPE_SUBTITLE).Select(kvp => kvp.Value);
            var englishSubtitleStream = subtitleStreams
                .FirstOrDefault(s => s.Language != null && s.Language.StartsWith("en", StringComparison.OrdinalIgnoreCase));

            if (englishSubtitleStream != null)
                e.Options.SubtitleStream = englishSubtitleStream;

            // An example of selecting a specific audio stream
            var audioStreams = e.Info.Streams.Where(kvp => kvp.Value.CodecType == AVMediaType.AVMEDIA_TYPE_AUDIO).Select(kvp => kvp.Value);
            var englishAudioStream = audioStreams
                .FirstOrDefault(s => s.Language != null && s.Language.StartsWith("en", StringComparison.OrdinalIgnoreCase));

            if (englishAudioStream != null)
                e.Options.AudioStream = englishAudioStream;

            // Setting Advanced Video Stream Options is also possible
            if (e.Options.VideoStream is StreamInfo videoStream)
            {
                // Example of forcing a codec for a stream
                // e.Options.DecoderCodec[videoStream.StreamIndex] = "mjpeg";

                // If we have a valid seek index let's use it!
                if (string.IsNullOrWhiteSpace(mediaFilePath) == false)
                {
                    try
                    {
                        // Try to Create or Load a Seek Index
                        var durationSeconds = e.Info.Duration.TotalSeconds > 0 ? e.Info.Duration.TotalSeconds : 0;
                        var seekIndex = LoadOrCreateVideoSeekIndex(mediaFilePath, videoStream.StreamIndex, durationSeconds);

                        // Make sure the seek index belongs to the media file path
                        if (seekIndex != null &&
                            !string.IsNullOrWhiteSpace(seekIndex.MediaSource) &&
                            seekIndex.MediaSource.Equals(mediaFilePath, StringComparison.OrdinalIgnoreCase) &&
                            seekIndex.StreamIndex == videoStream.StreamIndex)
                        {
                            // Set the index on the options object.
                            e.Options.VideoSeekIndex = seekIndex;
                        }
                    }
                    catch (Exception ex)
                    {
                        // Log the exception, and ignore it. Continue execution.
                        Debug.WriteLine($"Error loading seek index data. {ex.Message}");
                    }
                }

                // Hardware device priorities
                var deviceCandidates = new[]
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
                        if (accelerator == null) continue;
                        if (Debugger.IsAttached)
                            e.Options.VideoHardwareDevice = accelerator;

                        break;
                    }
                }

                // Start building a video filter
                var videoFilter = new StringBuilder();

                // The yadif filter de-interlaces the video; we check the field order if we need
                // to de-interlace the video automatically
                if (videoStream.IsInterlaced)
                    videoFilter.Append("yadif,");

                // Scale down to maximum 1080p screen resolution.
                if (videoStream.PixelHeight > 1080)
                {
                    // e.Options.VideoHardwareDevice = null;
                    videoFilter.Append("scale=-1:1080,");
                }

                // Example of fisheye correction filter:
                // videoFilter.Append("lenscorrection=cx=0.5:cy=0.5:k1=-0.85:k2=0.25,")
                e.Options.VideoFilter = videoFilter.ToString().TrimEnd(',');

                // Since the MediaElement control belongs to the GUI thread
                // and the closed captions channel property is a dependency
                // property, we need to set it on the GUI thread.
                media.Dispatcher?.InvokeAsync(() =>
                {
                    media.ClosedCaptionsChannel = videoStream.HasClosedCaptions ?
                        CaptionsChannel.CC1 : CaptionsChannel.CCP;
                });
            }

            // Examples of setting audio filters.
            // e.Options.AudioFilter = "aecho=0.8:0.9:1000:0.3";
            // e.Options.AudioFilter = "chorus=0.5:0.9:50|60|40:0.4|0.32|0.3:0.25|0.4|0.3:2|2.3|1.3";
            // e.Options.AudioFilter = "aphaser";
        }

        /// <summary>
        /// Handles the MediaOpened event of the Media control.
        /// </summary>
        /// <param name="sender">The source of the event.</param>
        /// <param name="e">The <see cref="RoutedEventArgs"/> instance containing the event data.</param>
        private void OnMediaOpened(object sender, MediaOpenedEventArgs e)
        {
            // This will fire when the media has opened.
        }

        /// <summary>
        /// Handles the MediaReady event of the Media control.
        /// </summary>
        /// <param name="sender">The sender.</param>
        /// <param name="e">The <see cref="EventArgs"/> instance containing the event data.</param>
        private void OnMediaReady(object sender, EventArgs e)
        {
            // Set a start position (see issue #66 or issue #277)
            // Media.Position = TimeSpan.FromSeconds(5);
            // await Media.Seek(TimeSpan.FromSeconds(5));
            ViewModel.NotificationMessage = "Media opened and ready.";
        }

        /// <summary>
        /// Handles the MediaClosed event of the Media control.
        /// </summary>
        /// <param name="sender">The sender.</param>
        /// <param name="e">The <see cref="EventArgs"/> instance containing the event data.</param>
        private void OnMediaClosed(object sender, EventArgs e)
        {
            // Always close the recorder so that the file trailer is written.
            lock (RecorderSyncLock)
            {
                StreamRecorder?.Close();
                StreamRecorder = null;
            }

            ViewModel.CurrentMediaOptions = null;
            ViewModel.NotificationMessage = "Media closed.";
        }

        /// <summary>
        /// Handles the MediaChanging event of the MediaControl.
        /// </summary>
        /// <param name="sender">The sender.</param>
        /// <param name="e">The <see cref="MediaOpeningEventArgs"/> instance containing the event data.</param>
        private void OnMediaChanging(object sender, MediaOpeningEventArgs e)
        {
            ViewModel.NotificationMessage = "Media is updating . . .";

            var availableStreams = e.Info.Streams
                .Where(s => s.Value.CodecType == (AVMediaType)StreamCycleMediaType)
                .Select(x => x.Value)
                .ToList();

            if (availableStreams.Count <= 0) return;

            // Allow cycling though a null stream (means removing the stream)
            // Except for video streams.
            if (StreamCycleMediaType != MediaType.Video)
                availableStreams.Add(null);

            int currentIndex;

            switch (StreamCycleMediaType)
            {
                case MediaType.Audio:
                    currentIndex = availableStreams.IndexOf(e.Options.AudioStream);
                    break;

                case MediaType.Video:
                    currentIndex = availableStreams.IndexOf(e.Options.VideoStream);
                    break;

                case MediaType.Subtitle:
                    currentIndex = availableStreams.IndexOf(e.Options.SubtitleStream);
                    break;

                default:
                    return;
            }

            currentIndex += 1;
            if (currentIndex >= availableStreams.Count)
                currentIndex = 0;

            var newStream = availableStreams[currentIndex];
            switch (StreamCycleMediaType)
            {
                case MediaType.Audio:
                    e.Options.AudioStream = newStream;
                    break;

                case MediaType.Video:
                    e.Options.VideoStream = newStream;
                    break;

                case MediaType.Subtitle:
                    e.Options.SubtitleStream = newStream;
                    break;

                default:
                    return;
            }
        }

        /// <summary>
        /// Handles the media changed event.
        /// </summary>
        /// <param name="sender">The sender.</param>
        /// <param name="e">The <see cref="MediaOpenedEventArgs"/> instance containing the event data.</param>
        private void OnMediaChanged(object sender, MediaOpenedEventArgs e)
        {
            ViewModel.NotificationMessage = "Media updated.";
        }

        /// <summary>
        /// Called when the current audio device changes.
        /// Call <see cref="MediaElement.ChangeMedia"/> so the new default audio device gets selected.
        /// </summary>
        /// <param name="sender">The sender.</param>
        /// <param name="e">The <see cref="EventArgs"/> instance containing the event data.</param>
        private async void OnAudioDeviceStopped(object sender, EventArgs e)
        {
            ViewModel.NotificationMessage = "Audio device stopped.";
            if (sender is MediaElement media)
                await media.ChangeMedia();
        }

        #endregion

        #region Other Media Event Handlers and Methods

        /// <summary>
        /// Handles the reception of data frames.
        /// </summary>
        /// <param name="sender">The sender.</param>
        /// <param name="e">The <see cref="DataFrameReceivedEventArgs"/> instance containing the event data.</param>
        private void OnDataFrameReceived(object sender, DataFrameReceivedEventArgs e)
        {
            // Inspect the contents of the event arguments to extract raw packet data.
            // decoding is left up to the user. See #442
        }

        /// <summary>
        /// Handles the PositionChanged event of the Media control.
        /// </summary>
        /// <param name="sender">The source of the event.</param>
        /// <param name="e">The <see cref="PositionChangedEventArgs"/> instance containing the event data.</param>
        private void OnMediaPositionChanged(object sender, PositionChangedEventArgs e)
        {
            var media = sender as MediaElement;
            if (!media.IsSeekable)
            {
                ViewModel.PlaybackProgress = 0;
                return;
            }

            ViewModel.PlaybackProgress = media.NaturalDuration.HasValue
                ? (double)e.Position.Ticks / media.NaturalDuration.Value.Ticks
                : 1;
        }

        /// <summary>
        /// Called when media state changes.
        /// </summary>
        /// <param name="sender">The sender.</param>
        /// <param name="e">The <see cref="MediaStateChangedEventArgs" /> instance containing the event data.</param>
        private void OnMediaStateChanged(object sender, MediaStateChangedEventArgs e)
        {
            var media = sender as MediaElement;

            ViewModel.PlaybackProgressState = e.MediaState switch
            {
                MediaPlaybackState.Close or MediaPlaybackState.Stop => TaskbarItemProgressState.None,
                MediaPlaybackState.Manual or MediaPlaybackState.Pause => TaskbarItemProgressState.Paused,
                MediaPlaybackState.Play => media.IsSeekable
                    ? TaskbarItemProgressState.Normal
                    : TaskbarItemProgressState.Indeterminate,
                _ => TaskbarItemProgressState.None,
            };
        }

        #endregion
    }
}
