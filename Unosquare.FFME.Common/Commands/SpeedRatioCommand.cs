namespace Unosquare.FFME.Commands
{
    using Core;
    using Shared;

    /// <summary>
    /// A command to change speed ratio asynchronously
    /// </summary>
    /// <seealso cref="Unosquare.FFME.Commands.MediaCommand" />
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
        public double SpeedRatio { get; set; } = Defaults.DefaultSpeedRatio;

        /// <summary>
        /// Performs the actions that this command implements.
        /// </summary>
        internal override void ExecuteInternal()
        {
            if (Manager.MediaCore.Clock.SpeedRatio != SpeedRatio)
                Manager.MediaCore.Clock.SpeedRatio = SpeedRatio;

            MediaEngine.Platform.GuiInvoke(ActionPriority.DataBind, () => 
            {
                Manager.MediaCore.SpeedRatio = Manager.MediaCore.Clock.SpeedRatio;
            });
        }
    }
}
