﻿namespace Unosquare.FFME.Platform
{
    using Rendering;
    using Shared;
    using System;
    using System.Diagnostics;
    using System.Windows.Threading;

    /// <summary>
    /// Root for platform-specific implementations
    /// </summary>
    /// <seealso cref="Unosquare.FFME.Shared.IPlatform" />
    internal class WindowsPlatform : IPlatform
    {
        /// <summary>
        /// Initializes static members of the <see cref="WindowsPlatform"/> class.
        /// </summary>
        static WindowsPlatform()
        {
            Instance = new WindowsPlatform();
        }

        /// <summary>
        /// Prevents a default instance of the <see cref="WindowsPlatform"/> class from being created.
        /// </summary>
        /// <exception cref="InvalidOperationException">Unable to get a valid GUI context.</exception>
        private WindowsPlatform()
        {
            NativeMethods = WindowsNativeMethods.Instance;

            if (WpfGraphicalContext.Current.IsValid)
                Gui = WpfGraphicalContext.Current;
            else if (WinFormsGraphicalContext.Current.IsValid)
                Gui = WinFormsGraphicalContext.Current;
            else
                throw new InvalidOperationException("Unable to get a valid GUI context.");

            IsInDesignTime = Gui.IsInDesignTime;
        }

        /// <summary>
        /// Gets the instance.
        /// </summary>
        /// <value>
        /// The instance.
        /// </value>
        public static WindowsPlatform Instance { get; }

        /// <summary>
        /// Retrieves the platform-specific Native methods
        /// </summary>
        public INativeMethods NativeMethods { get; }

        /// <summary>
        /// Gets the GUI contaxt implementation.
        /// </summary>
        public IGraphicalContext Gui { get; }

        /// <summary>
        /// Gets a value indicating whether this instance is in debug mode.
        /// </summary>
        public bool IsInDebugMode { get; } = Debugger.IsAttached;

        /// <summary>
        /// Gets a value indicating whether this instance is in design time.
        /// </summary>
        public bool IsInDesignTime { get; }

        /// <summary>
        /// Enqueues the given instructions with the given arguments on the main application dispatcher.
        /// This is a way to execute code in a fire-and-forget style
        /// </summary>
        /// <param name="priority">The priority.</param>
        /// <param name="callback">The callback.</param>
        /// <param name="arguments">The arguments.</param>
        public void UIEnqueueInvoke(ActionPriority priority, Delegate callback, params object[] arguments)
        {
            Gui?.UIEnqueueInvoke(priority, callback, arguments);
        }

        /// <summary>
        /// Synchronously invokes the given instructions on the main application dispatcher.
        /// </summary>
        /// <param name="priority">The priority.</param>
        /// <param name="action">The action.</param>
        public void UIInvoke(ActionPriority priority, Action action)
        {
            Gui?.UIInvoke(priority, action);
        }

        /// <summary>
        /// Creates a UI-aware dispatcher timer that executes actions on a schedule basis.
        /// </summary>
        /// <param name="priority">The priority.</param>
        /// <returns>
        /// An instance of the dispatcher timer
        /// </returns>
        public IDispatcherTimer CreateDispatcherTimer(ActionPriority priority)
        {
            return new WindowsDispatcherTimer((DispatcherPriority)priority);
        }

        /// <summary>
        /// Creates a renderer of the specified media type.
        /// </summary>
        /// <param name="mediaType">Type of the media.</param>
        /// <param name="mediaEngine">The media engine.</param>
        /// <returns>
        /// The renderer
        /// </returns>
        /// <exception cref="NotSupportedException">When the media type is not supported</exception>
        public IMediaRenderer CreateRenderer(MediaType mediaType, MediaEngine mediaEngine)
        {
            if (mediaType == MediaType.Audio) return new AudioRenderer(mediaEngine);
            else if (mediaType == MediaType.Video) return new VideoRenderer(mediaEngine);
            else if (mediaType == MediaType.Subtitle) return new SubtitleRenderer(mediaEngine);

            throw new NotSupportedException($"No suitable renderer for Media Type '{mediaType}'");
        }

        /// <summary>
        /// Handles global FFmpeg library messages
        /// </summary>
        /// <param name="message">The message.</param>
        public void HandleFFmpegLogMessage(MediaLogMessage message)
        {
            MediaElement.RaiseFFmpegMessageLogged(typeof(MediaElement), message);
        }
    }
}
