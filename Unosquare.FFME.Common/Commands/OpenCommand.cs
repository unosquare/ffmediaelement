namespace Unosquare.FFME.Commands
{
    using Core;
    using Decoding;
    using Shared;
    using System;
    using System.Threading.Tasks;

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
        /// Initializes a new instance of the <see cref="OpenCommand"/> class.
        /// </summary>
        /// <param name="manager">The manager.</param>
        /// <param name="inputStream">The custom implementation of an input stream.</param>
        public OpenCommand(MediaCommandManager manager, IMediaInputStream inputStream)
            : base(manager, MediaCommandType.Open)
        {
            InputStream = inputStream;
            Source = inputStream.StreamUri;
        }

        /// <summary>
        /// Gets the source uri of the media stream.
        /// </summary>
        public Uri Source { get; }

        /// <summary>
        /// Gets the custom input stream object when the open command
        /// was instantiated using a stream and not a URI.
        /// </summary>
        public IMediaInputStream InputStream { get; }

        /// <summary>
        /// Performs the actions that this command implements.
        /// </summary>
        /// <returns>A <see cref="Task"/> representing the asynchronous operation.</returns>
        internal override async Task ExecuteInternal()
        {
            var m = Manager.MediaCore;

            if (m.IsDisposed || m.State.IsOpen || m.State.IsOpening || m.State.IsChanging) return;

            // Notify Media will start opening
            m.Log(MediaLogMessageType.Debug, $"{nameof(OpenCommand)}: Entered");

            try
            {
                // TODO: Sometimes when the stream can't be read, the sample player stays as if it were trying to open
                // until the interrupt timeout occurs but and the Real-Time Clock continues. Strange behavior. Investigate more.

                // Signal the initial state
                m.State.ResetMediaProperties();
                m.State.Source = Source;
                m.State.IsOpening = true;

                // Register FFmpeg libraries if not already done
                if (MediaEngine.LoadFFmpeg())
                {
                    // Log an init message
                    m.Log(MediaLogMessageType.Info,
                        $"{nameof(FFInterop)}.{nameof(FFInterop.Initialize)}: FFmpeg v{MediaEngine.FFmpegVersionInfo}");
                }

                // Create a default stream container configuration object
                var containerConfig = new ContainerConfiguration();

                // Convert the URI object to something the Media Container understands (Uri to String)
                var mediaUrl = Source.ToString();

                // When opening via URL (and not via custom input stream), fixup the protocols and stuff
                if (InputStream == null)
                {
                    try
                    {
                        // the async protocol prefix allows for increased performance for local files.
                        // or anything that is file-system related
                        if (Source.IsFile || Source.IsUnc)
                        {
                            // Set the default protocol Prefix
                            mediaUrl = Source.LocalPath;
                            containerConfig.ProtocolPrefix = "async";
                        }
                    }
                    catch { }

                    // Support device URLs
                    // GDIGRAB: Example URI: device://gdigrab?desktop
                    if (string.IsNullOrWhiteSpace(Source.Scheme) == false
                        && (Source.Scheme.Equals("format") || Source.Scheme.Equals("device"))
                        && string.IsNullOrWhiteSpace(Source.Host) == false
                        && string.IsNullOrWhiteSpace(containerConfig.ForcedInputFormat)
                        && string.IsNullOrWhiteSpace(Source.Query) == false)
                    {
                        // Update the Input format and container input URL
                        // It is also possible to set some input options as follows:
                        // streamOptions.PrivateOptions["framerate"] = "20";
                        containerConfig.ForcedInputFormat = Source.Host;
                        mediaUrl = Uri.UnescapeDataString(Source.Query).TrimStart('?');
                        m.Log(MediaLogMessageType.Info, $"Media URI will be updated. Input Format: {Source.Host}, Input Argument: {mediaUrl}");
                    }
                }

                // Allow the stream input options to be changed
                await m.SendOnMediaInitializing(containerConfig, mediaUrl);

                // Instantiate the internal container using either a URL (default) or a custom input stream.
                if (InputStream == null)
                    m.Container = new MediaContainer(mediaUrl, containerConfig, m);
                else
                    m.Container = new MediaContainer(InputStream, containerConfig, m);

                // Notify the user media is opening and allow for media options to be modified
                // Stuff like audio and video filters and stream selection can be performed here.
                await m.SendOnMediaOpening();

                // Side-load subtitles if requested
                m.PreloadSubtitles();

                // Get the main container open
                m.Container.Open();

                // Reset buffering properties
                m.State.InitializeBufferingProperties();

                // Check if we have at least audio or video here
                if (m.Container.Components.HasAudio == false && m.Container.Components.HasVideo == false)
                    throw new MediaContainerException($"Unable to initialize at least one audio or video component fron the input stream.");

                // Charge! We are good to go, fire up the worker threads!
                m.StartWorkers();

                // Set the state to stopped and exit the IsOpening state
                m.State.IsOpening = false;
                m.State.UpdateMediaState(PlaybackStatus.Stop);

                // Raise the opened event
                await m.SendOnMediaOpened(m.Container.MediaInfo);
            }
            catch (Exception ex)
            {
                try { m.Container?.Dispose(); } catch { }
                m.DisposePreloadedSubtitles();
                m.Container = null;
                m.State.UpdateMediaState(PlaybackStatus.Close);
                await m.SendOnMediaFailed(ex);
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
