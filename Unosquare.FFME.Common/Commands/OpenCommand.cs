namespace Unosquare.FFME.Commands
{
    using Core;
    using Decoding;
    using FFmpeg.AutoGen;
    using Shared;
    using System;

    /// <summary>
    /// Implements the logic to open a media stream.
    /// </summary>
    /// <seealso cref="MediaCommand" />
    internal sealed class OpenCommand : MediaCommand
    {
        /// <summary>
        /// Initializes a new instance of the <see cref="OpenCommand" /> class.
        /// </summary>
        /// <param name="manager">The manager.</param>
        /// <param name="source">The source.</param>
        public OpenCommand(MediaCommandManager manager, Uri source)
            : base(manager, MediaCommandType.Open)
        {
            Source = source;
        }

        /// <summary>
        /// Gets the source uri of the media stream.
        /// </summary>
        public Uri Source { get; }

        /// <summary>
        /// Performs the actions that this command implements.
        /// </summary>
        internal override void ExecuteInternal()
        {
            var m = Manager.MediaCore;

            if (m.IsDisposed || m.State.IsOpen || m.State.IsOpening) return;

            try
            {
                // TODO: Sometimes when the stream can't be read, the sample player stays as if it were trying to open
                // until the interrupt timeout occurs but and the Real-Time Clock continues. Strange behavior. Investigate more.

                // Signal the initial state
                m.State.ResetMediaProperties();
                m.State.Source = Source;
                m.State.IsOpening = true;

                // Register FFmpeg libraries if not already done
                if (FFInterop.Initialize(MediaEngine.FFmpegDirectory, MediaEngine.FFmpegLoadModeFlags))
                {
                    // Set the folders and lib identifiers
                    MediaEngine.FFmpegDirectory = FFInterop.LibrariesPath;
                    MediaEngine.FFmpegLoadModeFlags = FFInterop.LibraryIdentifiers;

                    // Log an init message
                    m.Log(MediaLogMessageType.Info,
                        $"{nameof(FFInterop)}.{nameof(FFInterop.Initialize)}: FFmpeg v{ffmpeg.av_version_info()}");
                }

                // Convert the URI object to something the Media Container understands
                var mediaUrl = Source.IsFile ? Source.LocalPath : Source.ToString();

                // Create the stream container
                // the async protocol prefix allows for increased performance for local files.
                var streamOptions = new StreamOptions();

                // Set the default protocol Prefix
                try { streamOptions.ProtocolPrefix = Source.IsFile ? "async" : null; }
                catch { }

                // GDIGRAB: Example URI: format://gdigrab?desktop
                if (string.IsNullOrWhiteSpace(Source.Scheme) == false
                    && (Source.Scheme.Equals("format") || Source.Scheme.Equals("device"))
                    && string.IsNullOrWhiteSpace(Source.Host) == false
                    && string.IsNullOrWhiteSpace(streamOptions.Input.ForcedInputFormat)
                    && string.IsNullOrWhiteSpace(Source.Query) == false)
                {
                    // Update the Input format and container input URL
                    // It is also possible to set some input options as follows:
                    // streamOptions.Input.Add(StreamInputOptions.Names.FrameRate, "20");
                    streamOptions.Input.ForcedInputFormat = Source.Host;
                    mediaUrl = Uri.UnescapeDataString(Source.Query).TrimStart('?');
                    m.Log(MediaLogMessageType.Info, $"Media URI will be updated. Input Format: {Source.Host}, Input Argument: {mediaUrl}");
                }

                // Allow the stream input options to be changed
                m.SendOnMediaInitializing(streamOptions, mediaUrl);

                // Instantiate the internal container
                m.Container = new MediaContainer(mediaUrl, streamOptions, m);

                // Notify the user media is opening and allow for media options to be modified
                // Stuff like audio and video filters and stream selection can be performed here.
                m.SendOnMediaOpening();

                // Notify Media will start opening
                m.Log(MediaLogMessageType.Debug, $"{nameof(OpenCommand)}: Entered");
                m.Container.Open();

                // Reset buffering properties
                m.State.InitializeBufferingProperties();

                // Charge! Fire up the worker threads!
                m.StartWorkers();

                // Set the state to stopped and exit the IsOpening state
                m.State.IsOpening = false;
                m.State.UpdateMediaState(PlaybackStatus.Stop);

                // Raise the opened event
                m.SendOnMediaOpened();
            }
            catch (Exception ex)
            {
                m.State.UpdateMediaState(PlaybackStatus.Close);
                m.SendOnMediaFailed(ex);
            }
            finally
            {
                // Signal we are no longer in the opening state
                // so we can enqueue commands in the command handler
                m.State.IsOpening = false;
                m.Log(MediaLogMessageType.Debug, $"{nameof(OpenCommand)}: Completed");
            }
        }
    }
}
