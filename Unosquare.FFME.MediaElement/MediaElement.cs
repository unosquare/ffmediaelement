namespace Unosquare.FFME
{
    using Diagnostics;
    using Events;
    using Platform;
    using Primitives;
    using System;
    using System.ComponentModel;
    using System.Runtime.CompilerServices;
    using System.Threading.Tasks;

    public partial class MediaElement : ILoggingHandler, ILoggingSource, INotifyPropertyChanged
    {
        /// <summary>
        /// Signals whether the open task was called via the open command
        /// so that the source property changing handler does not re-run the open command.
        /// </summary>
        private readonly AtomicBoolean IsOpeningViaCommand = new AtomicBoolean(false);

        #region Events

        /// <summary>
        /// Occurs when a logging message from the FFmpeg library has been received.
        /// This is shared across all instances of Media Elements.
        /// </summary>
        /// <remarks>
        /// This event is raised on a background thread.
        /// All interaction with UI elements requires calls on their corresponding dispatcher.
        /// </remarks>
        public static event EventHandler<MediaLogMessageEventArgs> FFmpegMessageLogged;

        /// <summary>
        /// Occurs when a logging message has been logged.
        /// This does not include FFmpeg messages.
        /// </summary>
        /// <remarks>
        /// This event is raised on a background thread.
        /// All interaction with UI elements requires calls on their corresponding dispatcher.
        /// </remarks>
        public event EventHandler<MediaLogMessageEventArgs> MessageLogged;

        /// <summary>
        /// Raised before the input stream of the media is initialized.
        /// Use this method to modify the input options.
        /// </summary>
        /// <remarks>
        /// This event is raised on a background thread.
        /// All interaction with UI elements requires calls on their corresponding dispatcher.
        /// </remarks>
        public event EventHandler<MediaInitializingEventArgs> MediaInitializing;

        /// <summary>
        /// Raised before the input stream of the media is opened.
        /// Use this method to modify the media options and select streams.
        /// </summary>
        /// <remarks>
        /// This event is raised on a background thread.
        /// All interaction with UI elements requires calls on their corresponding dispatcher.
        /// </remarks>
        public event EventHandler<MediaOpeningEventArgs> MediaOpening;

        /// <summary>
        /// Raised before a change in media options is applied.
        /// Use this method to modify the selected streams.
        /// </summary>
        /// <remarks>
        /// This event is raised on a background thread.
        /// All interaction with UI elements requires calls on their corresponding dispatcher.
        /// </remarks>
        public event EventHandler<MediaOpeningEventArgs> MediaChanging;

        /// <summary>
        /// Raised when a packet is read from the input stream. Useful for capturing streams.
        /// This event is not raised on the UI thread and the pointers in the event arguments
        /// are only valid for the call. If you need to keep a queue you will need to clone and
        /// release the allocated memory yourself by using clone and release methods in the native
        /// FFmpeg API.
        /// </summary>
        public event EventHandler<PacketReadEventArgs> PacketRead;

        /// <summary>
        /// Raised when an audio frame is decoded from input stream. Useful for capturing streams.
        /// This event is not raised on the UI thread and the pointers in the event arguments
        /// are only valid for the call. If you need to keep a queue you will need to clone and
        /// release the allocated memory yourself by using clone and release methods in the native
        /// FFmpeg API.
        /// </summary>
        public event EventHandler<FrameDecodedEventArgs> AudioFrameDecoded;

        /// <summary>
        /// Raised when a video frame is decoded from input stream. Useful for capturing streams.
        /// This event is not raised on the UI thread and the pointers in the event arguments
        /// are only valid for the call. If you need to keep a queue you will need to clone and
        /// release the allocated memory yourself by using clone and release methods in the native
        /// FFmpeg API.
        /// </summary>
        public event EventHandler<FrameDecodedEventArgs> VideoFrameDecoded;

        /// <summary>
        /// Raised when a subtitle is decoded from input stream. Useful for capturing streams.
        /// This event is not raised on the UI thread and the pointers in the event arguments
        /// are only valid for the call. If you need to keep a queue you will need to clone and
        /// release the allocated memory yourself by using clone and release methods in the native
        /// FFmpeg API.
        /// </summary>
        public event EventHandler<SubtitleDecodedEventArgs> SubtitleDecoded;

        /// <inheritdoc />
        public event PropertyChangedEventHandler PropertyChanged;

        #endregion

        /// <summary>
        /// Gets or sets the FFmpeg path from which to load the FFmpeg binaries.
        /// You must set this path before setting the Source property for the first time on any instance of this control.
        /// Setting this property when FFmpeg binaries have been registered will throw an exception.
        /// </summary>
        public static string FFmpegDirectory
        {
            get => MediaEngine.FFmpegDirectory;
            set => MediaEngine.FFmpegDirectory = value;
        }

        /// <summary>
        /// Specifies the bitwise flags that correspond to FFmpeg library identifiers.
        /// Please use the <see cref="FFmpeg.AutoGen.FFmpegLoadMode"/> class for valid combinations.
        /// If FFmpeg is already loaded, the value cannot be changed.
        /// </summary>
        public static int FFmpegLoadModeFlags
        {
            get => MediaEngine.FFmpegLoadModeFlags;
            set => MediaEngine.FFmpegLoadModeFlags = value;
        }

        /// <summary>
        /// Gets the FFmpeg version information. Returns null
        /// when the libraries have not been loaded.
        /// </summary>
        public static string FFmpegVersionInfo => MediaEngine.FFmpegVersionInfo;

        /// <inheritdoc />
        ILoggingHandler ILoggingSource.LoggingHandler => this;

        /// <summary>
        /// Provides access to the underlying media engine driving this control.
        /// This property is intended for advanced usages only.
        /// </summary>
        internal MediaEngine MediaCore { get; private set; }

        #region Public API

        /// <summary>
        /// Creates a viedo seek index.
        /// </summary>
        /// <param name="mediaSource">The source URL.</param>
        /// <param name="streamIndex">Index of the stream. Use -1 for automatic stream selection.</param>
        /// <returns>
        /// The seek index object
        /// </returns>
        public static VideoSeekIndex CreateVideoSeekIndex(string mediaSource, int streamIndex) =>
            MediaEngine.CreateVideoSeekIndex(mediaSource, streamIndex);

        /// <summary>
        /// Forces the pre-loading of the FFmpeg libraries according to the values of the
        /// <see cref="FFmpegDirectory"/> and <see cref="FFmpegLoadModeFlags"/>
        /// Also, sets the <see cref="FFmpegVersionInfo"/> property. Throws an exception
        /// if the libraries cannot be loaded.
        /// </summary>
        /// <returns>true if libraries were loaded, false if libraries were already loaded.</returns>
        public static bool LoadFFmpeg() => MediaEngine.LoadFFmpeg();

        /// <summary>
        /// Forces the unloading of FFmpeg libraries.
        /// </summary>
        public static void UnloadFFmpeg() => MediaEngine.UnloadFFmpeg();

        /// <summary>
        /// Requests new media options to be applied, including stream component selection.
        /// Handle the <see cref="MediaChanging"/> event to set new <see cref="MediaOptions"/> based on
        /// <see cref="MediaInfo"/> properties.
        /// </summary>
        /// <returns>The awaitable command</returns>
        public ConfiguredTaskAwaitable<bool> ChangeMedia() => Task.Run(async () =>
        {
            try { return await MediaCore.ChangeMedia(); }
            catch (Exception ex) { PostMediaFailedEvent(ex); }
            return false;
        }).ConfigureAwait(true);

        /// <summary>
        /// Begins or resumes playback of the currently loaded media.
        /// </summary>
        /// <returns>The awaitable command</returns>
        public ConfiguredTaskAwaitable<bool> Play() => Task.Run(async () =>
        {
            try { return await MediaCore.Play(); }
            catch (Exception ex) { PostMediaFailedEvent(ex); }
            return false;
        }).ConfigureAwait(true);

        /// <summary>
        /// Pauses playback of the currently loaded media.
        /// </summary>
        /// <returns>The awaitable command</returns>
        public ConfiguredTaskAwaitable<bool> Pause() => Task.Run(async () =>
        {
            try { return await MediaCore.Pause(); }
            catch (Exception ex) { PostMediaFailedEvent(ex); }
            return false;
        }).ConfigureAwait(true);

        /// <summary>
        /// Pauses and rewinds the currently loaded media.
        /// </summary>
        /// <returns>The awaitable command</returns>
        public ConfiguredTaskAwaitable<bool> Stop() => Task.Run(async () =>
        {
            try { return await MediaCore.Stop(); }
            catch (Exception ex) { PostMediaFailedEvent(ex); }
            return false;
        }).ConfigureAwait(true);

        /// <summary>
        /// Closes the currently loaded media.
        /// </summary>
        /// <returns>The awaitable command</returns>
        public ConfiguredTaskAwaitable<bool> Close() => Task.Run(async () =>
        {
            try
            {
                var result = await MediaCore.Close();
                await GuiContext.Current.InvokeAsync(() => Source = null);
                return result;
            }
            catch (Exception ex) { PostMediaFailedEvent(ex); }
            return false;
        }).ConfigureAwait(true);

        /// <summary>
        /// Seeks to the specified target position.
        /// This is an alternative to using the <see cref="Position"/> dependency property.
        /// </summary>
        /// <param name="target">The target time to seek to.</param>
        /// <returns>The awaitable command</returns>
        public ConfiguredTaskAwaitable<bool> Seek(TimeSpan target) => Task.Run(async () =>
        {
            try { return await MediaCore.Seek(target); }
            catch (Exception ex) { PostMediaFailedEvent(ex); }
            return false;
        }).ConfigureAwait(true);

        /// <summary>
        /// Seeks a single frame forward.
        /// </summary>
        /// <returns>The awaitable command</returns>
        public ConfiguredTaskAwaitable<bool> StepForward() => Task.Run(async () =>
        {
            try { return await MediaCore.StepForward(); }
            catch (Exception ex) { PostMediaFailedEvent(ex); }
            return false;
        }).ConfigureAwait(true);

        /// <summary>
        /// Seeks a single frame backward.
        /// </summary>
        /// <returns>The awaitable command</returns>
        public ConfiguredTaskAwaitable<bool> StepBackward() => Task.Run(async () =>
        {
            try { return await MediaCore.StepBackward(); }
            catch (Exception ex) { PostMediaFailedEvent(ex); }
            return false;
        }).ConfigureAwait(true);

        /// <summary>
        /// Opens the specified URI.
        /// This is an alternative method of opening media vs using the
        /// <see cref="Source"/> Dependency Property.
        /// </summary>
        /// <param name="uri">The URI.</param>
        /// <returns>The awaitable task.</returns>
        public ConfiguredTaskAwaitable<bool> Open(Uri uri) => Task.Run(async () =>
        {
            try
            {
                IsOpeningViaCommand.Value = true;
                await GuiContext.Current.InvokeAsync(() => Source = uri);
                return await MediaCore.Open(uri);
            }
            catch (Exception ex)
            {
                await GuiContext.Current.InvokeAsync(() => Source = null);
                PostMediaFailedEvent(ex);
            }
            finally
            {
                IsOpeningViaCommand.Value = false;
            }

            return false;
        }).ConfigureAwait(true);

        /// <summary>
        /// Opens the specified custom input stream.
        /// </summary>
        /// <param name="stream">The stream.</param>
        /// <returns>The awaitable task</returns>
        public ConfiguredTaskAwaitable<bool> Open(IMediaInputStream stream) => Task.Run(async () =>
        {
            try
            {
                IsOpeningViaCommand.Value = true;
                await GuiContext.Current.InvokeAsync(() => Source = stream.StreamUri);
                return await MediaCore.Open(stream);
            }
            catch (Exception ex)
            {
                await GuiContext.Current.InvokeAsync(() => Source = null);
                PostMediaFailedEvent(ex);
            }
            finally
            {
                IsOpeningViaCommand.Value = false;
            }

            return false;
        }).ConfigureAwait(true);

        #endregion

        /// <inheritdoc />
        void ILoggingHandler.HandleLogMessage(MediaLogMessage message) =>
            RaiseMessageLoggedEvent(message);
    }
}
