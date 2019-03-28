namespace Unosquare.FFME.Diagnostics
{
    using Primitives;
    using System;
    using System.Collections.Concurrent;
    using System.Diagnostics;
    using System.Threading;

    /// <summary>
    /// A queue-based logger that automatically starts a background timer that
    /// empties the queue constantly, at a low priority, and in batches.
    /// Messages are handled by the <see cref="ILoggingHandler"/> object associated with the message.
    /// </summary>
    internal static class Logging
    {
        #region Private Members

        private static readonly ConcurrentQueue<MediaLogMessage> LogQueue = new ConcurrentQueue<MediaLogMessage>();
        private static readonly LogOutputTimerWorker LogOutputWorker = new LogOutputTimerWorker();

        #endregion

        #region Constructor

        /// <summary>
        /// Initializes static members of the <see cref="Logging"/> class.
        /// </summary>
        static Logging()
        {
            LogOutputWorker.StartAsync();
        }

        #endregion

        #region Extension Methods

        /// <summary>
        /// Logs the specified message.
        /// </summary>
        /// <param name="sender">The logging source.</param>
        /// <param name="aspectName">The apect of the code where the message is coming from.</param>
        /// <param name="message">The message text.</param>
        public static void LogDebug(this ILoggingSource sender, string aspectName, string message) =>
            Log(sender.LoggingHandler, MediaLogMessageType.Debug, aspectName, message);

        /// <summary>
        /// Logs the specified message.
        /// </summary>
        /// <param name="sender">The logging source.</param>
        /// <param name="aspectName">The apect of the code where the message is coming from.</param>
        /// <param name="message">The message text.</param>
        public static void LogInfo(this ILoggingSource sender, string aspectName, string message) =>
            Log(sender.LoggingHandler, MediaLogMessageType.Info, aspectName, message);

        /// <summary>
        /// Logs the specified message.
        /// </summary>
        /// <param name="sender">The logging source.</param>
        /// <param name="aspectName">The apect of the code where the message is coming from.</param>
        /// <param name="message">The message text.</param>
        public static void LogWarning(this ILoggingSource sender, string aspectName, string message) =>
            Log(sender.LoggingHandler, MediaLogMessageType.Warning, aspectName, message);

        /// <summary>
        /// Logs the specified message.
        /// </summary>
        /// <param name="sender">The logging source.</param>
        /// <param name="aspectName">The apect of the code where the message is coming from.</param>
        /// <param name="message">The message text.</param>
        public static void LogTrace(this ILoggingSource sender, string aspectName, string message) =>
            Log(sender.LoggingHandler, MediaLogMessageType.Trace, aspectName, message);

        /// <summary>
        /// Logs the specified message.
        /// </summary>
        /// <param name="sender">The logging source.</param>
        /// <param name="aspectName">The apect of the code where the message is coming from.</param>
        /// <param name="message">The message text.</param>
        public static void LogError(this ILoggingSource sender, string aspectName, string message) =>
           Log(sender.LoggingHandler, MediaLogMessageType.Error, aspectName, message);

        /// <summary>
        /// Logs the specified message.
        /// </summary>
        /// <param name="sender">The logging source.</param>
        /// <param name="aspectName">The apect of the code where the message is coming from.</param>
        /// <param name="message">The message text.</param>
        /// <param name="ex">The exception to log.</param>
        public static void LogError(this ILoggingSource sender, string aspectName, string message, Exception ex) =>
            Log(sender.LoggingHandler, MediaLogMessageType.Error, aspectName, $"{message}\r\n" +
                $"{ex?.GetType().Name}: {ex?.Message}\r\nStack Trace:\r\n{ex?.StackTrace}");

        #endregion

        #region Methods

        /// <summary>
        /// Logs the specified message. This the generic logging mechanism available to all classes.
        /// </summary>
        /// <param name="loggingHandler">The object that will handle the message output.</param>
        /// <param name="messageType">Type of the message.</param>
        /// <param name="message">The message.</param>
        internal static void Log(ILoggingHandler loggingHandler, MediaLogMessageType messageType, string message) =>
            Log(loggingHandler, messageType, Aspects.None, message);

        /// <summary>
        /// Logs the specified logging handler.
        /// </summary>
        /// <param name="loggingHandler">The logging handler.</param>
        /// <param name="messageType">Type of the message.</param>
        /// <param name="aspectName">Name of the code aspect where the message is coming from.</param>
        /// <param name="message">The message.</param>
        internal static void Log(ILoggingHandler loggingHandler, MediaLogMessageType messageType, string aspectName, string message)
        {
            // Prevent queueing messages without a handler
            if (loggingHandler == null || messageType == MediaLogMessageType.None)
                return;

            var messageItem = new MediaLogMessage(loggingHandler, messageType, message, aspectName);
            LogQueue.Enqueue(messageItem);
        }

        #endregion

        /// <summary>
        /// Implements the timer worker that outputs data to the log.
        /// </summary>
        /// <seealso cref="TimerWorkerBase" />
        private sealed class LogOutputTimerWorker : TimerWorkerBase
        {
            /// <summary>
            /// Initializes a new instance of the <see cref="LogOutputTimerWorker"/> class.
            /// </summary>
            public LogOutputTimerWorker()
                : base(nameof(LogOutputWorker), DefaultPeriod)
            {
                // placeholder
            }

            // <inheritdoc />
            protected override void ExecuteCycleLogic(CancellationToken ct)
            {
                try
                {
                    const int MaxMessagesPerCycle = 15;
                    var messageCount = 0;

                    while (!ct.IsCancellationRequested &&
                        messageCount <= MaxMessagesPerCycle &&
                        LogQueue.TryDequeue(out var message))
                    {
                        message.Handler?.HandleLogMessage(message);
                        messageCount += 1;
                    }
                }
                catch (Exception ex)
                {
                    Debug.WriteLine($"{nameof(Logging)}.{nameof(LogOutputWorker)} - {ex.GetType()}: {ex.Message}");
                }
            }

            // <inheritdoc />
            protected override void OnCycleException(Exception ex)
            {
                // placeholder
            }

            // <inheritdoc />
            protected override void OnDisposing()
            {
                // placeholder - nothing to dispose.
            }
        }
    }
}
