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
    /// <seealso cref="Unosquare.FFME.Commands.MediaCommand" />
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

            if (m.IsDisposed || m.IsOpen || m.IsOpening) return;

            try
            {
                // TODO: Sometimes when the stream can't be read, the sample player stays as if it were trying to open
                // until the interrupt timeout occurs but and the Real-Time Clock continues. Strange behavior.

                // Register FFmpeg if not already done
                if (FFInterop.IsInitialized == false)
                {
                    FFInterop.Initialize(MediaEngine.FFmpegDirectory, MediaEngine.FFmpegLoadModeFlags);
                    MediaEngine.FFmpegDirectory = FFInterop.LibrariesPath;
                    MediaEngine.FFmpegLoadModeFlags = FFInterop.LibraryIdentifiers;

                    m.Log(MediaLogMessageType.Info, $"INIT FFMPEG: {ffmpeg.av_version_info()}");
                }

                m.ResetControllerProperties();
                m.IsOpening = true;
                m.MediaState = MediaEngineState.Manual;

                var mediaUrl = Source.IsFile ? Source.LocalPath : Source.ToString();

                // Create the stream container
                // the async protocol prefix allows for increased performance for local files.
                m.Container = new MediaContainer(mediaUrl, m, Source.IsFile ? "async" : null);
                m.SendOnMediaOpening();
                m.Log(MediaLogMessageType.Debug, $"{nameof(OpenCommand)}: Entered");
                m.Container.Open();

                // Set the state to stopped
                m.MediaState = MediaEngineState.Stop;

                // Set some container properties
                if ((m.HasVideo && m.VideoBitrate <= 0) || (m.HasAudio && m.AudioBitrate <= 0))
                {
                    m.BufferCacheLength = 512 * 1024;
                }
                else
                {
                    var byteRate = (m.VideoBitrate + m.AudioBitrate) / 8;
                    m.BufferCacheLength = m.Container.IsStreamRealtime ?
                        byteRate / 2 : byteRate;
                }

                m.DownloadCacheLength = m.BufferCacheLength * (m.Container.IsStreamRealtime ? 30 : 4);

                // Fire up the worker threads!
                m.StartWorkers();

                // Signal we are no longer in the opening state 
                // so we can enqueue commands in the event handler
                m.IsOpening = false;

                // Raise the opened event
                m.SendOnMediaOpened();
            }
            catch (Exception ex)
            {
                m.MediaState = MediaEngineState.Close;
                m.SendOnMediaFailed(ex);
            }
            finally
            {
                m.IsOpening = false;
                m.NotifyPropertyChanges();
                m.Log(MediaLogMessageType.Debug, $"{nameof(OpenCommand)}: Completed");
            }
        }
    }
}
