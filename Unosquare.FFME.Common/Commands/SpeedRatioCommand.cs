namespace Unosquare.FFME.Commands
{
    using Shared;

    /// <summary>
    /// The Set Speed Ratio Command Implementation
    /// </summary>
    /// <seealso cref="CommandBase" />
    internal sealed class SpeedRatioCommand : CommandBase
    {
        /// <summary>
        /// Initializes a new instance of the <see cref="SpeedRatioCommand"/> class.
        /// </summary>
        /// <param name="mediaCore">The media core.</param>
        /// <param name="speedRatio">The speed ratio.</param>
        public SpeedRatioCommand(MediaEngine mediaCore, double speedRatio)
            : base(mediaCore)
        {
            SpeedRatio = speedRatio;
            CommandType = CommandType.SpeedRatio;
            Category = CommandCategory.Delayed;
        }

        /// <summary>
        /// Gets the command type identifier.
        /// </summary>
        public override CommandType CommandType { get; }

        /// <summary>
        /// Gets the command category.
        /// </summary>
        public override CommandCategory Category { get; }

        /// <summary>
        /// The target speed ratio
        /// </summary>
        public double SpeedRatio { get; set; } = Constants.Controller.DefaultSpeedRatio;

        /// <summary>
        /// Performs the actions represented by this deferred task.
        /// </summary>
        protected override void PerformActions()
        {
            if (MediaCore.Clock.SpeedRatio != SpeedRatio)
                MediaCore.Clock.SpeedRatio = SpeedRatio;

            MediaCore.State.SpeedRatio = MediaCore.Clock.SpeedRatio;
        }
    }
}
