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
                // until the interrupt timeout occurs but and the Real-Time Clock continues. Strange behavior.

                // Signal the initial state
                m.State.ResetControllerProperties();
                m.State.IsOpening = true;
                m.State.MediaState = PlaybackStatus.Manual;

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
                m.Container = new MediaContainer(mediaUrl, m, Source.IsFile ? "async" : null);
                m.SendOnMediaOpening();
                m.Log(MediaLogMessageType.Debug, $"{nameof(OpenCommand)}: Entered");
                m.Container.Open();
                m.ResetBufferingProperties();

                // Set the state to stopped
                m.State.MediaState = PlaybackStatus.Stop;

                // Signal we are no longer in the opening state
                // so we can enqueue commands in the event handler
                m.State.IsOpening = false;

                // Charge! Fire up the worker threads!
                m.StartWorkers();

                // Raise the opened event
                m.SendOnMediaOpened();
            }
            catch (Exception ex)
            {
                m.State.MediaState = PlaybackStatus.Close;
                m.SendOnMediaFailed(ex);
            }
            finally
            {
                m.State.IsOpening = false;
                m.Log(MediaLogMessageType.Debug, $"{nameof(OpenCommand)}: Completed");
            }
        }
    }
}
