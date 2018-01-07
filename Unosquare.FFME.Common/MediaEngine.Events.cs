namespace Unosquare.FFME
{
    using Shared;
    using System;
    using System.Runtime.CompilerServices;

    public partial class MediaEngine
    {
        #region Event Raiser Methods

        /// <summary>
        /// Raises the MessageLogged event
        /// </summary>
        /// <param name="eventArgs">The <see cref="MediaLogMessage" /> instance containing the event data.</param>
        internal void RaiseMessageLogged(MediaLogMessage eventArgs)
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
            Log(MediaLogMessageType.Error, $"Media Failure - {ex?.GetType()}: {ex?.Message}");
            Platform.GuiInvoke(ActionPriority.DataBind, () => Connector?.OnMediaFailed(this, ex));
        }

        /// <summary>
        /// Raises the media closed event.
        /// </summary>
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        internal void RaiseMediaClosedEvent()
        {
            Platform.GuiInvoke(ActionPriority.DataBind, () => Connector?.OnMediaClosed(this));
        }

        /// <summary>
        /// Raises the media opened event.
        /// </summary>
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        internal void RaiseMediaOpenedEvent()
        {
            Platform.GuiInvoke(ActionPriority.DataBind, () => Connector?.OnMediaOpened(this));
        }

        /// <summary>
        /// Raises the media opening event.
        /// </summary>
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        internal void RaiseMediaOpeningEvent()
        {
            Platform.GuiInvoke(ActionPriority.DataBind,
                () => Connector?.OnMediaOpening(this, Container.MediaOptions, Container.MediaInfo));
        }

        /// <summary>
        /// Raises the buffering started event.
        /// </summary>
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        internal void RaiseBufferingStartedEvent()
        {
            Platform.GuiInvoke(ActionPriority.DataBind, () => Connector?.OnBufferingStarted(this));
        }

        /// <summary>
        /// Raises the buffering ended event.
        /// </summary>
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        internal void RaiseBufferingEndedEvent()
        {
            Platform.GuiInvoke(ActionPriority.DataBind, () => Connector?.OnBufferingEnded(this));
        }

        /// <summary>
        /// Raises the Seeking started event.
        /// </summary>
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        internal void RaiseSeekingStartedEvent()
        {
            Platform.GuiInvoke(ActionPriority.DataBind, () => Connector?.OnSeekingStarted(this));
        }

        /// <summary>
        /// Raises the Seeking ended event.
        /// </summary>
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        internal void RaiseSeekingEndedEvent()
        {
            Platform.GuiInvoke(ActionPriority.DataBind, () => Connector?.OnSeekingEnded(this));
        }

        /// <summary>
        /// Raises the media ended event.
        /// </summary>
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        internal void RaiseMediaEndedEvent()
        {
            Platform.GuiInvoke(ActionPriority.DataBind, () => Connector?.OnMediaEnded(this));
        }

        /// <summary>
        /// Raises the Position Changed event
        /// </summary>
        /// <param name="position">The position.</param>
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        internal void RaisePositionChangedEvent(TimeSpan position)
        {
            Platform.GuiInvoke(ActionPriority.DataBind,
                () => Connector?.OnPositionChanged(this, position));
        }

        #endregion
    }
}