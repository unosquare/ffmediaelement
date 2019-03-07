namespace Unosquare.FFME.Commands
{
    using Core;
    using Decoding;
    using Primitives;
    using Shared;
    using System;
    using System.Collections.Generic;
    using System.Linq;
    using System.Runtime.CompilerServices;
    using System.Threading;
    using System.Threading.Tasks;
    using Workers;

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
                {
                    this.LogWarning(Aspects.EngineCommand, $"Direct Command '{command}' not accepted. Commanding is disposed or a command is pending completion.");
                    return Task.FromResult(false);
                }

                // Check if we are already open
                if (command == DirectCommandType.Open && State.IsOpen)
                {
                    this.LogWarning(Aspects.EngineCommand, $"Direct Command '{command}' not accepted. Close the media before calling Open.");
                    return Task.FromResult(false);
                }

                // Close or Change Require the media to be open
                if ((command == DirectCommandType.Close || command == DirectCommandType.Change) && !State.IsOpen)
                {
                    this.LogWarning(Aspects.EngineCommand, $"Direct Command '{command}' not accepted. Open media before calling Close.");
                    return Task.FromResult(false);
                }

                this.LogDebug(Aspects.EngineCommand, $"Direct Command '{command}' accepted. Perparing execution.");

                PendingDirectCommand = command;
                HasDirectCommandCompleted.Value = false;
                MediaCore.PausePlayback(true);

                var commandTask = new Task<bool>(() =>
                {
                    var commandResult = false;
                    var resumeResult = false;
                    Exception commandException = null;

                    // Cause an immediate packet read abort if we need to close
                    if (command == DirectCommandType.Close)
                        MediaCore.Container.SignalAbortReads(false);

                    // Pause the media core workers
                    MediaCore.Workers?.PauseAll();

                    // pause the queue processor
                    PauseAsync().Wait();

                    // clear the command queue and requests
                    ClearPriorityCommands();
                    ClearSeekCommands();

                    // execute the command
                    try
                    {
                        this.LogDebug(Aspects.EngineCommand, $"Direct Command '{command}' entered");
                        resumeResult = commandDeleagte.Invoke();
                    }
                    catch (Exception ex)
                    {
                        this.LogError(Aspects.EngineCommand, $"Direct Command '{command}' execution error", ex);
                        commandException = ex;
                        commandResult = false;
                    }

                    // We are done executing -- Update the commanding state
                    // The post-procesor will use the new IsOpening, IsClosing and IsChanging states
                    PendingDirectCommand = DirectCommandType.None;

                    try
                    {
                        // Update the sate based on command result
                        commandResult = PostProcessDirectCommand(command, commandException, resumeResult);

                        // Resume the workers and this processor if we are in the Open state
                        if (State.IsOpen && commandResult)
                        {
                            // Resume the media core workers
                            MediaCore.Workers.ResumePaused();

                            // Resume this queue processor
                            ResumeAsync();
                        }
                    }
                    catch (Exception ex)
                    {
                        commandResult = false;
                        this.LogError(Aspects.EngineCommand, $"Direct Command '{command}' postprocessing error", ex);
                    }
                    finally
                    {
                        // Allow for a new direct command to be processed
                        HasDirectCommandCompleted.Value = true;
                        this.LogDebug(Aspects.EngineCommand, $"Direct Command '{command}' completed. Result: {commandResult}");
                    }

                    return commandResult;
                });

                commandTask.Start();
                return commandTask;
            }
        }

        /// <summary>
        /// Executes boilerplate logic required when a direct command finishes executing.
        /// </summary>
        /// <param name="command">The command.</param>
        /// <param name="commandException">The command exception -- can be null.</param>
        /// <param name="resumeMedia">Only valid for the change command.</param>
        /// <returns>Fasle if there was an exception passed as an argument. True if null was passed to command exception.</returns>
        private bool PostProcessDirectCommand(DirectCommandType command, Exception commandException, bool resumeMedia)
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
                    MediaCore.ResetPlaybackPosition(true);
                    MediaCore.State.UpdateMediaState(PlaybackStatus.Close);
                    MediaCore.SendOnMediaFailed(commandException);
                }
            }
            else if (command == DirectCommandType.Close)
            {
                // Update notification properties
                State.ResetAll();
                MediaCore.ResetPlaybackPosition(true);
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
                    if (resumeMedia)
                    {
                        MediaCore.State.UpdateMediaState(PlaybackStatus.Play);
                    }
                    else
                    {
                        MediaCore.State.UpdateMediaState(PlaybackStatus.Pause);
                    }

                    MediaCore.State.UpdateMediaState(
                        resumeMedia ? PlaybackStatus.Play : PlaybackStatus.Pause);
                }
                else
                {
                    MediaCore.SendOnMediaFailed(commandException);
                    MediaCore.State.UpdateMediaState(PlaybackStatus.Pause);
                }
            }

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
        /// <returns>Always returns false because media will not be resumed</returns>
        /// <exception cref="MediaContainerException">Unable to initialize at least one audio or video component from the input stream.</exception>
        private bool CommandOpenMedia(IMediaInputStream inputStream, Uri streamUri)
        {
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
                            // The async protocol prefix by default does not ssem to provide
                            // any performance improvements. Just leaving it for future reference below.
                            // containerConfig.ProtocolPrefix = "async"
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
                        // Example: streamOptions.PrivateOptions["framerate"] = "20"
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
                StartWorkers();
            }
            catch
            {
                try { StopWorkers(); } catch { /* Ignore any exceptions and continue */ }
                try { MediaCore.Container?.Dispose(); } catch { /* Ignore any exceptions and continue */ }
                DisposePreloadedSubtitles();
                MediaCore.Container = null;
                throw;
            }

            return false;
        }

        /// <summary>
        /// Provides the implementation for the Close Media Command.
        /// </summary>
        /// <returns>Always returns false because media will not be resumed</returns>
        private bool CommandCloseMedia()
        {
            // Wait for the workers to stop
            StopWorkers();

            // Dispose the container
            MediaCore.Container?.Dispose();
            MediaCore.Container = null;

            return false;
        }

        /// <summary>
        /// Provides the implementation for the Change Media Command.
        /// </summary>
        /// <param name="playWhenCompleted">If media should be resume when the command gets pot processed.</param>
        /// <returns>Simply return the play when completed boolean if there are no exceptions</returns>
        private bool CommandChangeMedia(bool playWhenCompleted)
        {
            // Signal a change so the user get the chance to update
            // selected streams and options
            MediaCore.SendOnMediaChanging();

            // Side load subtitles
            PreLoadSubtitles();

            // Recreate selected streams as media components
            MediaCore.Container.UpdateComponents();
            MediaCore.State.UpdateFixedContainerProperties();

            // Dispose unused rendered and blocks and create new ones
            InitializeRendering();

            // Depending on whether or not the media is seekable
            // perform either a seek operation or a quick buffering operation.
            if (State.IsSeekable)
            {
                // Let's simply do an automated seek
                SeekMedia(new SeekOperation(MediaCore.PlaybackClock(), SeekMode.Normal), CancellationToken.None);
            }

            return playWhenCompleted;
        }

        #endregion

        #region Implementation Helpers

        /// <summary>
        /// Initializes the media block buffers and
        /// starts packet reader, frame decoder, and block rendering workers.
        /// </summary>
        private void StartWorkers()
        {
            MediaCore.Clock.SpeedRatio = Constants.Controller.DefaultSpeedRatio;

            // Ensure renderers and blocks are available
            InitializeRendering();

            // Instantiate the workers and fire them up.
            MediaCore.Workers = new MediaWorkerSet(MediaCore);
            MediaCore.Workers.Start();
        }

        /// <summary>
        /// Stops the packet reader, frame decoder, and block renderers
        /// </summary>
        private void StopWorkers()
        {
            // Pause the clock so no further updates are propagated
            MediaCore.PausePlayback(true);

            // Cause an immediate Packet read abort
            MediaCore.Container?.SignalAbortReads(false);

            // This causes the workers to stop and dispose.
            MediaCore.Workers.Dispose();

            // Call close on all renderers
            foreach (var renderer in MediaCore.Renderers.Values)
                renderer.Close();

            // Remove the renderers disposing of them
            MediaCore.Renderers.Clear();

            // Dispose the Blocks for all components
            foreach (var kvp in MediaCore.Blocks)
                kvp.Value.Dispose();

            MediaCore.Blocks.Clear();
            DisposePreloadedSubtitles();

            // Clear the render times
            MediaCore.LastRenderTime.Clear();

            // Reset the clock
            MediaCore.ResetPlaybackPosition(true);
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        private MediaType[] GetCurrentComponentTypes()
        {
            var result = new List<MediaType>(4);
            result.AddRange(MediaCore.Container.Components.MediaTypes);

            if (MediaCore.PreloadedSubtitles != null)
                result.Add(MediaType.Subtitle);

            return result.Distinct().ToArray();
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        private MediaType[] GetCurrentRenderingTypes()
        {
            var currentMediaTypes = new List<MediaType>(8);
            currentMediaTypes.AddRange(MediaCore?.Renderers?.Keys?.ToArray() ?? new MediaType[] { });
            currentMediaTypes.AddRange(MediaCore?.Blocks?.Keys?.ToArray() ?? new MediaType[] { });

            return currentMediaTypes.Distinct().ToArray();
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        private void InitializeRendering()
        {
            var oldMediaTypes = GetCurrentRenderingTypes();

            // We always remove the audio renderer in case there is a change in audio device.
            if (MediaCore.Renderers.ContainsKey(MediaType.Audio))
            {
                MediaCore.Renderers[MediaType.Audio].Close();
                MediaCore.Renderers.Remove(MediaType.Audio);
            }

            // capture the newly selected media types
            var newMediaTypes = GetCurrentComponentTypes();

            // capture all media types
            var allMediaTypes = oldMediaTypes.Union(newMediaTypes).Distinct().ToArray();

            // find all existing component blocks and renderers that are no longer needed
            var removableRenderers = oldMediaTypes.Where(t => !newMediaTypes.Contains(t)).Distinct().ToArray();

            // find all existing component renderers that are no longer needed
            foreach (var t in removableRenderers)
            {
                // Remove the renderer for the component
                if (!MediaCore.Renderers.ContainsKey(t))
                    continue;

                MediaCore.Renderers[t].Close();
                MediaCore.Renderers.Remove(t);
            }

            // Remove blocks that no longer are required or don't match in cache size
            foreach (var t in allMediaTypes)
            {
                // if blocks don't exist we don't need to remove them
                if (!MediaCore.Blocks.ContainsKey(t))
                    continue;

                // if blocks are in the new components and match in block size,
                // we don't need to remove them.
                if (newMediaTypes.Contains(t) && MediaCore.Blocks[t].Capacity == Constants.GetMaxBlocks(t, MediaCore))
                    continue;

                MediaCore.Blocks[t].Dispose();
                MediaCore.Blocks.Remove(t);
            }

            // Create the block buffers and renderers as necessary
            foreach (var t in newMediaTypes)
            {
                if (MediaCore.Blocks.ContainsKey(t) == false)
                    MediaCore.Blocks[t] = new MediaBlockBuffer(Constants.GetMaxBlocks(t, MediaCore), t);

                if (MediaCore.Renderers.ContainsKey(t) == false)
                    MediaCore.Renderers[t] = MediaEngine.Platform.CreateRenderer(t, MediaCore);

                MediaCore.Blocks[t].Clear();
                MediaCore.Renderers[t].WaitForReadyState();
                MediaCore.InvalidateRenderer(t);
            }
        }

        /// <summary>
        /// Pre-loads the subtitles from the MediaOptions.SubtitlesUrl.
        /// </summary>
        private void PreLoadSubtitles()
        {
            DisposePreloadedSubtitles();
            var subtitlesUrl = MediaCore.MediaOptions.SubtitlesUrl;

            // Don't load a thing if we don't have to
            if (string.IsNullOrWhiteSpace(subtitlesUrl))
                return;

            try
            {
                MediaCore.PreloadedSubtitles = MediaEngine.LoadBlocks(subtitlesUrl, MediaType.Subtitle, MediaCore);

                // Process and adjust subtitle delays if necessary
                if (MediaCore.MediaOptions.SubtitlesDelay != TimeSpan.Zero)
                {
                    var delay = MediaCore.MediaOptions.SubtitlesDelay;
                    for (var i = 0; i < MediaCore.PreloadedSubtitles.Count; i++)
                    {
                        var target = MediaCore.PreloadedSubtitles[i];
                        target.StartTime = TimeSpan.FromTicks(target.StartTime.Ticks + delay.Ticks);
                        target.EndTime = TimeSpan.FromTicks(target.EndTime.Ticks + delay.Ticks);
                        target.Duration = TimeSpan.FromTicks(target.EndTime.Ticks - target.StartTime.Ticks);
                    }
                }

                MediaCore.MediaOptions.IsSubtitleDisabled = true;
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
