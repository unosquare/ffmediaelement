namespace Unosquare.FFME.Commands
{
    using Shared;
    using System.Threading.Tasks;

    /// <summary>
    /// A command to change speed ratio asynchronously
    /// </summary>
    /// <seealso cref="MediaCommand" />
    internal sealed class SpeedRatioCommand : MediaCommand
    {
        /// <summary>
        /// Initializes a new instance of the <see cref="SpeedRatioCommand"/> class.
        /// </summary>
        /// <param name="manager">The manager.</param>
        /// <param name="speedRatio">The speed ratio.</param>
        public SpeedRatioCommand(MediaCommandManager manager, double speedRatio)
            : base(manager, MediaCommandType.SetSpeedRatio)
        {
            SpeedRatio = speedRatio;
        }

        /// <summary>
        /// The target speed ratio
        /// </summary>
        public double SpeedRatio { get; set; } = Constants.Controller.DefaultSpeedRatio;

        /// <summary>
        /// Performs the actions that this command implements.
        /// </summary>
        /// <returns>A <see cref="Task"/> representing the asynchronous operation.</returns>
        internal override Task ExecuteInternal()
        {
            if (Manager.MediaCore.Clock.SpeedRatio != SpeedRatio)
                Manager.MediaCore.Clock.SpeedRatio = SpeedRatio;

            Manager.MediaCore.State.SpeedRatio = Manager.MediaCore.Clock.SpeedRatio;

            return Task.CompletedTask;
        }
    }
}
