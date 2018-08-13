namespace Unosquare.FFME.Core
{
    using FFmpeg.AutoGen;
    using Shared;
    using System;
    using System.Collections.Concurrent;
    using System.Collections.Generic;
    using System.Collections.ObjectModel;
    using System.Threading;

    /// <summary>
    /// A queue-based logger that automatically stats a background timer that
    /// empties the queue constantly, at low priority.
    /// </summary>
    internal static class LoggingWorker
    {
        #region Private Members

        private static readonly ConcurrentQueue<MediaLogMessage> LogQueue = new ConcurrentQueue<MediaLogMessage>();
        private static readonly List<string> FFmpegLogBuffer = new List<string>(1024);
        private static readonly object FFmpegLogBufferSyncLock = new object();
        private static readonly ReadOnlyDictionary<int, MediaLogMessageType> FFmpegLogLevels =
            new ReadOnlyDictionary<int, MediaLogMessageType>(
                new Dictionary<int, MediaLogMessageType>
                {
                    { ffmpeg.AV_LOG_DEBUG, MediaLogMessageType.Debug },
                    { ffmpeg.AV_LOG_ERROR, MediaLogMessageType.Error },
                    { ffmpeg.AV_LOG_FATAL, MediaLogMessageType.Error },
                    { ffmpeg.AV_LOG_INFO, MediaLogMessageType.Info },
                    { ffmpeg.AV_LOG_PANIC, MediaLogMessageType.Error },
                    { ffmpeg.AV_LOG_TRACE, MediaLogMessageType.Trace },
                    { ffmpeg.AV_LOG_WARNING, MediaLogMessageType.Warning },
                });

        private static readonly Timer LogOutputter;
        private static bool IsOutputingLog;

        #endregion

        #region Constructor

        /// <summary>
        /// Initializes static members of the <see cref="LoggingWorker"/> class.
        /// </summary>
        static LoggingWorker()
        {
            LogOutputter = new Timer((s) =>
            {
                if (IsOutputingLog) return;
                IsOutputingLog = true;
                try
                {
                    const int MaxMessagesPerCycle = 10;
                    var messageCount = 0;
                    while (messageCount <= MaxMessagesPerCycle && LogQueue.TryDequeue(out MediaLogMessage message))
                    {
                        if (message.Source != null)
                            message.Source.SendOnMessageLogged(message);
                        else
                            MediaEngine.Platform?.HandleFFmpegLogMessage(message);

                        messageCount += 1;
                    }
                }
                catch
                {
                    throw;
                }
                finally
                {
                    IsOutputingLog = false;
                }
            },
            LogQueue, // the state argument passed on to the ticker
            Convert.ToInt32(Constants.Interval.LowPriority.TotalMilliseconds),
            Convert.ToInt32(Constants.Interval.LowPriority.TotalMilliseconds));
        }

        #endregion

        #region Properties

        /// <summary>
        /// Gets the FFmpeg log callback method.
        /// Example: ffmpeg.av_log_set_callback(LoggingWorker.FFmpegLogCallback);
        /// </summary>
        private static unsafe av_log_set_callback_callback FFmpegLogCallback { get; } = OnFFmpegMessageLogged;

        #endregion

        #region Methods

        /// <summary>
        /// Starts to listen to FFmpeg logging messages.
        /// This method is not thread-safe.
        /// </summary>
        public static void ConnectToFFmpeg()
        {
            ffmpeg.av_log_set_flags(ffmpeg.AV_LOG_SKIP_REPEATED);
            ffmpeg.av_log_set_level(MediaEngine.Platform.IsInDebugMode ? ffmpeg.AV_LOG_VERBOSE : ffmpeg.AV_LOG_WARNING);
            ffmpeg.av_log_set_callback(FFmpegLogCallback);
        }

        /// <summary>
        /// Logs the specified message. This the genric logging mechanism available to all classes.
        /// </summary>
        /// <param name="sender">The sender.</param>
        /// <param name="messageType">Type of the message.</param>
        /// <param name="message">The message.</param>
        /// <exception cref="ArgumentNullException">sender</exception>
        /// <exception cref="ArgumentNullException">When sender is null</exception>
        public static void Log(MediaEngine sender, MediaLogMessageType messageType, string message)
        {
            if (sender == null) throw new ArgumentNullException(nameof(sender));
            var eventArgs = new MediaLogMessage(sender as MediaEngine, messageType, message);
            LogQueue.Enqueue(eventArgs);
        }

        /// <summary>
        /// Logs a block rendering operation as a Trace Message
        /// if the debugger is attached.
        /// </summary>
        /// <param name="mediaCore">The media engine.</param>
        /// <param name="block">The block.</param>
        /// <param name="clockPosition">The clock position.</param>
        /// <param name="renderIndex">Index of the render.</param>
        internal static void LogRenderBlock(this MediaEngine mediaCore, MediaBlock block, TimeSpan clockPosition, int renderIndex)
        {
            if (MediaEngine.Platform.IsInDebugMode == false) return;

            try
            {
                var drift = TimeSpan.FromTicks(clockPosition.Ticks - block.StartTime.Ticks);
                mediaCore?.Log(MediaLogMessageType.Trace,
                $"{block.MediaType.ToString().Substring(0, 1)} "
                    + $"BLK: {block.StartTime.Format()} | "
                    + $"CLK: {clockPosition.Format()} | "
                    + $"DFT: {drift.TotalMilliseconds,4:0} | "
                    + $"IX: {renderIndex,3} | "
                    + $"PQ: {mediaCore.Container?.Components[block.MediaType]?.BufferLength / 1024d,7:0.0}k | "
                    + $"TQ: {mediaCore.Container?.Components.BufferLength / 1024d,7:0.0}k");
            }
            catch
            {
                // swallow
            }
        }

        /// <summary>
        /// Logs the specified message. This the way ffmpeg messages are logged.
        /// </summary>
        /// <param name="messageType">Type of the message.</param>
        /// <param name="message">The message.</param>
        private static void LogGlobal(MediaLogMessageType messageType, string message)
        {
            var eventArgs = new MediaLogMessage(null, messageType, message);
            LogQueue.Enqueue(eventArgs);
        }

        /// <summary>
        /// Log message callback from ffmpeg library.
        /// </summary>
        /// <param name="p0">The p0.</param>
        /// <param name="level">The level.</param>
        /// <param name="format">The format.</param>
        /// <param name="vl">The vl.</param>
        private static unsafe void OnFFmpegMessageLogged(void* p0, int level, string format, byte* vl)
        {
            const int lineSize = 1024;
            lock (FFmpegLogBufferSyncLock)
            {
                if (level > ffmpeg.av_log_get_level()) return;
                var lineBuffer = stackalloc byte[lineSize];
                var printPrefix = 1;
                ffmpeg.av_log_format_line(p0, level, format, vl, lineBuffer, lineSize, &printPrefix);
                var line = FFInterop.PtrToStringUTF8(lineBuffer);
                FFmpegLogBuffer.Add(line);

                var messageType = MediaLogMessageType.Debug;
                if (FFmpegLogLevels.ContainsKey(level))
                    messageType = FFmpegLogLevels[level];

                if (line.EndsWith("\n"))
                {
                    line = string.Join(string.Empty, FFmpegLogBuffer);
                    line = line.TrimEnd();
                    FFmpegLogBuffer.Clear();
                    LogGlobal(messageType, line);
                }
            }
        }

        #endregion
    }
}
