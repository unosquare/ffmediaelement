namespace Unosquare.FFME.Core
{
    using System;
    using System.Collections.Concurrent;
    using System.Collections.Generic;
    using System.Collections.ObjectModel;
    using System.Text;
    using System.Threading;
    using FFmpeg.AutoGen;
    using Shared;

    /// <summary>
    /// A queue-based logger
    /// </summary>
    internal static class LoggingWorker
    {
        private static readonly ConcurrentQueue<MediaLogMessage> LogQueue = new ConcurrentQueue<MediaLogMessage>();
        private static readonly List<string> FFmpegLogBuffer = new List<string>();
        private static readonly object FFmpegLogBufferSyncLock = new object();
        private static readonly ReadOnlyDictionary<int, MediaLogMessageType> FFmpegLogLevels =
            new ReadOnlyDictionary<int, MediaLogMessageType>(
                new Dictionary<int, MediaLogMessageType>
                {
                    {ffmpeg.AV_LOG_DEBUG, MediaLogMessageType.Debug},
                    {ffmpeg.AV_LOG_ERROR, MediaLogMessageType.Error},
                    {ffmpeg.AV_LOG_FATAL, MediaLogMessageType.Error},
                    {ffmpeg.AV_LOG_INFO, MediaLogMessageType.Info},
                    {ffmpeg.AV_LOG_PANIC, MediaLogMessageType.Error},
                    {ffmpeg.AV_LOG_TRACE, MediaLogMessageType.Trace},
                    {ffmpeg.AV_LOG_WARNING, MediaLogMessageType.Warning},
                });
        private static Timer LogOutputter = null;

        /// <summary>
        /// Initializes static members of the <see cref="LoggingWorker"/> class.
        /// </summary>
        static LoggingWorker()
        {
            LogOutputter = new Timer((s) =>
                {
                    while (LogQueue.TryDequeue(out MediaLogMessage eventArgs))
                    {
                        if (eventArgs.Source != null)
                            eventArgs.Source.SendOnMessageLogged(eventArgs);
                        else
                            MediaEngine.Platform?.HandleFFmpegLogMessage(eventArgs);
                    }
                },
                LogQueue,
                Constants.LogOutputterUpdateInterval,
                Constants.LogOutputterUpdateInterval);
        }

        /// <summary>
        /// Logs the specified message. This the genric logging mechanism available to all classes.
        /// </summary>
        /// <param name="sender">The sender.</param>
        /// <param name="messageType">Type of the message.</param>
        /// <param name="message">The message.</param>
        /// <exception cref="System.ArgumentNullException">sender</exception>
        public static void Log(object sender, MediaLogMessageType messageType, string message)
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
                    + $"PQ: {mediaCore.Container?.Components[block.MediaType]?.PacketBufferLength / 1024d,7:0.0}k | "
                    + $"TQ: {mediaCore.Container?.Components.PacketBufferLength / 1024d,7:0.0}k");
            }
            catch
            {
                // swallow
            }
        }

        /// <summary>
        /// Log message callback from ffmpeg library.
        /// </summary>
        /// <param name="p0">The p0.</param>
        /// <param name="level">The level.</param>
        /// <param name="format">The format.</param>
        /// <param name="vl">The vl.</param>
        internal static unsafe void OnFFmpegMessageLogged(void* p0, int level, string format, byte* vl)
        {
            const int lineSize = 1024;

            lock (FFmpegLogBufferSyncLock)
            {
                if (level > ffmpeg.av_log_get_level()) return;
                var lineBuffer = stackalloc byte[lineSize];
                var printPrefix = 1;
                ffmpeg.av_log_format_line(p0, level, format, vl, lineBuffer, lineSize, &printPrefix);
                var line = Utils.PtrToString(lineBuffer);
                FFmpegLogBuffer.Add(line);

                var messageType = MediaLogMessageType.Debug;
                if (FFmpegLogLevels.ContainsKey(level))
                    messageType = FFmpegLogLevels[level];

                if (line.EndsWith("\n"))
                {
                    line = string.Join(string.Empty, FFmpegLogBuffer);
                    line = line.TrimEnd();
                    FFmpegLogBuffer.Clear();
                    LoggingWorker.Log(typeof(MediaEngine), messageType, line);
                }
            }
        }
    }
}
