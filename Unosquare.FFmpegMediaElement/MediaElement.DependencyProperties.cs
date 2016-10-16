namespace Unosquare.FFmpegMediaElement
{
    using System;
    using System.Windows;
    using System.Windows.Controls;
    using System.Windows.Media;
    using System.Windows.Threading;

    partial class MediaElement
    {
        /// <summary>
        /// Defines constants for Property Names that are used to
        /// either register dependency properties or notify property changes.
        /// </summary>
        private static class PropertyNames
        {
            public const string Source = "Source";
            public const string Stretch = "Stretch";
            public const string StretchDirection = "StretchDirection";
            public const string Volume = "Volume";
            public const string Balance = "Balance";
            public const string IsMuted = "IsMuted";
            public const string ScrubbingEnabled = "ScrubbingEnabled";
            public const string UnloadedBehavior = "UnloadedBehavior";
            public const string LoadedBehavior = "LoadedBehavior";
            public const string IsPlaying = "IsPlaying";
            public const string HasMediaEnded = "HasMediaEnded";
            public const string Position = "Position";
            public const string SpeedRatio = "SpeedRatio";
            public const string UserAgent = "UserAgent";
            public const string Referer = "Referer";
        }

        #region Source

        /// <summary>
        /// DependencyProperty for FFmpegMediaElement Source property. 
        /// </summary>
        /// <seealso cref="MediaElement.Source"> 
        /// This property is cached (_source). 
        public static readonly DependencyProperty SourceProperty = DependencyProperty.Register(
            PropertyNames.Source, typeof(Uri), typeof(MediaElement), new FrameworkPropertyMetadata(
                null, FrameworkPropertyMetadataOptions.AffectsMeasure | FrameworkPropertyMetadataOptions.AffectsRender,
                OnSourcePropertyChanged, OnSourcePropertyCoerce));

        /// <summary>
        /// Called when [source property changed].
        /// </summary>
        /// <param name="dependencyObject">The dependency object.</param>
        /// <param name="e">The <see cref="DependencyPropertyChangedEventArgs"/> instance containing the event data.</param>
        private static void OnSourcePropertyChanged(DependencyObject dependencyObject, DependencyPropertyChangedEventArgs e)
        {
            //var element = dependencyObject as MediaElement;
            //if (element == null) return;

            //var uri = e.NewValue as Uri;
            //if (uri != null)
            //    element.OpenMedia(uri);
            //else
            //    element.CloseMedia(true);

            // PLACEHOLDER
        }

        private static object OnSourcePropertyCoerce(DependencyObject dependencyObject, object baseValue)
        {
            var element = dependencyObject as MediaElement;
            if (element == null) return null;

            var uri = baseValue as Uri;
            if (uri != null)
            {
                element.CloseMedia(true);
                element.OpenMedia(uri, element.Referer, element.UserAgent);
            }
            else
            {
                element.CloseMedia(true);
            }

            return uri;
        }

        /// <summary>
        /// Gets/Sets the Source on this MediaElement. 
        /// The Source property is the Uri of the media to be played.
        /// Note: Currently, this does not support the pack protocol (pack://)
        /// </summary> 
        public Uri Source
        {
            get { return GetValue(SourceProperty) as Uri; }
            set { SetValue(SourceProperty, value); }
        }

        #endregion

        #region Stretch

        /// <summary>
        /// DependencyProperty for Stretch property. 
        /// </summary> 
        public static readonly DependencyProperty StretchProperty = DependencyProperty.Register(
            PropertyNames.Stretch, typeof(Stretch), typeof(MediaElement), new FrameworkPropertyMetadata(
                Stretch.Uniform, FrameworkPropertyMetadataOptions.AffectsMeasure | FrameworkPropertyMetadataOptions.AffectsRender,
                OnStretchPropertyChanged));

        /// <summary>
        /// Called when [stretch property changed].
        /// </summary>
        /// <param name="dependencyObject">The dependency object.</param>
        /// <param name="e">The <see cref="DependencyPropertyChangedEventArgs"/> instance containing the event data.</param>
        private static void OnStretchPropertyChanged(DependencyObject dependencyObject, DependencyPropertyChangedEventArgs e)
        {
            var element = dependencyObject as MediaElement;
            if (element == null) return;

            element.ViewBox.Stretch = (Stretch)e.NewValue;
        }

        /// <summary>
        /// Gets/Sets the Stretch on this MediaElement.
        /// The Stretch property determines how large the MediaElement will be drawn. 
        /// </summary>
        public Stretch Stretch
        {
            get { return (Stretch)GetValue(StretchProperty); }
            set { SetValue(StretchProperty, value); }
        }

        #endregion

        #region StretchDirection

        /// <summary> 
        /// DependencyProperty for StretchDirection property.
        /// </summary> 
        public static readonly DependencyProperty StretchDirectionProperty = DependencyProperty.Register(
            PropertyNames.StretchDirection, typeof(StretchDirection), typeof(MediaElement), new FrameworkPropertyMetadata(
                StretchDirection.Both, FrameworkPropertyMetadataOptions.AffectsMeasure | FrameworkPropertyMetadataOptions.AffectsRender,
                OnStretchDirectionPropertyChanged));

        /// <summary>
        /// Called when [stretch direction property changed].
        /// </summary>
        /// <param name="dependencyObject">The dependency object.</param>
        /// <param name="e">The <see cref="DependencyPropertyChangedEventArgs"/> instance containing the event data.</param>
        private static void OnStretchDirectionPropertyChanged(DependencyObject dependencyObject, DependencyPropertyChangedEventArgs e)
        {
            var element = dependencyObject as MediaElement;
            if (element == null) return;

            element.ViewBox.StretchDirection = (StretchDirection)e.NewValue;
        }

        /// <summary> 
        /// Gets/Sets the stretch direction of the Viewbox, which determines the restrictions on
        /// scaling that are applied to the content inside the Viewbox.  For instance, this property 
        /// can be used to prevent the content from being smaller than its native size or larger than 
        /// its native size.
        /// </summary> 
        public StretchDirection StretchDirection
        {
            get { return (StretchDirection)GetValue(StretchDirectionProperty); }
            set { SetValue(StretchDirectionProperty, value); }
        }

        #endregion

        #region Volume

        /// <summary> 
        ///     The DependencyProperty for the MediaElement.Volume property.
        /// </summary>
        public static readonly DependencyProperty VolumeProperty
            = DependencyProperty.Register(
                        PropertyNames.Volume,
                        typeof(double),
                        typeof(MediaElement),
                        new FrameworkPropertyMetadata(
                              0.5,
                              FrameworkPropertyMetadataOptions.None,
                              new PropertyChangedCallback(VolumePropertyChanged)));

        private static void VolumePropertyChanged(DependencyObject d, DependencyPropertyChangedEventArgs e)
        {
            var element = d as MediaElement;
            if (element == null) return;

            if (element.Media != null)
                element.Media.Volume = Convert.ToDecimal((double)e.NewValue);
        }


        /// <summary>
        /// Gets/Sets the Volume property on the MediaElement.
        /// Note: Valid values are from 0 to 1
        /// </summary>
        public double Volume
        {
            get
            {
                return (double)GetValue(VolumeProperty);
            }
            set
            {
                SetValue(VolumeProperty, value);
            }
        }


        #endregion

        #region Balance

        /// <summary>
        ///     The DependencyProperty for the MediaElement.Balance property. 
        /// </summary>
        public static readonly DependencyProperty BalanceProperty
            = DependencyProperty.Register(
                        PropertyNames.Balance,
                        typeof(double),
                        typeof(MediaElement),
                        new FrameworkPropertyMetadata(
                              0.0,
                              FrameworkPropertyMetadataOptions.None,
                              new PropertyChangedCallback(BalancePropertyChanged),
                              new CoerceValueCallback(CoerceBalanceProperty)));

        private static void BalancePropertyChanged(DependencyObject d, DependencyPropertyChangedEventArgs e)
        {
            var element = d as MediaElement;
            if (element == null) return;
        }

        public static object CoerceBalanceProperty(DependencyObject d, object value)
        {
            return 0.0d;
        }

        /// <summary>
        /// Gets/Sets the Balance property on the MediaElement. 
        /// Note: Balance changes are not yet supported. Value will always be 0;
        /// </summary> 
        public double Balance
        {
            get
            {
                return (double)GetValue(BalanceProperty);
            }
            set
            {
                SetValue(BalanceProperty, value);
            }
        }


        #endregion

        #region ScrubbingEnabled

        /// <summary>
        /// The DependencyProperty for the MediaElement.ScrubbingEnabled property.
        /// </summary> 
        public static readonly DependencyProperty ScrubbingEnabledProperty
            = DependencyProperty.Register(
                        PropertyNames.ScrubbingEnabled,
                        typeof(bool),
                        typeof(MediaElement),
                        new FrameworkPropertyMetadata(
                            false,
                            FrameworkPropertyMetadataOptions.None,
                            new PropertyChangedCallback(ScrubbingEnabledPropertyChanged)));

        private static void ScrubbingEnabledPropertyChanged(DependencyObject d, DependencyPropertyChangedEventArgs e)
        {
            var element = d as MediaElement;
            if (element == null) return;
        }

        /// <summary>
        /// Gets/Sets the ScrubbingEnabled property on the MediaElement.
        /// Note: Frame scrubbing is always enabled. The real effect of this property is
        /// that when it is set to true, setting values on the Position property occurs synchronously.
        /// Wehn it is set to false, setting values on the Position property occurs asyncrhonously
        /// </summary>
        public bool ScrubbingEnabled
        {
            get { return (bool)GetValue(ScrubbingEnabledProperty); }
            set { SetValue(ScrubbingEnabledProperty, value); }
        }

        #endregion

        #region UnloadedBehavior

        /// <summary> 
        /// The DependencyProperty for the MediaElement.UnloadedBehavior property. 
        /// </summary>
        public static readonly DependencyProperty UnloadedBehaviorProperty
            = DependencyProperty.Register(
                        PropertyNames.UnloadedBehavior,
                        typeof(MediaState),
                        typeof(MediaElement),
                        new FrameworkPropertyMetadata(
                            MediaState.Close,
                            FrameworkPropertyMetadataOptions.None,
                            new PropertyChangedCallback(UnloadedBehaviorPropertyChanged)));

        private static void UnloadedBehaviorPropertyChanged(DependencyObject d, DependencyPropertyChangedEventArgs e)
        {
            var element = d as MediaElement;
            if (element == null) return;
        }

        /// <summary>
        /// Specifies how the underlying media should behave when the given 
        /// MediaElement is unloaded, the default behavior is to Close the 
        /// media.
        /// </summary> 
        public MediaState UnloadedBehavior
        {
            get
            {
                return (MediaState)GetValue(UnloadedBehaviorProperty);
            }

            set
            {
                SetValue(UnloadedBehaviorProperty, value);
            }
        }

        #endregion

        #region LoadedBehavior

        /// <summary>
        /// The DependencyProperty for the MediaElement.LoadedBehavior property.
        /// </summary>
        public static readonly DependencyProperty LoadedBehaviorProperty
            = DependencyProperty.Register(
                        PropertyNames.LoadedBehavior,
                        typeof(MediaState),
                        typeof(MediaElement),
                        new FrameworkPropertyMetadata(
                            MediaState.Play,
                            FrameworkPropertyMetadataOptions.None,
                            new PropertyChangedCallback(LoadedBehaviorPropertyChanged)));


        private static void LoadedBehaviorPropertyChanged(DependencyObject d, DependencyPropertyChangedEventArgs e)
        {
            var element = d as MediaElement;
            if (element == null) return;
        }

        /// <summary>
        /// Specifies the behavior that the media element should have when it 
        /// is loaded. The default behavior is that it is under manual control 
        /// (i.e. the caller should call methods such as Play in order to play
        /// the media). If a source is set, then the default behavior changes to 
        /// to be playing the media. If a source is set and a loaded behavior is
        /// also set, then the loaded behavior takes control.
        /// </summary>
        public MediaState LoadedBehavior
        {
            get
            {
                return (MediaState)GetValue(LoadedBehaviorProperty);
            }

            set
            {
                SetValue(LoadedBehaviorProperty, value);
            }
        }

        #endregion

        #region IsMuted

        /// <summary> 
        /// The DependencyProperty for the MediaElement.IsMuted property.
        /// </summary> 
        public static readonly DependencyProperty IsMutedProperty
            = DependencyProperty.Register(
                        PropertyNames.IsMuted,
                        typeof(bool),
                        typeof(MediaElement),
                        new FrameworkPropertyMetadata(
                            false,
                            FrameworkPropertyMetadataOptions.None,
                            new PropertyChangedCallback(IsMutedPropertyChanged)));

        /// <summary>
        /// Gets/Sets the IsMuted property on the MediaElement.
        /// Note: Muting Sets the Volume to 0; Unmuting set the volume to what
        /// it originally was before muting.
        /// </summary> 
        public bool IsMuted
        {
            get
            {
                return (bool)GetValue(IsMutedProperty);
            }
            set
            {
                SetValue(IsMutedProperty, value);
            }
        }

        private double VolumeBeforeMuting = 0;
        private static void IsMutedPropertyChanged(DependencyObject d, DependencyPropertyChangedEventArgs e)
        {
            var element = d as MediaElement;
            if (element == null) return;
            if (element.Media == null) return;

            var newValue = (bool)e.NewValue;
            var oldValue = (bool)e.OldValue;
            if (newValue)
            {
                element.VolumeBeforeMuting = element.Volume;
                element.Volume = 0d;
            }
            else
            {
                if (element.VolumeBeforeMuting > 0.0d)
                    element.Volume = element.VolumeBeforeMuting;
            }
        }

        #endregion

        #region Position

        private bool PositionUpdatingFromMediaDone = true;
        private DispatcherTimer SeekPositionUpdateTimer = null;
        private decimal SeekRequestedPosition = decimal.MinValue;
        private DateTime SeekRequestedPositionTime = DateTime.MinValue;
        private readonly object PositionSyncLock = new object();

        /// <summary>
        ///     The DependencyProperty for the MediaElement.Position property. 
        /// </summary>
        public static readonly DependencyProperty PositionProperty
            = DependencyProperty.Register(
                        PropertyNames.Position,
                        typeof(decimal),
                        typeof(MediaElement),
                        new FrameworkPropertyMetadata(
                              0.0M,
                              FrameworkPropertyMetadataOptions.AffectsMeasure | FrameworkPropertyMetadataOptions.AffectsRender,
                              new PropertyChangedCallback(PositionPropertyChanged),
                              new CoerceValueCallback(CoercePositionProperty)));

        /// <summary>
        /// Callback when the seek position timer ticks.
        /// </summary>
        /// <param name="sender">The sender.</param>
        /// <param name="e">The <see cref="EventArgs"/> instance containing the event data.</param>
        private void SeekPositionUpdateTimerTick(object sender, EventArgs e)
        {
            if (ScrubbingEnabled) return; // when scrubbing is enabled, the position clock is not really of any use
            if (Media == null) return;
            if (Media.IsLiveStream) return;
            if (PositionUpdatingFromMediaDone == false) return;
            if (SeekRequestedPosition == decimal.MinValue) return;

            // We dont' want to wait for timeouts if we have the frame readily available
            if (Media.QueryIsFrameAvailable(SeekRequestedPosition) == false)
            {
                // If we have determined scrubbing is not enabled, then we want to check for a timeout
                // if a new position was requested too recently, just ignore it. We won't seek immediately
                var elapsedSinceLastRequest = DateTime.UtcNow.Subtract(SeekRequestedPositionTime).TotalMilliseconds;
                if (elapsedSinceLastRequest < Constants.SeekPositionUpdateTimeoutMillis)
                    return;
            }

            // Once here, we will perform the seek and coerce the seek value, ensuring 
            // the updates only come from within this operation and not the Media.Position
            // property change notification
            lock (PositionSyncLock)
            {
                PositionUpdatingFromMediaDone = false;
                this.Media.Seek(SeekRequestedPosition);
                this.Position = this.Media.Position - this.Media.StartTime;
                SeekRequestedPosition = decimal.MinValue;
                SeekRequestedPositionTime = DateTime.MinValue;
            }
        }

        private static void PositionPropertyChanged(DependencyObject d, DependencyPropertyChangedEventArgs e)
        {
            var element = d as MediaElement;
            if (element == null) return;
            if (element.Media == null) return;

            // If we are not done setting the position from media, don't allow a change via seeking.
            if (element.PositionUpdatingFromMediaDone == false || element.Media.IsLiveStream) return;

            // At this point we can be certain that the position property is being changed by driving
            // the value and not becuase a Media.Position property change notification
            lock (element.PositionSyncLock)
            {
                var targetPosition = element.Media.StartTime + (decimal)e.NewValue;
                if (element.ScrubbingEnabled)
                {
                    // synchronous behavior
                    element.PositionUpdatingFromMediaDone = false;
                    element.Media.Seek(targetPosition);
                    element.SeekRequestedPositionTime = DateTime.MinValue;
                    element.SeekRequestedPosition = decimal.MinValue;
                }
                else
                {
                    // asynchronous behavior
                    element.Media.Pause();
                    element.SeekRequestedPosition = targetPosition;
                    element.SeekRequestedPositionTime = DateTime.UtcNow;
                }

            }
        }

        public static object CoercePositionProperty(DependencyObject d, object value)
        {
            var element = d as MediaElement;
            if (element == null) return 0.0M;
            if (element.Media == null) return 0.0M;
            if (element.Media.IsLiveStream) return element.Media.Position;

            return (decimal)value;
        }

        /// <summary>
        /// Gets/Sets the Position property on the MediaElement. 
        /// </summary> 
        public decimal Position
        {
            get
            {
                return (decimal)GetValue(PositionProperty);
            }
            set
            {
                SetValue(PositionProperty, value);
            }
        }

        #endregion

        #region SpeedRatio

        /// <summary>
        ///     The DependencyProperty for the MediaElement.SpeedRatio property. 
        /// </summary>
        public static readonly DependencyProperty SpeedRatioProperty
            = DependencyProperty.Register(
                        PropertyNames.SpeedRatio,
                        typeof(decimal),
                        typeof(MediaElement),
                        new FrameworkPropertyMetadata(
                              Constants.DefaultSpeedRatio,
                              FrameworkPropertyMetadataOptions.AffectsMeasure | FrameworkPropertyMetadataOptions.AffectsRender,
                              new PropertyChangedCallback(SpeedRatioPropertyChanged),
                              new CoerceValueCallback(CoerceSpeedRatioProperty)));

        private static void SpeedRatioPropertyChanged(DependencyObject d, DependencyPropertyChangedEventArgs e)
        {
            var element = d as MediaElement;
            if (element == null) return;
            if (element.Media == null) return;

            var targetSpeedRatio = (decimal)e.NewValue;
            element.Media.SpeedRatio = targetSpeedRatio;
        }

        public static object CoerceSpeedRatioProperty(DependencyObject d, object value)
        {
            var element = d as MediaElement;
            if (element == null) return Constants.DefaultSpeedRatio;
            if (element.Media == null) return Constants.DefaultSpeedRatio;
            if (element.Media.IsLiveStream) return Constants.DefaultSpeedRatio;
            return value;
        }

        /// <summary>
        /// Gets/Sets the SpeedRatio property on the MediaElement. 
        /// </summary> 
        public decimal SpeedRatio
        {
            get
            {
                return (decimal)GetValue(SpeedRatioProperty);
            }
            set
            {
                SetValue(SpeedRatioProperty, value);
            }
        }

        #endregion

        #region UserAgent

        /// <summary>
        /// Dependency property for the user agent property
        /// </summary>
        public static readonly DependencyProperty UserAgentProperty = DependencyProperty.Register(
            PropertyNames.UserAgent, typeof(string), typeof(MediaElement), new FrameworkPropertyMetadata(
                null, FrameworkPropertyMetadataOptions.AffectsMeasure | FrameworkPropertyMetadataOptions.AffectsRender));

        /// <summary>
        /// Gets or sets the user agent. Property takes effect only upon loading
        /// live streams such as m3u8 files.
        /// </summary>
        /// <value>
        /// The user agent.
        /// </value>
        public string UserAgent
        {
            get { return GetValue(UserAgentProperty) as string; }
            set
            {
                if (Media != null)
                    throw new InvalidOperationException("The UserAgent property can only be set before loading a media stream.");
                SetValue(UserAgentProperty, value);
            }
        }

        #endregion

        #region Referer

        /// <summary>
        /// Dependency property for the Referer property
        /// </summary>
        public static readonly DependencyProperty RefererProperty = DependencyProperty.Register(
            PropertyNames.Referer, typeof(string), typeof(MediaElement), new FrameworkPropertyMetadata(
                null, FrameworkPropertyMetadataOptions.AffectsMeasure | FrameworkPropertyMetadataOptions.AffectsRender));

        /// <summary>
        /// Gets or sets the referer. Property takes effect only upon loading
        /// live streams such as m3u8 files.
        /// </summary>
        /// <value>
        /// The user agent.
        /// </value>
        public string Referer
        {
            get { return GetValue(RefererProperty) as string; }
            set
            {
                if (Media != null)
                    throw new InvalidOperationException("The Referer property can only be set before loading a media stream.");
                SetValue(RefererProperty, value);
            }
        }

        #endregion
    }
}
