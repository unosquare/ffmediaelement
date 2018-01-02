namespace Unosquare.FFME
{
    using Core;
    using System;
    using System.ComponentModel;
    using System.Runtime.CompilerServices;

    public partial class MediaElementCore
    {
        #region Event Raiser Methods

        /// <summary>
        /// Raises the MessageLogged event
        /// </summary>
        /// <param name="eventArgs">The <see cref="MediaLogMessagEventArgs" /> instance containing the event data.</param>
        internal void RaiseMessageLogged(MediaLogMessagEventArgs eventArgs)
        {
            Connector?.OnMessageLogged(this, eventArgs);
        }

        /// <summary>
        /// Raises the media failed event.
        /// </summary>
        /// <param name="ex">The ex.</param>
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        internal void RaiseMediaFailedEvent(Exception ex)
        {
            Logger.Log(MediaLogMessageType.Error, $"Media Failure - {ex?.GetType()}: {ex?.Message}");
            Platform.UIInvoke(CoreDispatcherPriority.DataBind, () => Connector?.OnMediaFailed(this, new ExceptionEventArgs(ex)));
        }

        /// <summary>
        /// Raises the media closed event.
        /// </summary>
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        internal void RaiseMediaClosedEvent()
        {
            Platform.UIInvoke(CoreDispatcherPriority.DataBind, () => Connector?.OnMediaClosed(this, EventArgs.Empty));
        }

        /// <summary>
        /// Raises the media opened event.
        /// </summary>
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        internal void RaiseMediaOpenedEvent()
        {
            Platform.UIInvoke(CoreDispatcherPriority.DataBind, () => Connector?.OnMediaOpened(this, EventArgs.Empty));
        }

        /// <summary>
        /// Raises the media opening event.
        /// </summary>
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        internal void RaiseMediaOpeningEvent()
        {
            Platform.UIInvoke(CoreDispatcherPriority.DataBind,
                () => Connector?.OnMediaOpening(this, new MediaOpeningEventArgs(this, Container.MediaOptions, Container.MediaInfo)));
        }

        /// <summary>
        /// Raises the buffering started event.
        /// </summary>
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        private void RaiseBufferingStartedEvent()
        {
            Platform.UIInvoke(CoreDispatcherPriority.DataBind, () => Connector?.OnBufferingStarted(this, EventArgs.Empty));
        }

        /// <summary>
        /// Raises the buffering ended event.
        /// </summary>
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        private void RaiseBufferingEndedEvent()
        {
            Platform.UIInvoke(CoreDispatcherPriority.DataBind, () => Connector?.OnBufferingEnded(this, EventArgs.Empty));
        }

        /// <summary>
        /// Raises the Seeking started event.
        /// </summary>
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        private void RaiseSeekingStartedEvent()
        {
            Platform.UIInvoke(CoreDispatcherPriority.DataBind, () => Connector?.OnSeekingStarted(this, EventArgs.Empty));
        }

        /// <summary>
        /// Raises the Seeking ended event.
        /// </summary>
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        private void RaiseSeekingEndedEvent()
        {
            Platform.UIInvoke(CoreDispatcherPriority.DataBind, () => Connector?.OnSeekingEnded(this, EventArgs.Empty));
        }

        /// <summary>
        /// Raises the media ended event.
        /// </summary>
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        private void RaiseMediaEndedEvent()
        {
            Platform.UIInvoke(CoreDispatcherPriority.DataBind, () => Connector?.OnMediaEnded(this, EventArgs.Empty));
        }

        /// <summary>
        /// Raises the Position Changed event
        /// </summary>
        /// <param name="position">The position.</param>
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        private void RaisePositionChangedEvent(TimeSpan position)
        {
            Platform.UIInvoke(CoreDispatcherPriority.DataBind,
                () => Connector?.OnPositionChanged(this, new PositionChangedEventArgs(this, position)));
        }

        #endregion
    }
}