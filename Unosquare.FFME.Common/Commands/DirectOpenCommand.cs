﻿namespace Unosquare.FFME.Commands
{
    using Core;
    using Decoding;
    using Shared;
    using System;

    /// <summary>
    /// The Open Command Implementation
    /// </summary>
    /// <seealso cref="DirectCommandBase" />
    internal sealed class DirectOpenCommand : DirectCommandBase
    {
        private Exception ExceptionResult;

        /// <summary>
        /// Initializes a new instance of the <see cref="DirectOpenCommand" /> class.
        /// </summary>
        /// <param name="mediaCore">The manager.</param>
        /// <param name="source">The source.</param>
        public DirectOpenCommand(MediaEngine mediaCore, Uri source)
            : base(mediaCore)
        {
            Source = source;
            CommandType = CommandType.Open;
        }

        /// <summary>
        /// Initializes a new instance of the <see cref="DirectOpenCommand"/> class.
        /// </summary>
        /// <param name="mediaCore">The manager.</param>
        /// <param name="inputStream">The custom implementation of an input stream.</param>
        public DirectOpenCommand(MediaEngine mediaCore, IMediaInputStream inputStream)
            : base(mediaCore)
        {
            InputStream = inputStream;
            Source = inputStream.StreamUri;
            CommandType = CommandType.Open;
        }

        /// <inheritdoc />
        public override CommandType CommandType { get; }

        /// <summary>
        /// Gets the source uri of the media stream.
        /// </summary>
        public Uri Source { get; }

        /// <summary>
        /// Gets the custom input stream object when the open command
        /// was instantiated using a stream and not a URI.
        /// </summary>
        public IMediaInputStream InputStream { get; }

        /// <inheritdoc />
        public override void PostProcess()
        {
            MediaCore.State.UpdateFixedContainerProperties();

            if (ExceptionResult == null)
            {
                MediaCore.State.UpdateMediaState(PlaybackStatus.Stop);
                MediaCore.SendOnMediaOpened();
            }
            else
            {
                MediaCore.ResetPosition();
                MediaCore.State.UpdateMediaState(PlaybackStatus.Close);
                MediaCore.SendOnMediaFailed(ExceptionResult);
            }

            this.LogDebug(Aspects.EngineCommand, $"{CommandType} Completed");
        }

        /// <inheritdoc />
        protected override void PerformActions()
        {
            // Notify Media will start opening
            this.LogDebug(Aspects.EngineCommand, $"{CommandType} Entered");
            var m = MediaCore;
            try
            {
                // TODO: Sometimes when the stream can't be read, the sample player stays as if it were trying to open
                // until the interrupt timeout occurs but and the Real-Time Clock continues. Strange behavior. Investigate more.

                // Signal the initial state
                m.State.ResetAll();
                m.State.UpdateSource(Source);

                // Register FFmpeg libraries if not already done
                if (MediaEngine.LoadFFmpeg())
                {
                    // Log an init message
                    this.LogInfo(Aspects.EngineCommand,
                        $"{nameof(FFInterop)}.{nameof(FFInterop.Initialize)}: FFmpeg v{MediaEngine.FFmpegVersionInfo}");
                }

                // Create a default stream container configuration object
                var containerConfig = new ContainerConfiguration();

                // Convert the URI object to something the Media Container understands (Uri to String)
                var mediaUrl = Uri.EscapeUriString(Source.ToString());

                // When opening via URL (and not via custom input stream), fix up the protocols and stuff
                if (InputStream == null)
                {
                    try
                    {
                        // the async protocol prefix allows for increased performance for local files.
                        // or anything that is file-system related
                        if (Source.IsFile || Source.IsUnc)
                        {
                            // Set the default protocol Prefix
                            // containerConfig.ProtocolPrefix = "async";
                            mediaUrl = Source.LocalPath;
                        }
                    }
                    catch { /* Ignore exception and continue */ }

                    // Support device URLs
                    // GDI GRAB: Example URI: device://gdigrab?desktop
                    if (string.IsNullOrWhiteSpace(Source.Scheme) == false
                        && (Source.Scheme.Equals("format") || Source.Scheme.Equals("device"))
                        && string.IsNullOrWhiteSpace(Source.Host) == false
                        && string.IsNullOrWhiteSpace(containerConfig.ForcedInputFormat)
                        && string.IsNullOrWhiteSpace(Source.Query) == false)
                    {
                        // Update the Input format and container input URL
                        // It is also possible to set some input options as follows:
                        // ReSharper disable once CommentTypo
                        // streamOptions.PrivateOptions["framerate"] = "20";
                        containerConfig.ForcedInputFormat = Source.Host;
                        mediaUrl = Uri.UnescapeDataString(Source.Query).TrimStart('?');
                        this.LogInfo(Aspects.EngineCommand,
                            $"Media URI will be updated. Input Format: {Source.Host}, Input Argument: {mediaUrl}");
                    }
                }

                // Allow the stream input options to be changed
                m.SendOnMediaInitializing(containerConfig, mediaUrl);

                // Instantiate the internal container using either a URL (default) or a custom input stream.
                m.Container = InputStream == null ?
                    new MediaContainer(mediaUrl, containerConfig, m) :
                    new MediaContainer(InputStream, containerConfig, m);

                // Notify the user media is opening and allow for media options to be modified
                // Stuff like audio and video filters and stream selection can be performed here.
                m.State.UpdateFixedContainerProperties();
                m.SendOnMediaOpening();

                // Side-load subtitles if requested
                m.PreLoadSubtitles();

                // Get the main container open
                m.Container.Open();

                // Reset buffering properties
                m.State.UpdateFixedContainerProperties();
                m.State.InitializeBufferingStatistics();

                // Packet Buffer Notification Callbacks
                m.Container.Components.OnPacketQueueChanged = (op, packet, mediaType, state) =>
                {
                    m.State.UpdateBufferingStats(state.Length, state.Count, state.CountThreshold);
                    m.BufferChangedEvent.Complete();
                };

                // Check if we have at least audio or video here
                if (m.State.HasAudio == false && m.State.HasVideo == false)
                    throw new MediaContainerException("Unable to initialize at least one audio or video component from the input stream.");

                // Charge! We are good to go, fire up the worker threads!
                m.StartWorkers();
            }
            catch (Exception ex)
            {
                try { m.StopWorkers(); } catch { /* Ignore any exceptions and continue */ }
                try { m.Container?.Dispose(); } catch { /* Ignore any exceptions and continue */ }
                m.DisposePreloadedSubtitles();
                m.Container = null;
                ExceptionResult = ex;
            }
        }
    }
}
