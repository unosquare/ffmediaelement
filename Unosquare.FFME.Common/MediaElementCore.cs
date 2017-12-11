namespace Unosquare.FFME
{
    using Commands;
    using Core;
    using System;
    using System.ComponentModel;
    using System.Runtime.CompilerServices;
    using System.Threading.Tasks;

    /// <summary>
    /// Represents a control that contains audio and/or video.
    /// In contrast with System.Windows.Controls.MediaElement, this version uses
    /// the FFmpeg library to perform reading and decoding of media streams.
    /// </summary>
    /// <seealso cref="System.IDisposable" />
    /// <seealso cref="System.ComponentModel.INotifyPropertyChanged" />
    public partial class MediaElementCore : IDisposable, INotifyPropertyChanged
    {
        #region Fields and Property Backing
#pragma warning disable SA1401 // Fields must be private
        internal static AtomicBoolean IsFFmpegLoaded = new AtomicBoolean();

        /// <summary>
        /// The logger
        /// </summary>
        internal readonly IMediaLogger Logger;

        /// <summary>
        /// To detect redundant calls
        /// </summary>
        internal bool IsDisposed = false;

        /// <summary>
        /// The ffmpeg directory
        /// </summary>
        private static string m_FFmpegDirectory = null;

        /// <summary>
        /// The position update timer
        /// </summary>
        private IDispatcherTimer UIPropertyUpdateTimer = null;

        /// <summary>
        /// When position is being set from within this control, this field will
        /// be set to true. This is useful to detect if the user is setting the position
        /// or if the Position property is being driven from within
        /// </summary>
        private AtomicBoolean m_IsPositionUpdating = new AtomicBoolean();

        /// <summary>
        /// Flag when disposing process start but not finished yet
        /// </summary>
        private AtomicBoolean m_IsDisposing = new AtomicBoolean();

#pragma warning restore SA1401 // Fields must be private
        #endregion

        #region Constructors

        /// <summary>
        /// Initializes a new instance of the <see cref="MediaElementCore"/> class.
        /// </summary>
        /// <param name="parent">The parent.</param>
        /// <param name="isInDesignTime">if set to <c>true</c> [is in design time].</param>
        public MediaElementCore(object parent, bool isInDesignTime)
        {
            Parent = parent;
            Logger = new GenericMediaLogger<MediaElementCore>(this);
            Commands = new MediaCommandManager(this);

            if (!isInDesignTime)
            {
                // The UI Property update timer is responsible for timely updates to properties outside of the worker threads
                // We use the loaded priority because it is the priority right below the Render one.
                UIPropertyUpdateTimer = Platform.CreateTimer(CoreDispatcherPriority.Loaded);
                UIPropertyUpdateTimer.Interval = Constants.UIPropertyUpdateInterval;
                UIPropertyUpdateTimer.IsEnabled = true;

                // The tick callback performs the updates
                UIPropertyUpdateTimer.Tick += (s, e) =>
                {
                    UpdatePosition(IsOpen ? Clock?.Position ?? TimeSpan.Zero : TimeSpan.Zero);

                    if (HasMediaEnded == false && CanReadMorePackets && (IsOpening || IsOpen))
                    {
                        var bufferedLength = Container?.Components?.PacketBufferLength ?? 0d;
                        BufferingProgress = Math.Min(1d, bufferedLength / BufferCacheLength);
                        var oldIsBugffering = IsBuffering;
                        var newIsBuffering = bufferedLength < BufferCacheLength;

                        if (oldIsBugffering == false && newIsBuffering)
                            RaiseBufferingStartedEvent();
                        else if (oldIsBugffering && newIsBuffering == false)
                            RaiseBufferingEndedEvent();

                        IsBuffering = HasMediaEnded == false && newIsBuffering;
                    }
                    else
                    {
                        BufferingProgress = 0;
                        IsBuffering = false;
                    }

                    var downloadProgress = Math.Min(1d, Math.Round((Container?.Components.PacketBufferLength ?? 0d) / DownloadCacheLength, 3));
                    if (double.IsNaN(downloadProgress)) downloadProgress = 0;
                    DownloadProgress = downloadProgress;
                };

                // Go ahead and fire up the continuous updates
                UIPropertyUpdateTimer.Start();
            }
        }

        #endregion

        #region Events

        /// <summary>
        /// Occurs when a logging message from the FFmpeg library has been received.
        /// This is shared across all instances of Media Elements
        /// </summary>
        public static event EventHandler<MediaLogMessagEventArgs> FFmpegMessageLogged;

        /// <summary>
        /// Multicast event for property change notifications.
        /// </summary>
        public event PropertyChangedEventHandler PropertyChanged;

        /// <summary>
        /// Occurs when a logging message has been logged.
        /// This does not include FFmpeg messages.
        /// </summary>
        public event EventHandler<MediaLogMessagEventArgs> MessageLogged;

        #endregion

        #region Properties

        /// <summary>
        /// Gets or sets the FFmpeg path from which to load the FFmpeg binaries.
        /// You must set this path before setting the Source property for the first time on any instance of this control.
        /// Settng this property when FFmpeg binaries have been registered will throw an exception.
        /// </summary>
        public static string FFmpegDirectory
        {
            get => m_FFmpegDirectory;
            set
            {
                if (IsFFmpegLoaded.Value == false)
                {
                    m_FFmpegDirectory = value;
                    return;
                }

                if ((value?.Equals(m_FFmpegDirectory) ?? false) == false)
                    throw new InvalidOperationException($"Unable to set a new FFmpeg registration path: {value}. FFmpeg binaries have already been registered.");
            }
        }

        /// <summary>
        /// Gets the parent control (platform specific).
        /// </summary>
        public object Parent { get; }

        /// <summary>
        /// When position is being set from within this control, this field will
        /// be set to true. This is useful to detect if the user is setting the position
        /// or if the Position property is being driven from within
        /// </summary>
        internal bool IsPositionUpdating
        {
            get => m_IsPositionUpdating.Value;
            set => m_IsPositionUpdating.Value = value;
        }

        #endregion

        #region Methods

        /// <summary>
        /// Performs application-defined tasks associated with freeing, releasing, or resetting unmanaged resources.
        /// </summary>
        public void Dispose()
        {
            Dispose(true);
        }

        /// <summary>
        /// Raises the FFmpegMessageLogged event
        /// </summary>
        /// <param name="eventArgs">The <see cref="MediaLogMessagEventArgs" /> instance containing the event data.</param>
        internal static void RaiseFFmpegMessageLogged(MediaLogMessagEventArgs eventArgs)
        {
            FFmpegMessageLogged?.Invoke(typeof(MediaElementCore), eventArgs);
        }

        internal async Task Open(Uri uri)
        {
            Source = uri;

            // TODO: Calling this multiple times while an operation is in progress breaks the control :(
            // for now let's throw an exception but ideally we want the user NOT to be able to change the value in the first place.
            if (IsOpening)
                throw new InvalidOperationException($"Unable to change {nameof(Source)} to '{uri}' because {nameof(IsOpening)} is currently set to true.");

            if (uri != null)
            {
                await Commands.Close();
                await Commands.Open(uri);
                if (LoadedBehavior == CoreMediaState.Play || CanPause == false)
                    Commands.Play();
            }
            else
            {
                await Commands.Close();
            }
        }

        /// <summary>
        /// Updates the position property signaling the update is
        /// coming internally. This is to distinguish between user/binding 
        /// written value to the Position Porperty and value set by this control's
        /// internal clock.
        /// </summary>
        /// <param name="value">The current position.</param>
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        internal void UpdatePosition(TimeSpan value)
        {
            if (IsPositionUpdating || IsSeeking)
                return;

            IsPositionUpdating = true;
            Platform.UIEnqueueInvoke(
                CoreDispatcherPriority.DataBind,
                (Action<TimeSpan>)((v) =>
                {
                    if (Position != v)
                        Position = v;

                    IsPositionUpdating = false;
                }),
                new object[] { value });
        }

        #endregion

        #region Logging Events

        /// <summary>
        /// Raises the MessageLogged event
        /// </summary>
        /// <param name="eventArgs">The <see cref="MediaLogMessagEventArgs" /> instance containing the event data.</param>
        internal void RaiseMessageLogged(MediaLogMessagEventArgs eventArgs)
        {
            MessageLogged?.Invoke(this, eventArgs);
        }

        #endregion

        #region INotifyPropertyChanged Implementation

        /// <summary>
        /// Checks if a property already matches a desired value.  Sets the property and
        /// notifies listeners only when necessary.
        /// </summary>
        /// <typeparam name="T">Type of the property.</typeparam>
        /// <param name="storage">Reference to a property with both getter and setter.</param>
        /// <param name="value">Desired value for the property.</param>
        /// <param name="propertyName">Name of the property used to notify listeners.  This
        /// value is optional and can be provided automatically when invoked from compilers that
        /// support CallerMemberName.</param>
        /// <returns>True if the value was changed, false if the existing value matched the
        /// desired value.</returns>
        private bool SetProperty<T>(ref T storage, T value, [CallerMemberName] string propertyName = null)
        {
            if (Equals(storage, value))
                return false;

            storage = value;
            OnPropertyChanged(propertyName);
            return true;
        }

        /// <summary>
        /// Notifies listeners that a property value has changed.
        /// </summary>
        /// <param name="propertyName">Name of the property used to notify listeners.  This
        /// value is optional and can be provided automatically when invoked from compilers
        /// that support <see cref="CallerMemberNameAttribute"/>.</param>
        private void OnPropertyChanged([CallerMemberName] string propertyName = null)
        {
            if (PropertyChanged == null) return;
            Platform.UIInvoke(CoreDispatcherPriority.DataBind, () =>
            {
                PropertyChanged(this, new PropertyChangedEventArgs(propertyName));
            });
        }

        #endregion

        #region IDisposable Implementation

        /// <summary>
        /// Releases unmanaged and - optionally - managed resources.
        /// </summary>
        /// <param name="alsoManaged"><c>true</c> to release both managed and unmanaged resources; <c>false</c> to release only unmanaged resources.</param>
        private void Dispose(bool alsoManaged)
        {
            if (IsDisposed) return;

            if (alsoManaged)
            {
                m_IsDisposing.Value = true;

                // free managed resources
                Commands.Close().Wait();

                if (Container != null)
                {
                    Container.Dispose();
                    Container = null;
                }

                if (UIPropertyUpdateTimer != null)
                {
                    UIPropertyUpdateTimer.Stop();
                    UIPropertyUpdateTimer.IsEnabled = false;
                    UIPropertyUpdateTimer = null;
                }

                m_PacketReadingCycle.Dispose();
                m_FrameDecodingCycle.Dispose();
                m_BlockRenderingCycle.Dispose();
                m_SeekingDone.Dispose();
            }

            IsDisposed = true;
        }

        #endregion
    }
}
