namespace Unosquare.FFME.Commands
{
    using Core;
    using Shared;
    using System.Runtime.CompilerServices;
    using System.Text;

    /// <summary>
    /// Close Command Implementation
    /// </summary>
    /// <seealso cref="DirectCommandBase" />
    internal sealed class DirectCloseCommand : DirectCommandBase
    {
        /// <summary>
        /// Initializes a new instance of the <see cref="DirectCloseCommand"/> class.
        /// </summary>
        /// <param name="mediaCore">The media core.</param>
        public DirectCloseCommand(MediaEngine mediaCore)
            : base(mediaCore)
        {
            CommandType = CommandType.Close;
        }

        /// <inheritdoc />
        public override CommandType CommandType { get; }

        /// <inheritdoc />
        public override void PostProcess()
        {
            var m = MediaCore;
            if (m == null) return;

            // Update notification properties
            m.State.ResetAll();
            m.ResetPosition();
            m.State.UpdateMediaState(PlaybackStatus.Close);
            m.State.UpdateSource(null);

            // Notify media has closed
            MediaCore.SendOnMediaClosed();
            LogReferenceCounter();
            this.LogDebug(Aspects.EngineCommand, $"{CommandType} Completed");
        }

        /// <inheritdoc />
        protected override void PerformActions()
        {
            this.LogDebug(Aspects.EngineCommand, $"{CommandType} Entered");
            var m = MediaCore;

            // Wait for the workers to stop
            m.StopWorkers();

            // Dispose the container
            m.Container?.Dispose();
            m.Container = null;

            // Dispose the Blocks for all components
            foreach (var kvp in m.Blocks)
                kvp.Value.Dispose();

            m.Blocks.Clear();
            m.DisposePreloadedSubtitles();

            // Clear the render times
            m.LastRenderTime.Clear();
        }

        /// <summary>
        /// Outputs Reference Counter Results
        /// </summary>
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        private void LogReferenceCounter()
        {
            if (MediaEngine.Platform?.IsInDebugMode ?? true) return;
            if (RC.Current.InstancesByLocation.Count <= 0) return;

            var builder = new StringBuilder();
            builder.AppendLine("Unmanaged references were left alive. This is an indication that there is a memory leak.");
            foreach (var kvp in RC.Current.InstancesByLocation)
                builder.AppendLine($"    {kvp.Key,30} - Instances: {kvp.Value}");

            this.LogError(Aspects.ReferenceCounter, builder.ToString());
        }
    }
}
