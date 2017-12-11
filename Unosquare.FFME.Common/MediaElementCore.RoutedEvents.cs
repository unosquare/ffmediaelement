#pragma warning disable SA1649 // File name must match first type name
namespace Unosquare.FFME
{
    using Core;
    using System;
    using System.Runtime.CompilerServices;

    /// <summary>
    /// Represents an Exception event arguments
    /// </summary>
    /// <seealso cref="System.EventArgs" />
    public class ExceptionEventArgs : EventArgs
    {
        /// <summary>
        /// Initializes a new instance of the <see cref="ExceptionEventArgs"/> class.
        /// </summary>
        /// <param name="e">The e.</param>
        public ExceptionEventArgs(Exception e)
        {
            Exception = e;
        }

        /// <summary>
        /// Gets the exception.
        /// </summary>
        /// <value>
        /// The exception.
        /// </value>
        public Exception Exception { get; }
    }

    public partial class MediaElementCore
    {
        #region CLR Accessors

        /// <summary>
        /// Occurs when buffering of packets was started
        /// </summary>
        public event EventHandler BufferingStarted;

        /// <summary>
        /// Occurs when buffering of packets was Ended
        /// </summary>
        public event EventHandler BufferingEnded;

        /// <summary>
        /// Occurs when Seeking of packets was started
        /// </summary>
        public event EventHandler SeekingStarted;

        /// <summary>
        /// Occurs when Seeking of packets was Ended
        /// </summary>
        public event EventHandler SeekingEnded;

        /// <summary>
        /// Raised when the media fails to load or a fatal error has occurred which prevents playback.
        /// </summary>
        public event EventHandler<ExceptionEventArgs> MediaFailed;

        /// <summary>
        /// Raised when the media is opened 
        /// </summary> 
        public event EventHandler MediaOpened;

        /// <summary>
        /// Raised before the input stream of the media is opened.
        /// Use this method to modify the input options.
        /// </summary>
        public event EventHandler<MediaOpeningEventArgs> MediaOpening;

        /// <summary> 
        /// Raised when the corresponding media ends.
        /// </summary>
        public event EventHandler MediaEnded;

        #endregion

        #region Helper Methods

        /// <summary>
        /// Raises the media failed event.
        /// </summary>
        /// <param name="ex">The ex.</param>
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        internal void RaiseMediaFailedEvent(Exception ex)
        {
            LogEventStart(nameof(MediaFailed));
            Logger.Log(MediaLogMessageType.Error, $"Media Failure - {ex?.GetType()}: {ex?.Message}");
            Platform.UIInvoke(CoreDispatcherPriority.DataBind, () => MediaFailed(this, new ExceptionEventArgs(ex)));
            LogEventDone(nameof(MediaFailed));
        }

        /// <summary>
        /// Raises the media opened event.
        /// </summary>
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        internal void RaiseMediaOpenedEvent()
        {
            LogEventStart(nameof(MediaOpened));
            Platform.UIInvoke(CoreDispatcherPriority.DataBind, () => MediaOpened(this, EventArgs.Empty));
            LogEventDone(nameof(MediaOpened));
        }

        /// <summary>
        /// Raises the media opening event.
        /// </summary>
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        internal void RaiseMediaOpeningEvent()
        {
            LogEventStart(nameof(MediaOpening));
            Platform.UIInvoke(CoreDispatcherPriority.DataBind,
                () => MediaOpening(this, new MediaOpeningEventArgs(this, Container.MediaOptions, Container.MediaInfo)));

            LogEventDone(nameof(MediaOpening));
        }

        /// <summary>
        /// Logs the start of an event
        /// </summary>
        /// <param name="callerName">Name of the caller.</param>
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        private void LogEventStart(string callerName)
        {
            if (Utils.IsInDebugMode)
                Logger.Log(MediaLogMessageType.Trace, $"EVENT START: {callerName}");
        }

        /// <summary>
        /// Logs the end of an event.
        /// </summary>
        /// <param name="callerName">Name of the caller.</param>
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        private void LogEventDone(string callerName)
        {
            if (Utils.IsInDebugMode)
                Logger.Log(MediaLogMessageType.Trace, $"EVENT DONE : {callerName}");
        }

        /// <summary>
        /// Raises the buffering started event.
        /// </summary>
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        private void RaiseBufferingStartedEvent()
        {
            LogEventStart(nameof(BufferingStarted));
            Platform.UIInvoke(CoreDispatcherPriority.DataBind, () => BufferingStarted(this, EventArgs.Empty));
            LogEventDone(nameof(BufferingStarted));
        }

        /// <summary>
        /// Raises the buffering ended event.
        /// </summary>
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        private void RaiseBufferingEndedEvent()
        {
            LogEventStart(nameof(BufferingEnded));
            Platform.UIInvoke(CoreDispatcherPriority.DataBind, () => BufferingEnded(this, EventArgs.Empty));
            LogEventDone(nameof(BufferingEnded));
        }

        /// <summary>
        /// Raises the Seeking started event.
        /// </summary>
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        private void RaiseSeekingStartedEvent()
        {
            LogEventStart(nameof(SeekingStarted));
            Platform.UIInvoke(CoreDispatcherPriority.DataBind, () => SeekingStarted(this, EventArgs.Empty));
            LogEventDone(nameof(SeekingStarted));
        }

        /// <summary>
        /// Raises the Seeking ended event.
        /// </summary>
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        private void RaiseSeekingEndedEvent()
        {
            LogEventStart(nameof(SeekingEnded));
            Platform.UIInvoke(CoreDispatcherPriority.DataBind, () => SeekingEnded(this, EventArgs.Empty));
            LogEventDone(nameof(SeekingEnded));
        }

        /// <summary>
        /// Raises the media ended event.
        /// </summary>
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        private void RaiseMediaEndedEvent()
        {
            LogEventStart(nameof(MediaEnded));
            Platform.UIInvoke(CoreDispatcherPriority.DataBind, () => MediaEnded(this, EventArgs.Empty));
            LogEventDone(nameof(MediaEnded));
        }

        #endregion
    }
}
#pragma warning restore SA1649 // File name must match first type name