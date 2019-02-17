namespace Unosquare.FFME.Commands
{
    using Core;
    using Decoding;
    using Primitives;
    using Shared;
    using System;
    using System.Linq;
    using System.Threading;
    using System.Threading.Tasks;

    internal partial class CommandManager
    {
        #region State Backing

        private readonly AtomicBoolean HasDirectCommandCompleted = new AtomicBoolean(true);
        private readonly AtomicInteger m_PendingDirectCommand = new AtomicInteger((int)DirectCommandType.None);

        #endregion

        #region Properties

        /// <summary>
        /// Gets a value indicating whether a <see cref="OpenMediaAsync(Uri)"/> operation is in progress.
        /// </summary>
        public bool IsOpening => PendingDirectCommand == DirectCommandType.Open;

        /// <summary>
        /// Gets a value indicating whether a <see cref="CloseMediaAsync"/> operation is in progress.
        /// </summary>
        public bool IsClosing => PendingDirectCommand == DirectCommandType.Close;

        /// <summary>
        /// Gets a value indicating whether a <see cref="ChangeMediaAsync"/> operation is in progress.
        /// </summary>
        public bool IsChanging => PendingDirectCommand == DirectCommandType.Change;

        /// <summary>
        /// Gets a value indicating the direct command that is pending or in progress.
        /// </summary>
        private DirectCommandType PendingDirectCommand
        {
            get => (DirectCommandType)m_PendingDirectCommand.Value;
            set => m_PendingDirectCommand.Value = (int)value;
        }

        /// <summary>
        /// Gets a value indicating whether a direct command is pending or in progress.
        /// </summary>
        private bool IsDirectCommandPending => PendingDirectCommand != DirectCommandType.None && HasDirectCommandCompleted.Value;

        #endregion

        #region Execution Helpers

        /// <summary>
        /// Execute boilerplate logic required ofr the execution of direct commands.
        /// </summary>
        /// <param name="command">The command.</param>
        /// <param name="commandDeleagte">The command deleagte.</param>
        /// <returns>The awaitable task.</returns>
        private Task<bool> ExecuteDirectCommand(DirectCommandType command, Func<bool> commandDeleagte)
        {
            lock (SyncLock)
            {
                // Check the basic conditions for a direct command to execute
                if (IsDisposed || IsDisposing || IsDirectCommandPending || command == DirectCommandType.None)
                    return Task.FromResult(false);

                // Check if we are already open
                if (command == DirectCommandType.Open && State.IsOpen)
                    return Task.FromResult(false);

                // Close or Change Require the media to be open
                if ((command == DirectCommandType.Close || command == DirectCommandType.Change) && !State.IsOpen)
                    return Task.FromResult(false);

                PendingDirectCommand = command;
                HasDirectCommandCompleted.Value = false;

                var commandTask = new Task<bool>(() =>
                {
                    var result = false;
                    Exception commandException = null;

                    // TODO: The below causes issues while performing seeks.
                    // Cause an immediate Packet read abort if we need to close
                    if (command == DirectCommandType.Close)
                        MediaCore.Container.SignalAbortReads(false);

                    // pause the queue processor
                    PauseAsync().Wait();

                    // clear the command queue and requests
                    ClearPriorityCommands();
                    ClearSeekCommands();

                    // execute the command
                    try
                    {
                        result = commandDeleagte.Invoke();
                    }
                    catch (Exception ex)
                    {
                        commandException = ex;
                    }

                    // We are done executing -- Update the commanding state
                    // The post-procesor will use the new IsOpening, IsClosing and IsChanging states
                    PendingDirectCommand = DirectCommandType.None;

                    // Update the sate based on command result
                    result = PostProcessDirectCommand(command, commandException, result);

                    // Resume the workers and this processor if we are in the Open state
                    if (State.IsOpen)
                    {
                        // Resume the workers
                        MediaCore.Workers.Resume(false);

                        // Resume this queue processor
                        ResumeAsync();
                    }

                    // Allow for a new direct command to be processed
                    HasDirectCommandCompleted.Value = true;

                    return result;
                });

                commandTask.ConfigureAwait(false);
                commandTask.Start();
                return commandTask;
            }
        }

        /// <summary>
        /// Executes boilerplate logic required when a direct command finishes executing.
        /// </summary>
        /// <param name="command">The command.</param>
        /// <param name="commandException">The command exception -- can be null.</param>
        /// <param name="commandResult">Contains the command result.</param>
        /// <returns>Fasle if there was an exception. True if no exceptions occurred.</returns>
        private bool PostProcessDirectCommand(DirectCommandType command, Exception commandException, bool commandResult)
        {
            if (command == DirectCommandType.Open)
            {
                MediaCore.State.UpdateFixedContainerProperties();

                if (commandException == null)
                {
                    MediaCore.State.UpdateMediaState(PlaybackStatus.Stop);
                    MediaCore.SendOnMediaOpened();
                }
                else
                {
                    MediaCore.ResetPosition();
                    MediaCore.State.UpdateMediaState(PlaybackStatus.Close);
                    MediaCore.SendOnMediaFailed(commandException);
                }
            }
            else if (command == DirectCommandType.Close)
            {
                // Update notification properties
                State.ResetAll();
                MediaCore.ResetPosition();
                State.UpdateMediaState(PlaybackStatus.Close);
                State.UpdateSource(null);

                // Notify media has closed
                MediaCore.SendOnMediaClosed();
                LogReferenceCounter();
            }
            else if (command == DirectCommandType.Change)
            {
                MediaCore.State.UpdateFixedContainerProperties();

                if (commandException == null)
                {
                    MediaCore.SendOnMediaChanged();

                    // command result contains the play after seek.
                    if (commandResult)
                    {
                        MediaCore.ResumePlayback();
                    }
                    else
                    {
                        MediaCore.State.UpdateMediaState(PlaybackStatus.Pause);
                    }

                    MediaCore.State.UpdateMediaState(
                        commandResult ? PlaybackStatus.Play : PlaybackStatus.Pause);
                }
                else
                {
                    MediaCore.SendOnMediaFailed(commandException);
                    MediaCore.State.UpdateMediaState(PlaybackStatus.Pause);
                }
            }

            this.LogDebug(Aspects.EngineCommand, $"{command} Completed");

            // return true if there was no exception found running the command.
            return commandException == null;
        }

        #endregion

        #region Command Implementations

        /// <summary>
        /// Provides the implementation for the Open Media Command.
        /// </summary>
        /// <param name="inputStream">The input stream.</param>
        /// <param name="streamUri">The stream URI.</param>
        /// <returns>True if the command was successful</returns>
        /// <exception cref="MediaContainerException">Unable to initialize at least one audio or video component from the input stream.</exception>
        private bool CommandOpenMedia(IMediaInputStream inputStream, Uri streamUri)
        {
            // Notify Media will start opening
            this.LogDebug(Aspects.EngineCommand, $"{DirectCommandType.Open} Entered");
            var result = false;

            try
            {
                // TODO: Sometimes when the stream can't be read, the sample player stays as if it were trying to open
                // until the interrupt timeout occurs but and the Real-Time Clock continues. Strange behavior. Investigate more.

                // Signal the initial state
                var source = inputStream == null ? streamUri : inputStream.StreamUri;
                State.ResetAll();
                State.UpdateSource(source);

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
                var mediaUrl = Uri.EscapeUriString(source.ToString());

                // When opening via URL (and not via custom input stream), fix up the protocols and stuff
                if (inputStream == null)
                {
                    try
                    {
                        // the async protocol prefix allows for increased performance for local files.
                        // or anything that is file-system related
                        if (source.IsFile || source.IsUnc)
                        {
                            // Set the default protocol Prefix
                            // containerConfig.ProtocolPrefix = "async";
                            mediaUrl = source.LocalPath;
                        }
                    }
                    catch { /* Ignore exception and continue */ }

                    // Support device URLs
                    // GDI GRAB: Example URI: device://gdigrab?desktop
                    if (string.IsNullOrWhiteSpace(source.Scheme) == false
                        && (source.Scheme.Equals("format") || source.Scheme.Equals("device"))
                        && string.IsNullOrWhiteSpace(source.Host) == false
                        && string.IsNullOrWhiteSpace(containerConfig.ForcedInputFormat)
                        && string.IsNullOrWhiteSpace(source.Query) == false)
                    {
                        // Update the Input format and container input URL
                        // It is also possible to set some input options as follows:
                        // ReSharper disable once CommentTypo
                        // streamOptions.PrivateOptions["framerate"] = "20";
                        containerConfig.ForcedInputFormat = source.Host;
                        mediaUrl = Uri.UnescapeDataString(source.Query).TrimStart('?');
                        this.LogInfo(Aspects.EngineCommand,
                            $"Media URI will be updated. Input Format: {source.Host}, Input Argument: {mediaUrl}");
                    }
                }

                // Allow the stream input options to be changed
                MediaCore.SendOnMediaInitializing(containerConfig, mediaUrl);

                // Instantiate the internal container using either a URL (default) or a custom input stream.
                MediaCore.Container = inputStream == null ?
                    new MediaContainer(mediaUrl, containerConfig, MediaCore) :
                    new MediaContainer(inputStream, containerConfig, MediaCore);

                // Notify the user media is opening and allow for media options to be modified
                // Stuff like audio and video filters and stream selection can be performed here.
                State.UpdateFixedContainerProperties();
                MediaCore.SendOnMediaOpening();

                // Side-load subtitles if requested
                PreLoadSubtitles();

                // Get the main container open
                MediaCore.Container.Open();

                // Reset buffering properties
                State.UpdateFixedContainerProperties();
                State.InitializeBufferingStatistics();

                // Check if we have at least audio or video here
                if (State.HasAudio == false && State.HasVideo == false)
                    throw new MediaContainerException("Unable to initialize at least one audio or video component from the input stream.");

                // Charge! We are good to go, fire up the worker threads!
                MediaCore.StartWorkers();

                result = true;
            }
            catch
            {
                try { MediaCore.StopWorkers(); } catch { /* Ignore any exceptions and continue */ }
                try { MediaCore.Container?.Dispose(); } catch { /* Ignore any exceptions and continue */ }
                DisposePreloadedSubtitles();
                MediaCore.Container = null;
                throw;
            }

            return result;
        }

        /// <summary>
        /// Provides the implementation for the Close Media Command.
        /// </summary>
        /// <returns>True if the command was successful</returns>
        private bool CommandCloseMedia()
        {
            var result = false;

            try
            {
                this.LogDebug(Aspects.EngineCommand, $"{DirectCommandType.Close} Entered");

                // Wait for the workers to stop
                MediaCore.StopWorkers();

                // Dispose the container
                MediaCore.Container?.Dispose();
                MediaCore.Container = null;

                // Dispose the Blocks for all components
                foreach (var kvp in MediaCore.Blocks)
                    kvp.Value.Dispose();

                MediaCore.Blocks.Clear();
                DisposePreloadedSubtitles();

                // Clear the render times
                MediaCore.LastRenderTime.Clear();

                result = true;
            }
            catch
            {
                throw;
            }

            return result;
        }

        /// <summary>
        /// Provides the implementation for the Change Media Command.
        /// </summary>
        /// <param name="playWhenCompleted">if set to <c>true</c> [play when completed].</param>
        /// <returns>True if the command was successful</returns>
        private bool CommandChangeMedia(bool playWhenCompleted)
        {
            this.LogDebug(Aspects.EngineCommand, $"{DirectCommandType.Change} Entered");

            // Signal the start of a sync-buffering scenario
            MediaCore.Clock.Pause();

            // Wait for the cycles to complete
            MediaCore.Workers.Pause(true);

            // Signal a change so the user get the chance to update
            // selected streams and options
            MediaCore.SendOnMediaChanging();

            // Side load subtitles
            PreLoadSubtitles();

            // Capture the current media types before components change
            var oldMediaTypes = MediaCore.Container.Components.MediaTypes.ToArray();

            // Recreate selected streams as media components
            var mediaTypes = MediaCore.Container.UpdateComponents();
            MediaCore.State.UpdateFixedContainerProperties();

            // find all existing component blocks and renderers that no longer exist
            // We always remove the audio component in case there is a change in audio device
            var removableMediaTypes = oldMediaTypes
                .Where(t => mediaTypes.Contains(t) == false)
                .Union(new[] { MediaType.Audio })
                .Distinct()
                .ToArray();

            // find all existing component blocks and renderers that no longer exist
            foreach (var t in removableMediaTypes)
            {
                // Remove the renderer for the component
                if (MediaCore.Renderers.ContainsKey(t))
                {
                    MediaCore.Renderers[t].Close();
                    MediaCore.Renderers.Remove(t);
                }

                // Remove the block buffer for the component
                if (!MediaCore.Blocks.ContainsKey(t)) continue;
                MediaCore.Blocks[t]?.Dispose();
                MediaCore.Blocks.Remove(t);
            }

            // Create the block buffers and renderers as necessary
            foreach (var t in mediaTypes)
            {
                if (MediaCore.Blocks.ContainsKey(t) == false)
                    MediaCore.Blocks[t] = new MediaBlockBuffer(Constants.MaxBlocks[t], t);

                if (MediaCore.Renderers.ContainsKey(t) == false)
                    MediaCore.Renderers[t] = MediaEngine.Platform.CreateRenderer(t, MediaCore);

                MediaCore.Blocks[t].Clear();
                MediaCore.Renderers[t].WaitForReadyState();
            }

            // Depending on whether or not the media is seekable
            // perform either a seek operation or a quick buffering operation.
            if (State.IsSeekable)
            {
                // Let's simply do an automated seek
                SeekMedia(new SeekOperation(MediaCore.WallClock, SeekMode.Normal), CancellationToken.None);
            }
            else
            {
                // Let's perform quick-buffering
                // MediaCore.Container.Components.RunQuickBuffering(MediaCore);

                // Mark the renderers as invalidated
                foreach (var t in mediaTypes)
                    MediaCore.InvalidateRenderer(t);
            }

            return playWhenCompleted;
        }

        #endregion

        #region Implementation Helpers

        /// <summary>
        /// Pre-loads the subtitles from the MediaOptions.SubtitlesUrl.
        /// </summary>
        private void PreLoadSubtitles()
        {
            DisposePreloadedSubtitles();
            var subtitlesUrl = MediaCore.Container.MediaOptions.SubtitlesUrl;

            // Don't load a thing if we don't have to
            if (string.IsNullOrWhiteSpace(subtitlesUrl))
                return;

            try
            {
                MediaCore.PreloadedSubtitles = MediaEngine.LoadBlocks(subtitlesUrl, MediaType.Subtitle, MediaCore);

                // Process and adjust subtitle delays if necessary
                if (MediaCore.Container.MediaOptions.SubtitlesDelay != TimeSpan.Zero)
                {
                    var delay = MediaCore.Container.MediaOptions.SubtitlesDelay;
                    for (var i = 0; i < MediaCore.PreloadedSubtitles.Count; i++)
                    {
                        var target = MediaCore.PreloadedSubtitles[i];
                        target.StartTime = TimeSpan.FromTicks(target.StartTime.Ticks + delay.Ticks);
                        target.EndTime = TimeSpan.FromTicks(target.EndTime.Ticks + delay.Ticks);
                        target.Duration = TimeSpan.FromTicks(target.EndTime.Ticks - target.StartTime.Ticks);
                    }
                }

                MediaCore.Container.MediaOptions.IsSubtitleDisabled = true;
            }
            catch (MediaContainerException mex)
            {
                DisposePreloadedSubtitles();
                this.LogWarning(Aspects.Component,
                    $"No subtitles to side-load found in media '{subtitlesUrl}'. {mex.Message}");
            }
        }

        /// <summary>
        /// Disposes the preloaded subtitles.
        /// </summary>
        private void DisposePreloadedSubtitles()
        {
            MediaCore.PreloadedSubtitles?.Dispose();
            MediaCore.PreloadedSubtitles = null;
        }

        #endregion
    }
}
